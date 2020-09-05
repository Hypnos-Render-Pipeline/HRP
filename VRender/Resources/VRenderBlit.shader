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


			sampler2D _SceneColor;
			sampler2D _CameraDepthNormalsTexture;
			sampler2D _CameraDepthTexture;
			sampler2D _CameraGBufferTexture2;

			float CheckBounds(float2 uv, float d)
			{
				float ob = any(uv < 0) + any(uv > 1);
#if defined(UNITY_REVERSED_Z)
				ob += (d <= 0.00001);
#else
				ob += (d >= 0.99999);
#endif
				return ob * 1e8;
			}

			// Check if the camera is perspective.
			// (returns 1.0 when orthographic)
			inline float CheckPerspective(float x)
			{
				return lerp(x, 1.0, unity_OrthoParams.w);
			}
			inline float IsPerspective() {
				return 1 - unity_OrthoParams.w;
			}

			float SampleDepth(float2 uv)
			{
				float d;
				if (CheckPerspective(0)) {
					d = 1 - tex2D(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv));
				}
				else {
					d = Linear01Depth(tex2D(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv)));
				}
				return d * _ProjectionParams.z + CheckBounds(uv, d);
			}

			float3 SampleNormal(float2 uv)
			{
#if defined(SOURCE_GBUFFER)
				float3 norm = SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, uv).xyz;
				norm = norm * 2 - any(norm); // gets (0,0,0) when norm == 0
				norm = mul((float3x3)unity_WorldToCamera, norm);
#if defined(VALIDATE_NORMALS)
				norm = normalize(norm);
#endif
				return norm;
#else
				float4 cdn = tex2D(_CameraDepthNormalsTexture, uv);
				return DecodeViewNormalStereo(cdn)* float3(1.0, 1.0, -1.0);
#endif
			}



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

			float3 normal(v2f IN) : SV_Target
			{
				float3 n = SampleNormal(IN.uv);
				float3 normal = mul(UNITY_MATRIX_V, float4(normalize(n), 0)).xyz;
				normal.z *= -1;
				//normal = (normal + 1) * 0.5;
				return length(n) > 0 ? normal : float3(0, 0, 1);
			}


			v2f vert1(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.vertex.xy /= 5;
				o.vertex.x += 0.8;
				o.vertex.y += 0;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			v2f vert2(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.vertex.xy /= 5;
				o.vertex.x += 0.8;
				o.vertex.y += 0.4;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			v2f vert3(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.vertex.xy /= 5;
				o.vertex.x += 0.8;
				o.vertex.y += 0.8;
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
				#pragma vertex vert
				#pragma fragment normal
			ENDCG
		}

		Pass
		{
			CGPROGRAM
				#pragma vertex vert1
				#pragma fragment variance
			ENDCG
		}
		
		Pass
		{
			CGPROGRAM
				#pragma vertex vert2
				#pragma fragment frag
			ENDCG
		}

		Pass
		{
			CGPROGRAM
				#pragma vertex vert3
				#pragma fragment frag
			ENDCG
		}
    }
}
