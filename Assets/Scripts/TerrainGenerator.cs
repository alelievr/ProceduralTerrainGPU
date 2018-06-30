using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

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
	
	[Space, Header("Debug")]
	public Material			visualize3DNoiseMaterial;

	int						noiseKernel;
	Vector3Int				noiseKernelGroupSize;
	int						marchingCubeKernel;
	Vector3Int				marchingCubeKernelGroupSize;

	[HideInInspector]
	public RenderTexture	noiseTexture;

	ComputeBuffer			verticesBuffer;
	ComputeBuffer			trianglesBuffer;

	public void Start ()
	{
		noiseKernel = FindKernel(KernelIds.perlinNoise3DKernel, out noiseKernelGroupSize);
		marchingCubeKernel = FindKernel(KernelIds.marchingCubeKernel, out marchingCubeKernelGroupSize);

		GenerateBuffers();

		// Bind noise kernel parameters:
		terrain3DNoiseShader.SetTexture(noiseKernel, KernelIds.noiseTextureId, noiseTexture);
		terrain3DNoiseShader.SetVector(KernelIds.chunkPositionId, Vector4.zero);

		// Bind marching cubes parameters:
		isoSurfaceShader.SetTexture(marchingCubeKernel, KernelIds.noiseTextureId, noiseTexture);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.verticesId, verticesBuffer);
		isoSurfaceShader.SetBuffer(marchingCubeKernel, KernelIds.trianglesId, trianglesBuffer);
	
		// Bind debug parameters:
		visualize3DNoiseMaterial.SetTexture("_NoiseTex", noiseTexture);

		GenerateTerrain();
	}

	void GenerateBuffers()
	{
		// Create the noise texture
		noiseTexture = new RenderTexture(size, size, size, GraphicsFormat.R16_SFloat);
		noiseTexture.dimension = TextureDimension.Tex3D;
		noiseTexture.enableRandomWrite = true;
		noiseTexture.volumeDepth = size;
		noiseTexture.Create();

		// Create the computeBuffer that will store vertices and indices for draw procedural
		verticesBuffer = new ComputeBuffer(size * size * size * 5, sizeof(float) * 3, ComputeBufferType.Append);
		// the maximum number of triangles is 5 times the number of volxel (with the marching cubes algorithm)
		trianglesBuffer = new ComputeBuffer(size * size * size * 5, sizeof(float) * 3, ComputeBufferType.Append);
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
		Stopwatch sw = new Stopwatch();
		sw.Start();
		
		// 3D noise generation
		terrain3DNoiseShader.Dispatch(noiseKernel, size / noiseKernelGroupSize.x, size / noiseKernelGroupSize.y, size / noiseKernelGroupSize.z);

		// Isosurface generation
		isoSurfaceShader.Dispatch(marchingCubeKernel, size / marchingCubeKernelGroupSize.x, size / marchingCubeKernelGroupSize.y, size / marchingCubeKernelGroupSize.z);
		
		Vector3[] a = new Vector3[size * size * size];
		verticesBuffer.GetData(a);
		
		Debug.Log("a: " + a[2]);

		sw.Stop();
		Debug.Log("3D noise generated in " + sw.Elapsed.TotalMilliseconds + " ms");
	}
	
	void Update ()
	{
		
	}

	private void OnDisable()
	{
		verticesBuffer.Release();
		trianglesBuffer.Release();
		noiseTexture.Release();
	}
}
