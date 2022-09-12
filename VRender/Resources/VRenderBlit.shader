Shader "Hidden/VRenderBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

		 CGINCLUDE

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

			sampler2D _MainTex;
			float4 _MainTex_ST;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			float2 _Pixel_WH;
			float4 variance(v2f IN) : SV_Target
			{				
				float4 color = 0;
				float wei_sum = 0;
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						float2 uv = IN.uv + float2(i, j) / _Pixel_WH;
						float4 c = tex2D(_MainTex, uv);
						color += c;
						wei_sum += 1;
					}
				}
				color /= wei_sum;
				return color.a;
			}

			float _YOffset;

			v2f subregion_vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.vertex.xy /= 5;
				o.vertex.x += 0.8;
				o.vertex.y += _YOffset;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			float3 frag(v2f IN) : SV_Target
			{
				return tex2D(_MainTex, IN.uv);
			}

		ENDCG


		Pass
		{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment variance
			ENDCG
		}

		Pass
		{
			CGPROGRAM
				#pragma vertex subregion_vert
				#pragma fragment variance
			ENDCG
		}

		Pass
		{
			CGPROGRAM
				#pragma vertex subregion_vert
				#pragma fragment frag
			ENDCG
		}
    }
}
