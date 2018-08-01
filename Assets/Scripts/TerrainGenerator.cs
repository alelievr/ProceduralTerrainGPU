using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

[ExecuteInEditMode]
public class TerrainGenerator : MonoBehaviour
{
	[Header("Compute shaders")]
	public ComputeShader	terrain3DNoiseShader;
	public ComputeShader	isoSurfaceShader;
	public ComputeShader	normalFromNoiseShader;

	[Space, Header("Chunk settings")]
	public int				size = 64;

	[Space, Header("Noise settings")]
	public float			seed;
	public float			scale = 1;
	// public float			persistance = 1.2f;
	public float			lacunarity = 2.0f;
	public int				octaves = 3;
	public Vector3			position;

	[Space, Header("Terrain material")]
	public Material			terrainMaterial;
	
	[Space, Header("Debug")]
	public Material			visualize3DNoiseMaterial;
	public Mesh				generatedMesh;
	public MeshRenderer		debugMeshRenderer;

	int						noiseKernel;
	Vector3Int				noiseKernelGroupSize;
	int						marchingCubeKernel;
	Vector3Int				marchingCubeKernelGroupSize;
	int						computeNormalKernel;
	Vector3Int				computeNormalGroupSize;

	RenderTexture			noiseTexture;
	RenderTexture			normalTexture;
	RenderTexture			debugTexture;

	ComputeBuffer			verticesBuffer;
	ComputeBuffer			normalsBuffer;
	ComputeBuffer			trianglesBuffer;
	ComputeBuffer			verticesCountReadbackBuffer;

	ComputeBuffer			drawBuffer;

	[System.NonSerialized]
	bool					displayTerrain = false;

	public void Start ()
	{
		displayTerrain = false;
		noiseKernel = FindKernel(terrain3DNoiseShader, KernelIds.perlinNoise3DKernel, out noiseKernelGroupSize);
		marchingCubeKernel = FindKernel(isoSurfaceShader, KernelIds.marchingCubeKernel, out marchingCubeKernelGroupSize);
		computeNormalKernel = FindKernel(normalFromNoiseShader, KernelIds.computeNormalKernel, out computeNormalGroupSize);

		GenerateBuffers();

		// Bind noise kernel parameters:
		terrain3DNoiseShader.SetTexture(noiseKernel, KernelIds.noiseTextureId, noiseTexture);
		terrain3DNoiseShader.SetVector(KernelIds.chunkPosition, position);
		terrain3DNoiseShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);
		terrain3DNoiseShader.SetFloat(KernelIds.seed, seed);
		terrain3DNoiseShader.SetFloat(KernelIds.lacunarity, lacunarity);
		terrain3DNoiseShader.SetInt(KernelIds.octaves, octaves);
		terrain3DNoiseShader.SetFloat(KernelIds.scale, scale);

		// Bind marching cubes parameters:
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.noiseTextureId, noiseTexture);
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.debugTextureId, debugTexture);
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.normalTextureId, normalTexture);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.verticesId, verticesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.trianglesId, trianglesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.normalsId, normalsBuffer);
		isoSurfaceShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);

		// Bind normal compute parameters:
		normalFromNoiseShader.SetTexture(computeNormalKernel, KernelIds.noiseTextureId, noiseTexture);
		normalFromNoiseShader.SetTexture(computeNormalKernel, KernelIds.normalTextureId, normalTexture);
		normalFromNoiseShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);
	
		// Bind debug parameters:
		visualize3DNoiseMaterial.SetTexture("_NoiseTex", normalTexture);

		GenerateTerrain();
	}

	void GenerateBuffers()
	{
		// Create the noise texture
		noiseTexture = new RenderTexture(size, size, 0, GraphicsFormat.R16_SFloat);
		noiseTexture.dimension = TextureDimension.Tex3D;
		noiseTexture.enableRandomWrite = true;
		noiseTexture.volumeDepth = size;
		noiseTexture.filterMode = FilterMode.Point;
		noiseTexture.wrapMode = TextureWrapMode.Clamp;
		noiseTexture.Create();
		
		// Create the debug texture
		debugTexture = new RenderTexture(size, size, 0, GraphicsFormat.R16_SFloat);
		debugTexture.dimension = TextureDimension.Tex3D;
		debugTexture.enableRandomWrite = true;
		debugTexture.volumeDepth = size;
		debugTexture.filterMode = FilterMode.Point;
		debugTexture.wrapMode = TextureWrapMode.Clamp;
		debugTexture.Create();
		
		// Create the normal texture
		// normalTexture = new RenderTexture(size, size, 0, GraphicsFormat.R16G16B16_SNorm);
		normalTexture = new RenderTexture(size, size, 0);
		normalTexture.format = RenderTextureFormat.ARGBHalf;
		normalTexture.dimension = TextureDimension.Tex3D;
		normalTexture.enableRandomWrite = true;
		normalTexture.volumeDepth = size;
		normalTexture.filterMode = FilterMode.Point;
		normalTexture.wrapMode = TextureWrapMode.Clamp;
		normalTexture.Create();

		// Create the computeBuffer that will store vertices and indices for draw procedural
		verticesBuffer = new ComputeBuffer(size * size * size * 15, sizeof(float) * 3, ComputeBufferType.Counter);
		// The maximum number of triangles is 5 times the number of volxel (with the marching cubes algorithm)
		trianglesBuffer = new ComputeBuffer(size * size * size * 5, sizeof(float) * 3, ComputeBufferType.Default);
		// A buffer to generate the triangle indicies and keep track of the number of triangles from compute shader
		verticesCountReadbackBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		// The normal buffer, set to the maximum of vertex
		normalsBuffer = new ComputeBuffer(size * size * size * 15, sizeof(float) * 3, ComputeBufferType.Default);

		drawBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
	}

	int FindKernel(ComputeShader computeShader, string kernelName, out Vector3Int size)
	{
		int kernel = computeShader.FindKernel(kernelName);
		uint sizeX, sizeY, sizeZ;
		
		computeShader.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
		size = new Vector3Int((int)sizeX, (int)sizeY, (int)sizeZ);

		return kernel;
	}

	public void GenerateTerrain()
	{
		displayTerrain = false;
		Stopwatch sw = new Stopwatch();
		sw.Start();

		verticesBuffer.SetCounterValue(0);
		
		// 3D noise generation
		terrain3DNoiseShader.Dispatch(noiseKernel, size / noiseKernelGroupSize.x, size / noiseKernelGroupSize.y, size / noiseKernelGroupSize.z);

		// Generate normals
		normalFromNoiseShader.Dispatch(computeNormalKernel, size / computeNormalGroupSize.x, size / computeNormalGroupSize.y, size / computeNormalGroupSize.z);

		// Isosurface generation
		isoSurfaceShader.Dispatch(marchingCubeKernel, size / marchingCubeKernelGroupSize.x, size / marchingCubeKernelGroupSize.y, size / marchingCubeKernelGroupSize.z);

		ComputeBuffer.CopyCount(verticesBuffer, verticesCountReadbackBuffer, 0);

		// Get the vertices count back to the cpu
		int[] verticesCountBuffer = new int[1];
		verticesCountReadbackBuffer.GetData(verticesCountBuffer);
		int verticesCount = verticesCountBuffer[0] * 3;

		// Create an argument buffer for the DrawProceduralIndirect
		int[] drawBufferData = {verticesCount, 1, 0, 0};
		drawBuffer.SetData(drawBufferData);

		CreateDebugMesh(verticesCount);

		sw.Stop();
		Debug.Log("3D noise generated in " + sw.Elapsed.TotalMilliseconds + " ms");

		displayTerrain = true;
	}

	void CreateDebugMesh(int verticesCount)
	{
		Vector3[] a = new Vector3[verticesCount];
		verticesBuffer.GetData(a);

		Vector3[] n = new Vector3[verticesCount];
		normalsBuffer.GetData(n);

		// for (int i = 0; i < a.Length; i++)
		// 	Debug.Log("vertice: " + a[i]);

		int[] t = new int[verticesCount];
		trianglesBuffer.GetData(t);
		
		// for (int i = 0; i < a.Length; i++)
		// 	Debug.Log("triangle: " + t[i]);

		generatedMesh = new Mesh();
		generatedMesh.vertices = a;
		generatedMesh.triangles = t;
		generatedMesh.normals = n;

		debugMeshRenderer.GetComponent< MeshFilter >().sharedMesh = generatedMesh;
	}

	int i = 0;
	
	public void Update ()
	{
		i++;

		if (displayTerrain)
		{
			terrainMaterial.SetPass(0);
			Graphics.DrawProceduralIndirect(MeshTopology.Triangles, drawBuffer, 0);

			Debug.Log("draw !");

			if (i >= 500)
			{
				displayTerrain = false;
				i = 0;
			}
		}
	}

	private void OnDestroy()
	{
		Release();
	}

	private void Release()
	{
		verticesCountReadbackBuffer.Release();
		verticesBuffer.Release();
		trianglesBuffer.Release();
	}
}
