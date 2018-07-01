using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
	TerrainGenerator	terrainGenerator;

	private void OnEnable()
	{
		terrainGenerator = target as TerrainGenerator;
		EditorApplication.update += terrainGenerator.Update;
	}

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		if (GUILayout.Button("Generate noise"))
		{
			terrainGenerator.Start();
		}
	}
}
