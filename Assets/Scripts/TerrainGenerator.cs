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

	[Space, Header("Chunk settings")]
	public int				size = 64;

	[Space, Header("Noise settings")]
	public int				seed;
	public float			scale = 1;
	public float			persistance = 1.2f;
	public float			lacunarity = 1.6f;

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

	RenderTexture			noiseTexture;
	RenderTexture			debugTexture;

	ComputeBuffer			verticesBuffer;
	ComputeBuffer			trianglesBuffer;
	ComputeBuffer			counterBuffer;

	ComputeBuffer			drawBuffer;

	[System.NonSerialized]
	bool					displayTerrain = false;

	public void Start ()
	{
		displayTerrain = false;
		noiseKernel = FindKernel(KernelIds.perlinNoise3DKernel, out noiseKernelGroupSize);
		marchingCubeKernel = FindKernel(KernelIds.marchingCubeKernel, out marchingCubeKernelGroupSize);

		GenerateBuffers();

		// Bind noise kernel parameters:
		terrain3DNoiseShader.SetTexture(noiseKernel, KernelIds.noiseTextureId, noiseTexture);
		terrain3DNoiseShader.SetVector(KernelIds.chunkPositionId, Vector4.zero);
		terrain3DNoiseShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);

		// Bind marching cubes parameters:
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.noiseTextureId, noiseTexture);
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.debugTextureId, debugTexture);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.verticesId, verticesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.trianglesId, trianglesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.triangleCounterId, counterBuffer);
		isoSurfaceShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);
	
		// Bind debug parameters:
		visualize3DNoiseMaterial.SetTexture("_NoiseTex", debugTexture);

		GenerateTerrain();
	}

	void GenerateBuffers()
	{
		// Create the noise texture
		noiseTexture = new RenderTexture(size, size, size, GraphicsFormat.R16_SFloat);
		noiseTexture.dimension = TextureDimension.Tex3D;
		noiseTexture.enableRandomWrite = true;
		noiseTexture.volumeDepth = size;
		noiseTexture.filterMode = FilterMode.Point;
		noiseTexture.Create();
		
		// Create the debug texture
		debugTexture = new RenderTexture(size, size, size, GraphicsFormat.R16_SFloat);
		debugTexture.dimension = TextureDimension.Tex3D;
		debugTexture.enableRandomWrite = true;
		debugTexture.volumeDepth = size;
		debugTexture.filterMode = FilterMode.Point;
		debugTexture.Create();

		// Create the computeBuffer that will store vertices and indices for draw procedural
		verticesBuffer = new ComputeBuffer(size * size * size * 15, sizeof(float) * 3, ComputeBufferType.Default);
		// The maximum number of triangles is 5 times the number of volxel (with the marching cubes algorithm)
		trianglesBuffer = new ComputeBuffer(size * size * size * 5, sizeof(float) * 3, ComputeBufferType.Default);
		// A buffer to generate the triangle indicies and keep track of the number of triangles from compute shader
		counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Counter);

		drawBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
	}

	int FindKernel(string kernelName, out Vector3Int size)
	{
		int kernel = terrain3DNoiseShader.FindKernel(KernelIds.perlinNoise3DKernel);
		uint sizeX, sizeY, sizeZ;
		
		terrain3DNoiseShader.GetKernelThreadGroupSizes(noiseKernel, out sizeX, out sizeY, out sizeZ);
		size = new Vector3Int((int)sizeX, (int)sizeY, (int)sizeZ);

		return kernel;
	}

	public void GenerateTerrain()
	{
		displayTerrain = false;
		Stopwatch sw = new Stopwatch();
		sw.Start();

		verticesBuffer.SetCounterValue(0);
		trianglesBuffer.SetCounterValue(0);
		counterBuffer.SetCounterValue(0);
		
		// 3D noise generation
		terrain3DNoiseShader.Dispatch(noiseKernel, size / noiseKernelGroupSize.x, size / noiseKernelGroupSize.y, size / noiseKernelGroupSize.z);

		// Isosurface generation
		isoSurfaceShader.Dispatch(marchingCubeKernel, size / marchingCubeKernelGroupSize.x, size / marchingCubeKernelGroupSize.y, size / marchingCubeKernelGroupSize.z);

		int[] drawBufferData = {0, 1, 0, 0};
		drawBuffer.SetData(drawBufferData);
		ComputeBuffer.CopyCount(verticesBuffer, drawBuffer, 0);

		ComputeBuffer tmp = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		ComputeBuffer.CopyCount(counterBuffer, tmp, 0);
		
		int[] c = new int[1];
		tmp.GetData(c);
		Debug.Log("Counter: " + c[0]);
		
		Vector3[] a = new Vector3[c[0] * 3];
		verticesBuffer.GetData(a);

		int[] t = new int[c[0] * 3];
		trianglesBuffer.GetData(t);

		// for (int i = 0; i < c[0]; i++)
		// {
		// 	t[i] = i;
		// }

		generatedMesh = new Mesh();
		generatedMesh.vertices = a;
		generatedMesh.triangles = t;

		debugMeshRenderer.GetComponent< MeshFilter >().sharedMesh = generatedMesh;
		
		sw.Stop();
		Debug.Log("3D noise generated in " + sw.Elapsed.TotalMilliseconds + " ms");

		displayTerrain = true;
	}

	int i = 0;
	
	public void Update ()
	{
		i++;

		if (displayTerrain)
		{
			terrainMaterial.SetPass(0);
			Graphics.DrawProceduralIndirect(MeshTopology.Triangles, drawBuffer, 0);

			if (i == 500)
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
		counterBuffer.Release();
		verticesBuffer.Release();
		trianglesBuffer.Release();
	}
}
