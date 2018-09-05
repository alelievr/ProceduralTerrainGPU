using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Debug = UnityEngine.Debug;

[ExecuteInEditMode]
public class TerrainGenerator : MonoBehaviour
{
	[Header("Compute shaders")]
	public ComputeShader	terrain3DNoiseShader;
	public ComputeShader	isoSurfaceShader;
	public ComputeShader	normalFromNoiseShader;
	public ComputeShader	copyMeshBuffersShader;

	[Space, Header("Chunk settings")]
	public int				size = 64;
	public int				chunkLoadSize = 1;

	[Space, Header("Noise settings")]
	public float			seed;
	public float			scale = 1;
	// public float			persistance = 1.2f;
	public float			lacunarity = 2.0f;
	public int				octaves = 3;
	public float			gain = 1;
	public float			frequency = 1;
	public Vector3			chunkPosition;

	[Space, Header("Terrain material")]
	public Material			terrainMaterial;

	[Space, Header("Debug")]
	public int				maxDebugPoints = 5000;
	
	int						noiseKernel;
	Vector3Int				noiseKernelGroupSize;
	int						marchingCubeKernel;
	Vector3Int				marchingCubeKernelGroupSize;
	int						computeNormalKernel;
	Vector3Int				computeNormalGroupSize;
	int						copyMeshBufferKernel;
	Vector3Int				copyMeshBufferGroupSize;

	RenderTexture			noiseTexture;
	RenderTexture			normalTexture;

	ComputeBuffer			verticesBuffer;
	ComputeBuffer			normalsBuffer;
	ComputeBuffer			trianglesBuffer;
	ComputeBuffer			verticesCountReadbackBuffer;

	ComputeBuffer			debugPointBuffer;

	new TerrainRenderer		renderer;

	[GenerateHLSL]
	public struct DebugPoint
	{
		public Vector4 position; // xyz: world position, w component is the density of the 3D noise
		public Vector4 direction; // xyz: object normal, w is unused
	}

	static class TerrainShaderIncludePath
	{
		#if UNITY_EDITOR
		[UnityEditor.ShaderIncludePath]
		public static string[] GetPaths()
		{
			return new string[] {
				"Assets/Scripts/"
			};
		}
		#endif
	}

	public void Start ()
	{
		renderer = GetComponent< TerrainRenderer >();
		
		noiseKernel = FindKernel(terrain3DNoiseShader, KernelIds.perlinNoise3DKernel, out noiseKernelGroupSize);
		marchingCubeKernel = FindKernel(isoSurfaceShader, KernelIds.marchingCubeKernel, out marchingCubeKernelGroupSize);
		computeNormalKernel = FindKernel(normalFromNoiseShader, KernelIds.computeNormalKernel, out computeNormalGroupSize);
		copyMeshBufferKernel = FindKernel(copyMeshBuffersShader, KernelIds.copyMeshBuffers, out copyMeshBufferGroupSize);

		GenerateBuffers();
		BindBuffers();

		foreach (var unused in GenerateStep())
			;
	}

	public IEnumerable GenerateStep()
	{
		renderer.ClearChunks();

		for (int x = -chunkLoadSize; x <= chunkLoadSize; x++)
			for (int z = -chunkLoadSize; z <= chunkLoadSize; z++)
			{
				chunkPosition = new Vector3(x, 0, z);
				terrain3DNoiseShader.SetVector(KernelIds.chunkPosition, chunkPosition);
				GenerateBuffers();
				BindBuffers();
				GenerateTerrain();
				yield return null;
			}
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

		// Debug buffer to display a list of objects
		debugPointBuffer = new ComputeBuffer(maxDebugPoints, Marshal.SizeOf(typeof(DebugPoint)), ComputeBufferType.Append);
	}

	void BindBuffers()
	{
		// Bind noise kernel parameters:
		terrain3DNoiseShader.SetTexture(noiseKernel, KernelIds.noiseTextureId, noiseTexture);
		terrain3DNoiseShader.SetVector(KernelIds.chunkPosition, chunkPosition);
		terrain3DNoiseShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);
		terrain3DNoiseShader.SetFloat(KernelIds.seed, seed);
		terrain3DNoiseShader.SetFloat(KernelIds.lacunarity, lacunarity);
		terrain3DNoiseShader.SetInt(KernelIds.octaves, octaves);
		terrain3DNoiseShader.SetFloat(KernelIds.scale, scale);
		terrain3DNoiseShader.SetFloat(KernelIds.gain, gain);
		terrain3DNoiseShader.SetFloat(KernelIds.frequency, frequency);

		// Bind marching cubes parameters:
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.noiseTextureId, noiseTexture);
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.normalTextureId, normalTexture);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.verticesId, verticesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.trianglesId, trianglesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.normalsId, normalsBuffer);
		isoSurfaceShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);

		// Bind normal compute parameters:
		normalFromNoiseShader.SetTexture(computeNormalKernel, KernelIds.noiseTextureId, noiseTexture);
		normalFromNoiseShader.SetTexture(computeNormalKernel, KernelIds.normalTextureId, normalTexture);
		normalFromNoiseShader.SetVector(KernelIds.chunkSizeId, Vector4.one * size);

		// Bind buffer copy parameter (only the generated ones, as we don't have )
		copyMeshBuffersShader.SetBuffer(copyMeshBufferKernel, KernelIds.generatedVertices, verticesBuffer);
		copyMeshBuffersShader.SetBuffer(copyMeshBufferKernel, KernelIds.generatedNormals, normalsBuffer);
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
		Stopwatch sw = new Stopwatch();
		sw.Start();

		verticesBuffer.SetCounterValue(0);
		normalsBuffer.SetCounterValue(0);
		trianglesBuffer.SetCounterValue(0);
		
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

		// ComputeBuffer drawChunkBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
		// // Create an argument buffer for the DrawProceduralIndirect
		// int[] drawBufferData = {verticesCount, 1, 0, 0};
		// drawChunkBuffer.SetData(drawBufferData);

		// Align the buffer size on the copy kernel dispatch size
		int copySize = verticesCount + (copyMeshBufferGroupSize.x - (verticesCount % copyMeshBufferGroupSize.x));
		// Allocate the final buffers for the mesh
		// ComputeBuffer meshVertices = new ComputeBuffer(copySize, sizeof(float) * 3);
		// ComputeBuffer meshNormals = new ComputeBuffer(copySize, sizeof(float) * 3);

		// Bind these buffers for copy
		// copyMeshBuffersShader.SetBuffer(copyMeshBufferKernel, KernelIds.meshVertices, meshVertices);
		// copyMeshBuffersShader.SetBuffer(copyMeshBufferKernel, KernelIds.meshNormals, meshNormals);

		// And copy them
		// copyMeshBuffersShader.Dispatch(copyMeshBufferKernel, copySize / copyMeshBufferGroupSize.x, 1, 1);

		renderer.ClearChunks();
		Debug.Log("One chunk generated !");
		renderer.AddChunkToRender(verticesBuffer, normalsBuffer, chunkPosition * size, verticesCount);

		sw.Stop();
		Debug.Log("3D noise generated in " + sw.Elapsed.TotalMilliseconds + " ms");
	}

	public void Update()
	{
		
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
