using UnityEngine;

public class KernelIds
{
    public static readonly string   perlinNoise3DKernel = "PerlinNoise3D";
    public static readonly string   marchingCubeKernel = "MarchingCube";

    public static readonly int      noiseTextureId = Shader.PropertyToID("_NoiseTexture");
    public static readonly int      debugTextureId = Shader.PropertyToID("_DebugOutput");
    public static readonly int      chunkPositionId = Shader.PropertyToID("chunkPosition");
    public static readonly int      chunkSizeId = Shader.PropertyToID("chunkSize");

    public static readonly int      verticesId = Shader.PropertyToID("vertices");
    public static readonly int      trianglesId = Shader.PropertyToID("triangles");
    public static readonly int      triangleCounterId = Shader.PropertyToID("triangleCounter");
}