using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static class TerrainGeneratorShaderIncludePath
{
	[UnityEditor.ShaderIncludePath]
	public static string[] GetPaths()
	{
		return new string[] {
			"Assets/Scripts/"
		};
	}
}