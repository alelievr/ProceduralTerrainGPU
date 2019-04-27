using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

[ExecuteInEditMode, AddComponentMenu("Rendering/DrawProcedural")]
public class DrawProceduralRenderer : MonoBehaviour
{
	public Material terrainMaterial;

	List<ChunkRenderParams> chunks = new List<ChunkRenderParams>();

	class ChunkRenderParams
	{
		public ComputeBuffer			args;
		public MaterialPropertyBlock	properties;
		public Bounds					bounds;

		public ChunkRenderParams(ComputeBuffer args, MaterialPropertyBlock block, Bounds bounds)
		{
			this.args = args;
			this.properties = block;
			this.bounds = bounds;
		}
	}

	public void AddChunkToRender(ComputeBuffer args, MaterialPropertyBlock properties, Bounds bounds)
	{
		chunks.Add(new ChunkRenderParams(args, properties, bounds));
	}

	public void ClearChunks()
	{
		chunks.Clear();
	}

	public void Update()
	{
		foreach (var chunk in chunks)
		{
			Debug.Log("Draw chunk !");
			Graphics.DrawProceduralIndirect(terrainMaterial, chunk.bounds, MeshTopology.Triangles, chunk.args, properties: chunk.properties);
		}
	}
}
