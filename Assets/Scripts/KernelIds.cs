using UnityEngine;

public class KernelIds
{
    // Kernel names
    public static readonly string   perlinNoise3DKernel = "PerlinNoise3D";
    public static readonly string   marchingCubeKernel = "MarchingCubes";
    public static readonly string   computeNormalKernel = "ComputeNormals";
    public static readonly string   copyMeshBuffers = "CopyMeshBuffers";

    // IsoSurface properties
    public static readonly int      noiseTexture = Shader.PropertyToID("_NoiseTexture");
    public static readonly int      debugTexture = Shader.PropertyToID("_DebugOutput");
    public static readonly int      normalTexture = Shader.PropertyToID("_NormalTexture");
    public static readonly int      chunkPosition = Shader.PropertyToID("chunkPosition");
    public static readonly int      chunkSize = Shader.PropertyToID("chunkSize");
    public static readonly int      worldPosition = Shader.PropertyToID("worldPosition");
    public static readonly int      debugPoints = Shader.PropertyToID("debugPoints");
    public static readonly int      resolutionPerVoxel = Shader.PropertyToID("resolutionPerVoxel");

    // Mesh buffers
    public static readonly int      verticesId = Shader.PropertyToID("vertices");
    public static readonly int      trianglesId = Shader.PropertyToID("triangles");
    public static readonly int      normalsId = Shader.PropertyToID("normals");

    // Noise parameters
    public static readonly int      seed = Shader.PropertyToID("seed");
    public static readonly int      lacunarity = Shader.PropertyToID("lacunarity");
    public static readonly int      octaves = Shader.PropertyToID("octaves");
    public static readonly int      scale = Shader.PropertyToID("scale");
    public static readonly int      frequency = Shader.PropertyToID("frequency");
    public static readonly int      gain = Shader.PropertyToID("gain");

    // Copy buffers parameters 
    public static readonly int      generatedVertices = Shader.PropertyToID("generatedVertices");
    public static readonly int      generatedNormals = Shader.PropertyToID("generatedNormals");
    public static readonly int      meshVertices = Shader.PropertyToID("meshVertices");
    public static readonly int      meshNormals = Shader.PropertyToID("meshNormals");
}

public class ShaderIds
{
    public static readonly int      vertices = Shader.PropertyToID("vertices");
    public static readonly int      normals = Shader.PropertyToID("normals");
}