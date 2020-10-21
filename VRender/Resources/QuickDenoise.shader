Shader "Hidden/QuickDenoise"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
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

			SamplerState point_clamp_sampler;
			Texture2D _MainTex; SamplerState sampler_MainTex;
			int2 _Pixel_WH;
			float _DenoiseStrength;
			float _Flare;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = o.vertex.xy;
				o.uv = o.uv / 2 + 0.5;
				o.uv.y = 1 - o.uv.y;
				return o;
			}

			float Luminance(float3 col) {
				return col.r* 0.299 + col.g * 0.587 + col.b * 0.114;
			}


			float4 smartDeNoise(float2 uv, float threshold = 0.1, float sigma = 2, float kSigma = 2)
			{
				#define INV_SQRT_OF_2PI 0.39894228040143267793994605993439  // 1.0/SQRT_OF_2PI
				#define INV_PI 0.31830988618379067153776752674503
				float radius = round(kSigma * sigma);
				float radQ = radius * radius;

				float invSigmaQx2 = .5 / (sigma * sigma);      // 1.0 / (sigma^2 * 2.0)
				float invSigmaQx2PI = INV_PI * invSigmaQx2;    // 1.0 / (sqrt(PI) * sigma)

				float invThresholdSqx2 = .5 / (threshold * threshold);     // 1.0 / (sigma^2 * 2.0)
				float invThresholdSqrt2PI = INV_SQRT_OF_2PI / threshold;   // 1.0 / (sqrt(2*PI) * sigma)

				float4 centrPx = _MainTex.SampleLevel(sampler_MainTex, uv, 0);

				int2 size = _Pixel_WH;

				float zBuff = 0;
				float4 aBuff = 0;

				for (float x = -radius; x <= radius; x++) {
					float pt = sqrt(radQ - x * x);  // pt = yRadius: have circular trend
					for (float y = -pt; y <= pt; y++) {
						float2 d = float2(x, y) / size;

						float blurFactor = exp(-dot(d, d) * invSigmaQx2) * invSigmaQx2;

						float4 walkPx = _MainTex.SampleLevel(sampler_MainTex, uv + d, 0);

						float4 dC = walkPx - centrPx;
						float deltaFactor = exp(-dot(dC, dC) * invThresholdSqx2) * invThresholdSqrt2PI * blurFactor;

						zBuff += deltaFactor;
						aBuff += deltaFactor * walkPx;
					}
				}
				return aBuff / zBuff;
			}
					   
			float4x4 _V_Inv, _P_Inv;
			
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
				return tex2D(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv));
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

			float3 WPos(float2 uv) {
				float depth = SampleDepth(uv);
				float4 wpos = mul(_V_Inv, mul(_P_Inv, float4(uv * 2 - 1, depth, 1)));
				wpos /= wpos.w;
				return wpos.xyz;
			}


			float3 Upsample(float2 uv)
			{
				float2 pixel = floor(uv * _Pixel_WH * 2); 
				float2 pixel_in_low = floor(pixel / 2) + 0.5;
				float3 pixel_in_block = float3(pixel - 2 * pixel_in_low + 1, 0);
				pixel_in_block.xy = pixel_in_block.xy * 2 - 1;
				
				float3 cn = SampleNormal(uv);
				float3 cpos = WPos(uv);
				
				float2 uvs[4] = {(pixel_in_low + pixel_in_block.zz) / _Pixel_WH, 
									(pixel_in_low + pixel_in_block.xz) / _Pixel_WH,
									(pixel_in_low + pixel_in_block.zy) / _Pixel_WH, 
									(pixel_in_low + pixel_in_block.xy) / _Pixel_WH};
				
				float4 res = 0;
				for (int j = 0; j < 4; j++)
				{
					float3 color = _MainTex.SampleLevel(sampler_MainTex, uvs[j], 0).rgb;
				
					float2 pixel_dis = abs(uvs[j] - uv) * _Pixel_WH;
					float3 n = SampleNormal(uvs[j]);
					float3 pos = WPos(uvs[j]);

					float w_dis = (1 - pixel_dis.y) * (1 - pixel_dis.x);
					float w_n = 0.2 + (dot(cn, n) + 1) / 2;
					float w_d = max(0.05, 1 - 10 * distance(cpos, pos));

	
					float w = w_dis * w_n * w_d;
					res.rgb += w * color;
					res.a += w;
				}

				return res.rgb / res.a;
			}

			float3 RemoveFlare(float2 uv) {
				int3 offset = int3(1, -1, 0);

				float3 c = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
				float3 l = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.yz);
				float3 r = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.xz);
				float3 u = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.zx);
				float3 d = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.zy);
				float3 lu = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.yx);
				float3 ld = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.yy);
				float3 ru = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.xx);
				float3 rd = _MainTex.SampleLevel(sampler_MainTex, uv, 0, offset.xy);

				float avl = Luminance(l) + Luminance(r) + Luminance(u) + Luminance(d) + Luminance(lu) + Luminance(ld) + Luminance(ru) + Luminance(rd);
				avl /= 8;

				if (Luminance(c) > avl * _Flare) {
					return (l + r + u + d) / 4;
				}

				return c;
			}

		ENDCG


		Pass
		{	
			Ztest off
			ZWrite off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			fixed4 frag(v2f i) : SV_Target
			{
				return smartDeNoise(i.uv,_DenoiseStrength);
			}
			ENDCG
		}
		Pass
		{	
			Ztest off
			ZWrite off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float3 frag(v2f i) : SV_Target
			{
				return Upsample(i.uv);
			}
			ENDCG
		}
		Pass
		{	
			Ztest off
			ZWrite off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float3 frag(v2f i) : SV_Target
			{
				return RemoveFlare(i.uv);
			}
			ENDCG
		}
	}
}
