using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
	TerrainGenerator	terrainGenerator;
	IEnumerator			terrainStepEnumerator;

	private void OnEnable()
	{
		terrainGenerator = target as TerrainGenerator;
		terrainStepEnumerator = terrainGenerator.GenerateStep().GetEnumerator();
	}

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		if (GUILayout.Button("Generate noise"))
		{
			terrainGenerator.Start();
		}

		if (GUILayout.Button("Generate terrain step"))
			if (!terrainStepEnumerator.MoveNext())
				terrainStepEnumerator = terrainGenerator.GenerateStep().GetEnumerator();
	}
}
