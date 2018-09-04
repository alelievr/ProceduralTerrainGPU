Shader "Custom/Terrain"
{
    Properties
    {
		// Be careful, do not change the name here to _Color. It will conflict with the "fake" parameters (see end of properties) required for GI.
        _Color("Color", Color) = (0,1,0,1)
        
		// Blending state
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _CullMode("__cullmode", Float) = 2.0
    }
    
	HLSLINCLUDE
    
	#pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
 
	//-------------------------------------------------------------------------------------
    // Define
    //-------------------------------------------------------------------------------------
     #define UNITY_MATERIAL_UNLIT // Need to be define before including Material.hlsl
    
	//-------------------------------------------------------------------------------------
    // Include
    //-------------------------------------------------------------------------------------

	#include "CoreRP/ShaderLibrary/Common.hlsl"
	#include "HDRP/ShaderVariables.hlsl"
    
	//-------------------------------------------------------------------------------------
    // variable declaration
    //-------------------------------------------------------------------------------------
	
	float4 _Color;
    
    #pragma vertex Vert
    #pragma fragment Frag
    
	ENDHLSL
    
	SubShader
    {
        // This tags allow to use the shader replacement features
        Tags{ "RenderPipeline" = "HDRenderPipeline" "RenderType" = "HDUnlitShader" }

		// Unlit shader always render in forward
        Pass
        {
            Name "Forward Unlit"
            Tags { "LightMode" = "ForwardOnly" }
            
			Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            
			HLSLPROGRAM
 
			#define SHADERPASS SHADERPASS_FORWARD_UNLIT
            #include "HDRP/Material/Material.hlsl"

			StructuredBuffer<half3> vertices;
			StructuredBuffer<half3> normals;
			
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
				
				float3 positionOS = vertices[input.vertexId];
				float3 normalOS = normals[input.vertexId];
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
