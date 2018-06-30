Shader "Unlit/3DNoiseVisualizator"
{
	Properties
	{
		[HideInInspector]
		_NoiseTex ("Noise", 3D) = "white" {}
		_Slice ("Slice", Range(0, 1)) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler3D _NoiseTex;
			float4 _NoiseTex_ST;
			float _Slice;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _NoiseTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex3D(_NoiseTex, float3(i.uv, _Slice));
				return col;
			}
			ENDCG
		}
	}
}
