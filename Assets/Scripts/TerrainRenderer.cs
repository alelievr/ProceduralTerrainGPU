using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

[ExecuteInEditMode, AddComponentMenu("Rendering/Terrain")]
public class TerrainRenderer : MonoBehaviour
{
	// Use drawProceduralIndirect is broken for now because custom SRPs (HDRP/LWRP) dont have injection points for custom geometry drawing
	#if false

	public Material terrainMaterial;

	List<ChunkRenderParams> chunks = new List<ChunkRenderParams>();
	Dictionary<Camera, CommandBuffer> commandBuffers = new Dictionary<Camera, CommandBuffer>();

	class ChunkRenderParams
	{
		public ComputeBuffer			args;
		public MaterialPropertyBlock	block;

		public ChunkRenderParams(ComputeBuffer args, MaterialPropertyBlock block)
		{
			this.args = args;
			this.block = block;
		}
	}

	void Cleanup()
	{
		foreach (var kp in commandBuffers)
		{
			if (kp.Key != null)
				kp.Key.RemoveCommandBuffer(CameraEvent.AfterSkybox, kp.Value);
		}
		commandBuffers.Clear();
	}

	private void OnEnable()
	{
		Cleanup();
	}

	private void OnDisable()
	{
		Cleanup();
	}

	public void AddChunkToRender(ComputeBuffer args, ComputeBuffer vertices, ComputeBuffer normals)
	{
		MaterialPropertyBlock block = new MaterialPropertyBlock();

		block.SetBuffer(ShaderIds.vertices, vertices);
		block.SetBuffer(ShaderIds.normals, normals);

		chunks.Add(new ChunkRenderParams(args, block));
	}

	public void ClearChunks()
	{
		chunks.Clear();
	}

	private void OnWillRenderObject()
	{
		var act = gameObject.activeInHierarchy && enabled;
		if (!act)
		{
			Cleanup();
			return;
		}

		var cam = Camera.current;
		if (!cam)
			return;

		CommandBuffer cmd;

		if (!commandBuffers.TryGetValue(cam, out cmd))
		{
			cmd = commandBuffers[cam] = new CommandBuffer();
			cmd.name = "Terrain Renderer";
		}

		cmd.Clear();

		foreach (var chunk in chunks)
		{
			Debug.Log("draw buffer !");
			cmd.DrawProceduralIndirect(Matrix4x4.identity, terrainMaterial, -1, MeshTopology.Triangles, chunk.args, 0, chunk.block);
		}

		cam.AddCommandBuffer(CameraEvent.BeforeLighting, cmd);
	}

	#else

	public Transform			parent;
	public GameObject			chunkPrefab;

	class MeshReadbackRequest
	{
		public AsyncGPUReadbackRequest	verticesRequest;
		public AsyncGPUReadbackRequest	normalsRequest;
		public Vector3					worldPosition;
		public int						verticesCount;

		public bool IsDone()
		{
			return verticesRequest.done && normalsRequest.done;
		}

		public void Update()
		{
			verticesRequest.Update();
			normalsRequest.Update();
		}
	}

	List<MeshReadbackRequest>	readBackMeshesRequests = new List<MeshReadbackRequest>();
	List<GameObject>			chunks = new List<GameObject>();

	public void AddChunkToRender(ComputeBuffer vertices, ComputeBuffer normals, Vector3 worldPosition, int verticesCount)
	{
		MeshReadbackRequest req = new MeshReadbackRequest
		{
			verticesRequest = AsyncGPUReadback.Request(vertices),
			normalsRequest = AsyncGPUReadback.Request(normals),
			worldPosition = worldPosition,
			verticesCount = verticesCount,
		};

		readBackMeshesRequests.Add(req);
	}

	public void ClearChunks()
	{
		foreach (Transform t in parent)
			DestroyImmediate(t.gameObject);
	}

	public void Update()
	{
		for (int i = 0; i < readBackMeshesRequests.Count; i++)
		{
			var req = readBackMeshesRequests[i];

			if (req.IsDone())
			{
				GenerateMesh(req);
				readBackMeshesRequests.Remove(req);
				i--;
			}

			req.Update();
		}
	}

	void GenerateMesh(MeshReadbackRequest req)
	{
		var chunk = Instantiate(chunkPrefab, req.worldPosition, Quaternion.identity, parent);
		MeshFilter mf = chunk.GetComponent<MeshFilter>();
		Vector3[] vertices = req.verticesRequest.GetData<Vector3>().Take(req.verticesCount).ToArray();
		var mesh = new Mesh
		{
			vertices = vertices,
			normals = req.normalsRequest.GetData<Vector3>().Take(req.verticesCount).ToArray(),
			// Generate a dummy triangle indicies buffer because mesh
			triangles = Enumerable.Range(0, req.verticesCount).ToArray(),
		};

		mf.sharedMesh = mesh;
	}

	#endif
}
