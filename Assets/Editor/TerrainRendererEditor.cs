using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainRenderer))]
public class TerrainRendererEditor : Editor
{
	TerrainRenderer	renderer;

	private void OnEnable()
	{
		renderer = target as TerrainRenderer;

		EditorApplication.update += renderer.Update;
	}
}
