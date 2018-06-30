using UnityEngine;

public class KernelIds
{
    public static readonly string   perlinNoise3DKernel = "PerlinNoise3D";
    public static readonly string   marchingCubeKernel = "MarchingCube";

    public static readonly int      noiseTextureId = Shader.PropertyToID("noiseTexture");
    public static readonly int      chunkPositionId = Shader.PropertyToID("chunkPosition");

    public static readonly int      verticesId = Shader.PropertyToID("vertices");
    public static readonly int      trianglesId = Shader.PropertyToID("triangles");
}