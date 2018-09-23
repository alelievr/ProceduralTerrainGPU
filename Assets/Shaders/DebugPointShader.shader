Shader "Unlit/DebugPointShader"
{
	Properties
	{
        _Color("Color", Color) = (0,1,0,1)
		_MainTex ("Texture", 2D) = "white" {}
	}
	
	HLSLINCLUDE

    #pragma vertex Vert
    #pragma fragment Frag
	
	float4 _Color;

	ENDHLSL

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

        Pass
        {
            Name "Forward Unlit"
            Tags { "LightMode" = "ForwardOnly" }
            
			Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            
			HLSLPROGRAM
 
			#define SHADERPASS SHADERPASS_FORWARD_UNLIT
			#include "TerrainGenerator.cs.hlsl"
			#include "CoreRP/ShaderLibrary/Common.hlsl"
			#include "HDRP/ShaderVariables.hlsl"
            #include "HDRP/Material/Material.hlsl"

			StructuredBuffer<DebugPoint>	debugPoints;
			
			struct VertInput
			{
				uint vertexId		: SV_VertexID;
			};

			struct VertToFrag
			{
				float4 positionCS : SV_Position;
				float3 normalWS : TEXCOORD1;
			};
	
			VertToFrag Vert(VertInput input)
			{
				VertToFrag output;
				
				float3 positionOS = debugPoints[input.vertexId].position.xyz;
				float3 normalOS = debugPoints[input.vertexId].direction.xyz;
				float3 positionRWS = TransformObjectToWorld(positionOS);
				output.positionCS = TransformWorldToHClip(positionRWS);
				output.normalWS = TransformObjectToWorldNormal(normalOS);

				return output;
			}
 			
			float4 Frag(VertToFrag input, float facing : VFACE) : SV_Target
			{
				float3 normal = input.normalWS;
				return float4(_Color.rgb  * max(0.1, dot(float3(0.5, facing, 0), normal)), 1);
			}

			ENDHLSL
        }
	}
}
