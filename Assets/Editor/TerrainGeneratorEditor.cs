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
			
		if (GUILayout.Button("Check normal borders"))
			terrainGenerator.CheckNormals();
	}

	// void OnSceneGUI()
	// {
	// 	foreach (var kp in terrainGenerator.chunkNormals)
	// 	{
	// 		Vector3 p = Vector3.zero;
	// 		foreach (var n in kp.Value)
	// 		{
	// 			Handles.Label(kp.Key * terrainGenerator.terrainChunkSize + p, n.ToString());
	// 			if (p.x >= terrainGenerator.terrainChunkSize)
	// 			{
	// 				p.x = 0;
	// 				p.y++;
	// 			}
	// 			if (p.y >= terrainGenerator.terrainChunkSize)
	// 			{
	// 				p.y = 0;
	// 				p.z++;
	// 			}
	// 			p.x++;
	// 		}
	// 	}
	// }
}
