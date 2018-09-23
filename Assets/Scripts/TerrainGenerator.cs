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
	public int				terrainChunkSize = 64;
	public int				chunkLoadSize = 1;
	public int				resolutionPerVoxel = 1;

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

	public void Start ()
	{
		renderer = GetComponent< TerrainRenderer >();
		
		noiseKernel = FindKernel(terrain3DNoiseShader, KernelIds.perlinNoise3DKernel, out noiseKernelGroupSize);
		marchingCubeKernel = FindKernel(isoSurfaceShader, KernelIds.marchingCubeKernel, out marchingCubeKernelGroupSize);
		computeNormalKernel = FindKernel(normalFromNoiseShader, KernelIds.computeNormalKernel, out computeNormalGroupSize);
		copyMeshBufferKernel = FindKernel(copyMeshBuffersShader, KernelIds.copyMeshBuffers, out copyMeshBufferGroupSize);

		// We add 1 so we can generate seamless normals at the cost of chunkSize * chunkSize * 3 cells
		GenerateBuffers(terrainChunkSize + 1);
		BindBuffers(chunkPosition, terrainChunkSize + 1);

		foreach (var unused in GenerateStep())
			;
	}

	public IEnumerable GenerateStep()
	{
		renderer.ClearChunks();

		for (int x = -chunkLoadSize; x <= chunkLoadSize; x++)
			for (int z = -chunkLoadSize; z <= chunkLoadSize; z++)
			{
				Vector3 pos = chunkPosition + new Vector3(x, 0, z);
				BindBuffers(pos, terrainChunkSize + 1);
				GenerateTerrain(pos, terrainChunkSize + 1);
				yield return null;
			}
	}

	void GenerateBuffers(int chunkSize)
	{
		// Create the noise texture
		noiseTexture = new RenderTexture(chunkSize, chunkSize, 0, GraphicsFormat.R16_SFloat);
		noiseTexture.dimension = TextureDimension.Tex3D;
		noiseTexture.enableRandomWrite = true;
		noiseTexture.volumeDepth = chunkSize;
		noiseTexture.filterMode = FilterMode.Point;
		noiseTexture.wrapMode = TextureWrapMode.Clamp;
		noiseTexture.name = "Noise 3D Texture";
		noiseTexture.Create();
		
		// Create the normal texture
		normalTexture = new RenderTexture(chunkSize, chunkSize, 0);
		normalTexture.format = RenderTextureFormat.ARGBHalf;
		normalTexture.dimension = TextureDimension.Tex3D;
		normalTexture.enableRandomWrite = true;
		normalTexture.volumeDepth = chunkSize;
		normalTexture.filterMode = FilterMode.Point;
		normalTexture.wrapMode = TextureWrapMode.Clamp;
		normalTexture.name = "Normal 3D Texture";
		normalTexture.Create();

		// Create the computeBuffer that will store vertices and indices for draw procedural
		verticesBuffer = new ComputeBuffer(chunkSize * chunkSize * chunkSize * 15 * resolutionPerVoxel, sizeof(float) * 3, ComputeBufferType.Counter);
		// The maximum number of triangles is 5 times the number of voxel (with the marching cubes algorithm)
		trianglesBuffer = new ComputeBuffer(chunkSize * chunkSize * chunkSize * 5 * resolutionPerVoxel, sizeof(float) * 3, ComputeBufferType.Default);
		// A buffer to generate the triangle indicies and keep track of the number of triangles from compute shader
		verticesCountReadbackBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		// The normal buffer, set to the maximum of vertex
		normalsBuffer = new ComputeBuffer(chunkSize * chunkSize * chunkSize * 15 * resolutionPerVoxel, sizeof(float) * 3, ComputeBufferType.Default);

		// Debug buffer to display a list of objects
		debugPointBuffer = new ComputeBuffer(maxDebugPoints, Marshal.SizeOf(typeof(DebugPoint)), ComputeBufferType.Append);
	}

	void BindBuffers(Vector3 worldPosition, int chunkSize)
	{
		// Bind noise kernel parameters:
		terrain3DNoiseShader.SetTexture(noiseKernel, KernelIds.noiseTexture, noiseTexture);
		terrain3DNoiseShader.SetVector(KernelIds.chunkPosition, worldPosition);
		terrain3DNoiseShader.SetVector(KernelIds.chunkSize, Vector4.one * chunkSize);
		terrain3DNoiseShader.SetFloat(KernelIds.seed, seed);
		terrain3DNoiseShader.SetFloat(KernelIds.lacunarity, lacunarity);
		terrain3DNoiseShader.SetInt(KernelIds.octaves, octaves);
		terrain3DNoiseShader.SetFloat(KernelIds.scale, scale);
		terrain3DNoiseShader.SetFloat(KernelIds.gain, gain);
		terrain3DNoiseShader.SetFloat(KernelIds.frequency, frequency);

		// Bind marching cubes parameters:
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.noiseTexture, noiseTexture);
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.normalTexture, normalTexture);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.verticesId, verticesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.trianglesId, trianglesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.normalsId, normalsBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.debugPoints, debugPointBuffer);
		isoSurfaceShader.SetVector(KernelIds.chunkSize, Vector4.one * chunkSize);
		isoSurfaceShader.SetVector(KernelIds.worldPosition, chunkPosition * chunkSize);
		isoSurfaceShader.SetInt(KernelIds.resolutionPerVoxel, resolutionPerVoxel);

		// Bind normal compute parameters:
		normalFromNoiseShader.SetTexture(computeNormalKernel, KernelIds.noiseTexture, noiseTexture);
		normalFromNoiseShader.SetTexture(computeNormalKernel, KernelIds.normalTexture, normalTexture);
		normalFromNoiseShader.SetVector(KernelIds.chunkSize, Vector4.one * chunkSize);

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

	public void GenerateTerrain(Vector3 worldPosition, int chunkSize)
	{
		Stopwatch sw = new Stopwatch();
		sw.Start();

		verticesBuffer.SetCounterValue(0);
		normalsBuffer.SetCounterValue(0);
		trianglesBuffer.SetCounterValue(0);
		
		// 3D noise generation
		terrain3DNoiseShader.Dispatch(noiseKernel, chunkSize / noiseKernelGroupSize.x, chunkSize / noiseKernelGroupSize.y, chunkSize / noiseKernelGroupSize.z);

		// Generate normals
		normalFromNoiseShader.Dispatch(computeNormalKernel, chunkSize / computeNormalGroupSize.x, chunkSize / computeNormalGroupSize.y, chunkSize / computeNormalGroupSize.z);

		// We don't need the extra size anymore so we restore the original chunk size
		chunkSize -= 1;

		// Isosurface generation
		isoSurfaceShader.Dispatch(marchingCubeKernel, chunkSize * resolutionPerVoxel / marchingCubeKernelGroupSize.x, chunkSize * resolutionPerVoxel / marchingCubeKernelGroupSize.y, chunkSize * resolutionPerVoxel / marchingCubeKernelGroupSize.z);

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
		renderer.AddChunkToRender(verticesBuffer, normalsBuffer, worldPosition * chunkSize, verticesCount);

		sw.Stop();
	}

	public void Update()
	{
#if UNITY_EDITOR
		if (UnityEditor.EditorApplication.isPlaying)
        {
			renderer.ClearChunks();
            foreach (var unused in GenerateStep())
                ;
		}
#endif
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
