using UnityEngine;

public class KernelIds
{
    // Kernel names
    public static readonly string   perlinNoise3DKernel = "PerlinNoise3D";
    public static readonly string   marchingCubeKernel = "MarchingCubes";
    public static readonly string   computeNormalKernel = "ComputeNormals";
    public static readonly string   copyMeshBuffers = "CopyMeshBuffers";

    // IsoSurface properties
    public static readonly int      noiseTextureId = Shader.PropertyToID("_NoiseTexture");
    public static readonly int      debugTextureId = Shader.PropertyToID("_DebugOutput");
    public static readonly int      normalTextureId = Shader.PropertyToID("_NormalTexture");
    public static readonly int      chunkPosition = Shader.PropertyToID("chunkPosition");
    public static readonly int      chunkSizeId = Shader.PropertyToID("chunkSize");

    // Mesh buffers
    public static readonly int      verticesId = Shader.PropertyToID("vertices");
    public static readonly int      trianglesId = Shader.PropertyToID("triangles");
    public static readonly int      normalsId = Shader.PropertyToID("normals");

    // Noise parameters
    public static readonly int      seed = Shader.PropertyToID("seed");
    public static readonly int      lacunarity = Shader.PropertyToID("lacunarity");
    public static readonly int      octaves = Shader.PropertyToID("octaves");
    public static readonly int      scale = Shader.PropertyToID("scale");

    // Copy buffers parameters 
    public static readonly int      generatedVertices = Shader.PropertyToID("generatedVertices");
    public static readonly int      generatedNormals = Shader.PropertyToID("generatedNormals");
    public static readonly int      meshVertices = Shader.PropertyToID("meshVertices");
    public static readonly int      meshNormals = Shader.PropertyToID("meshNormals");
}