using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Experimental.Rendering;
using UnityEngine.Rendering;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine.Experimental.Rendering;

using Debug = UnityEngine.Debug;

[ExecuteInEditMode]
public class VoxelTerrainGenerator : MonoBehaviour
{
	public const int MinDispatchSize = 8;

	[Header("Compute shaders")]
	public ComputeShader	terrain3DNoiseShader;
	public ComputeShader	isoSurfaceShader;

	[Space, Header("Chunk settings")]
	public int				terrainChunkSize = 63;
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

	int						noiseKernel;
	Vector3Int				noiseKernelGroupSize;
	int						marchingCubeKernel;
	Vector3Int				marchingCubeKernelGroupSize;

	RenderTexture			noiseTexture;

	ComputeBuffer			verticesBuffer;
	ComputeBuffer			normalsBuffer;
	ComputeBuffer			trianglesBuffer;
	ComputeBuffer			verticesCountReadbackBuffer;

	ComputeBuffer			debugPointBuffer;

	new DrawProceduralRenderer		renderer;

	[GenerateHLSL]
	public struct DebugPoint
	{
		public Vector4 position; // xyz: world position, w component is the density of the 3D noise
		public Vector4 direction; // xyz: object normal, w is unused
	}

	public void Start ()
	{
		renderer = GetComponent< DrawProceduralRenderer >();

		noiseKernel = FindKernel(terrain3DNoiseShader, KernelIds.perlinNoise3DKernel, out noiseKernelGroupSize);
		marchingCubeKernel = FindKernel(isoSurfaceShader, "VoxelIsoSurface", out marchingCubeKernelGroupSize);

		// We add 1 so we can generate seamless normals at the cost of chunkSize * chunkSize * 3 cells
		GenerateBuffers(terrainChunkSize + 1);
		BindBuffers(chunkPosition, terrainChunkSize + 1);

		foreach (var unused in GenerateStep())
			;
	}

	public IEnumerable GenerateStep()
	{
		renderer.ClearChunks();

		int x = 0;
		// for (int x = -chunkLoadSize; x <= chunkLoadSize; x++)
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

		// Create the computeBuffer that will store vertices and indices for draw procedural
		verticesBuffer = new ComputeBuffer(chunkSize * chunkSize * chunkSize * 15 * resolutionPerVoxel, sizeof(float) * 3, ComputeBufferType.Counter);
		// The maximum number of triangles is 5 times the number of voxel (with the marching cubes algorithm)
		trianglesBuffer = new ComputeBuffer(chunkSize * chunkSize * chunkSize * 5 * resolutionPerVoxel, sizeof(float) * 3, ComputeBufferType.Default);
		// A buffer to generate the triangle indicies and keep track of the number of triangles from compute shader
		verticesCountReadbackBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		// The normal buffer, set to the maximum of vertex
		normalsBuffer = new ComputeBuffer(chunkSize * chunkSize * chunkSize * 15 * resolutionPerVoxel, sizeof(float) * 3, ComputeBufferType.Default);
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
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.verticesId, verticesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.trianglesId, trianglesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.normalsId, normalsBuffer);
		isoSurfaceShader.SetVector(KernelIds.chunkSize, Vector4.one * chunkSize);
		isoSurfaceShader.SetVector(KernelIds.worldPosition, chunkPosition * chunkSize);
		isoSurfaceShader.SetInt(KernelIds.resolutionPerVoxel, resolutionPerVoxel);
	}

	int FindKernel(ComputeShader computeShader, string kernelName, out Vector3Int size)
	{
		int kernel = computeShader.FindKernel(kernelName);
		uint sizeX, sizeY, sizeZ;

		computeShader.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
		size = new Vector3Int((int)sizeX, (int)sizeY, (int)sizeZ);

		return kernel;
	}

	public Dictionary< Vector3, Vector3[] > chunkNormals = new Dictionary<Vector3, Vector3[]>();
	public void GenerateTerrain(Vector3 worldPosition, int chunkSize)
	{
		Stopwatch sw = new Stopwatch();
		int dispatchSize = (int)Mathf.Ceil((float)chunkSize / (float)MinDispatchSize);
		sw.Start();

		verticesBuffer.SetCounterValue(0);

		// 3D noise generation
		terrain3DNoiseShader.Dispatch(noiseKernel, dispatchSize, dispatchSize, dispatchSize);

		// Isosurface generation
		isoSurfaceShader.Dispatch(marchingCubeKernel, dispatchSize * resolutionPerVoxel, dispatchSize * resolutionPerVoxel, dispatchSize * resolutionPerVoxel);

		ComputeBuffer.CopyCount(verticesBuffer, verticesCountReadbackBuffer, 0);

		// Vector3[] n = new Vector3[chunkSize * chunkSize * chunkSize];
		// normalsBuffer.GetData(n);
		// chunkNormals[worldPosition] = n;

		// Get the vertices count back to the cpu
		int[] verticesCountBuffer = new int[1];
		verticesCountReadbackBuffer.GetData(verticesCountBuffer);
		int verticesCount = verticesCountBuffer[0] * 3;

		MaterialPropertyBlock properties = new MaterialPropertyBlock();

		// TODO
		// properties.SetBuffer();

		renderer.ClearChunks();
		renderer.AddChunkToRender(verticesBuffer, properties, new Bounds(chunkPosition, chunkSize * Vector3.one));

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
		verticesCountReadbackBuffer?.Release();
		verticesBuffer?.Release();
		trianglesBuffer?.Release();
	}
}