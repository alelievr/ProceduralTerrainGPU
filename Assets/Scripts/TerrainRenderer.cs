using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class TerrainRenderer : MonoBehaviour
{
	public Material terrainMaterial;

	[HideInInspector]
	public List<ComputeBuffer> drawArguments = new List<ComputeBuffer>();

	Dictionary<Camera, CommandBuffer> commandBuffers = new Dictionary<Camera, CommandBuffer>();

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
		
		foreach (var args in drawArguments)
			cmd.DrawProceduralIndirect(Matrix4x4.identity, terrainMaterial, -1, MeshTopology.Triangles, args, 0);

		cam.AddCommandBuffer(CameraEvent.AfterSkybox, cmd);
	}
}
