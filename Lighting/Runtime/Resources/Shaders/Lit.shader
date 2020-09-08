Shader "HRP/Lit"
{
    Properties
    {
		_MainTex("Albedo", 2D) = "white" {}
		_Cutoff("Alpha Cutoff",Range(0,1)) = 0
		_Color("Color", Color) = (1,1,1,1)

		[Toggle]_AutoDesk("Auto desk", int) = 0

		_MetallicGlossMap("Metallic Smoothness", 2D) = "white" {}
		[Gamma] _Metallic("Metallic", Range(0,1)) = 0.0

		_AOMap("AO （R)", 2D) = "white" {}
		_AOScale("AO", Range(0,1)) = 1

		_Smoothness("Smoothness", Range(0,1)) = 0.5
		_GlossMapScale("SmoothnessMapScale", Range(0,1)) = 1.0

		_BumpMap("Normal Map", 2D) = "bump" {}
		_BumpScale("Scale", Range(-10,10)) = 1.0

		_EmissionMap("EmissionMap", 2D) = "white" {}
		[HDR]_EmissionColor("EmissionColor", Color) = (0,0,0)

		_Index("IOR", Range(1, 4)) = 1.45

		[Toggle]_Subsurface("SubSurface", int) = 0
		_Ld("Average Scatter Distance", Range(0, 1)) = 0.1
		_ScatterProfile("Scatter Profile", 2D) = "white" {}

		_ClearCoat("Clear Coat", Range(0,1)) = 0

		_Sheen("Sheen", Range(0,1)) = 0

		_Modified("Modified", int) = 0

		[HideInInspector] _Mode("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    // Rasterization Shader
    SubShader
    {
		Tags { "Queue" = "Geometry" }
		Pass
		{
			ColorMask 0
			Tags { "LightMode" = "PreZ" }
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
			void frag() {}
			ENDCG
		}
		Pass
		{
			Tags { "LightMode" = "GBuffer_Equal" }

			ZWrite off
			ZTest Equal

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "./Includes/GBuffer.hlsl"

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP _
			#pragma shader_feature _AOMAP _


			CBUFFER_START(UnityPerMaterial)
				float4			_Color;
				sampler2D 		_MainTex;
				float4			_MainTex_ST;
			#if _NORMALMAP
				sampler2D		_BumpMap;
				half			_BumpScale;
			#endif // _NORMALMAP
			#if _METALLICGLOSSMAP
				sampler2D		_MetallicGlossMap;
				float			_GlossMapScale;
			#else
				half			_Metallic;
				float			_Smoothness;
			#endif // _METALLICGLOSSMAP
			#if _EMISSION
				float4			_EmissionColor;
				sampler2D		_EmissionMap;
			#endif // _EMISSION
			#if _AOMAP
				sampler2D		_AOMap;
				float			_AOScale;
			#endif // _AOMAP
			CBUFFER_END

			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
			};

			v2f vert(a2v i) {
				v2f o;
				o.vertex = UnityObjectToClipPos(i.vertex);
				o.normal = UnityObjectToWorldNormal(i.normal);
				o.tangent = float4(UnityObjectToWorldDir(i.tangent.xyz), i.tangent.w);
				o.uv = TRANSFORM_TEX(i.uv, _MainTex);
				return o;
			}
						
			half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
			{
				#if defined(UNITY_NO_DXT5nm)
					return packednormal.xyz * 2 - 1;
				#else
					half3 normal;
					normal.xy = (packednormal.wy * 2 - 1);
					normal.xy *= bumpScale;
					normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
					return normal;
				#endif
			}	

			void frag(v2f i,out fixed4 target0 : SV_Target0, out fixed4 target1 : SV_Target1, out fixed4 target2 : SV_Target2, out fixed4 target3 : SV_Target3) {

				fixed3 baseColor = _Color * tex2D(_MainTex, i.uv).rgb;

				#if _METALLICGLOSSMAP
					float4 m_s = tex2D(_MetallicGlossMap, i.uv);
					fixed metallic = m_s.r;
					fixed smoothness = m_s.a * _GlossMapScale;
				#else
					fixed metallic = _Metallic;
					fixed smoothness = _Smoothness;
				#endif //_METALLICGLOSSMAP

				#if _NORMALMAP
					float3 normal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
					float3 n = normalize(i.normal), t = normalize(i.tangent.xyz);
					float3 binormal = cross(n, t) * i.tangent.w;
					float3x3 rotation = float3x3(t, binormal, n);
					normal = mul(normal, rotation);
				#else
					float3 normal = normalize(i.normal);
				#endif // _NORMALMAP
				#if _EMISSION
					float3 emission = _EmissionColor * tex2D(_EmissionMap, i.uv);
				#else
					float3 emission = 0;
				#endif // _EMISSION
				#if _AOMAP
					float ao = 1 - (_AOScale * (1 - tex2D(_AOMap, i.uv).r));
				#else
					float ao = 1;
				#endif // _AOMAP

				Encode2GBuffer(baseColor, 1 - smoothness, metallic, normal, emission, i.normal, ao, target0, target1, target2, target3);
			}

			ENDCG
		}
		Pass
		{
			Tags { "LightMode" = "GBuffer_LEqual" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "./Includes/GBuffer.hlsl"

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP _
			#pragma shader_feature _AOMAP _


			CBUFFER_START(UnityPerMaterial)
				float4			_Color;
				sampler2D 		_MainTex;
				float4			_MainTex_ST;
			#if _NORMALMAP
				sampler2D		_BumpMap;
				half			_BumpScale;
			#endif // _NORMALMAP
			#if _METALLICGLOSSMAP
				sampler2D		_MetallicGlossMap;
				float			_GlossMapScale;
			#else
				half			_Metallic;
				float			_Smoothness;
			#endif // _METALLICGLOSSMAP
			#if _EMISSION
				float4			_EmissionColor;
				sampler2D		_EmissionMap;
			#endif // _EMISSION
			#if _AOMAP
				sampler2D		_AOMap;
				float			_AOScale;
			#endif // _AOMAP
			CBUFFER_END

			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
			};

			v2f vert(a2v i) {
				v2f o;
				o.vertex = UnityObjectToClipPos(i.vertex);
				o.normal = UnityObjectToWorldNormal(i.normal);
				o.tangent = float4(UnityObjectToWorldDir(i.tangent.xyz), i.tangent.w);
				o.uv = TRANSFORM_TEX(i.uv, _MainTex);
				return o;
			}

			half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
			{
				#if defined(UNITY_NO_DXT5nm)
					return packednormal.xyz * 2 - 1;
				#else
					half3 normal;
					normal.xy = (packednormal.wy * 2 - 1);
					normal.xy *= bumpScale;
					normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
					return normal;
				#endif
			}

			void frag(v2f i,out fixed4 target0 : SV_Target0, out fixed4 target1 : SV_Target1, out fixed4 target2 : SV_Target2, out fixed4 target3 : SV_Target3) {

				fixed3 baseColor = _Color * tex2D(_MainTex, i.uv).rgb;

				#if _METALLICGLOSSMAP
					float4 m_s = tex2D(_MetallicGlossMap, i.uv);
					fixed metallic = m_s.r;
					fixed smoothness = m_s.a * _GlossMapScale;
				#else
					fixed metallic = _Metallic;
					fixed smoothness = _Smoothness;
				#endif //_METALLICGLOSSMAP

				#if _NORMALMAP
					float3 normal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
					float3 n = normalize(i.normal), t = normalize(i.tangent.xyz);
					float3 binormal = cross(n, t) * i.tangent.w;
					float3x3 rotation = float3x3(t, binormal, n);
					normal = mul(normal, rotation);
				#else
					float3 normal = normalize(i.normal);
				#endif // _NORMALMAP
				#if _EMISSION
					float3 emission = _EmissionColor * tex2D(_EmissionMap, i.uv);
				#else
					float3 emission = 0;
				#endif // _EMISSION
				#if _AOMAP
					float ao = 1 - (_AOScale * (1 - tex2D(_AOMap, i.uv).r));
				#else
					float ao = 1;
				#endif // _AOMAP

				Encode2GBuffer(baseColor, 1 - smoothness, metallic, normal, emission, i.normal, ao, target0, target1, target2, target3);
			}

			ENDCG
		}

		Pass
		{
			Tags { "LightMode" = "Transparent" }

			Blend One SrcAlpha
			ZTest on
			ZWrite off
			Cull back

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "./Includes/Light.hlsl"
			#include "./Includes/LTCLight.hlsl"
			#include "./Includes/PBS.hlsl"

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP _
			#pragma shader_feature _AOMAP _

			CBUFFER_START(UnityPerMaterial)
				float4			_Color;
				sampler2D 		_MainTex;
				float4			_MainTex_ST;
			#if _NORMALMAP
				sampler2D		_BumpMap;
				half			_BumpScale;
			#endif // _NORMALMAP
			#if _METALLICGLOSSMAP
				sampler2D		_MetallicGlossMap;
				float			_GlossMapScale;
			#else
				half			_Metallic;
				float			_Smoothness;
			#endif // _METALLICGLOSSMAP
			#if _EMISSION
				float4			_EmissionColor;
				sampler2D		_EmissionMap;
			#endif // _EMISSION
			#if _AOMAP
				sampler2D		_AOMap;
				float			_AOScale;
			#endif // _AOMAP
				float _Index;
			CBUFFER_END

			sampler2D _ScreenColor;
			float4x4 _V_Inv, _VP_Inv;
			float4 _ScreenParameters;

			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
				float4 wpos : TEXCOORD1;
				float4 spos : TEXCOORD2;
			};

			v2f vert(a2v i) {
				v2f o;
				o.wpos = mul(unity_ObjectToWorld, i.vertex);
				o.vertex = UnityObjectToClipPos(i.vertex);
				o.normal = UnityObjectToWorldNormal(i.normal);
				o.tangent = float4(UnityObjectToWorldDir(i.tangent.xyz), i.tangent.w);
				o.uv = TRANSFORM_TEX(i.uv, _MainTex);
				o.spos = ComputeScreenPos(o.vertex);
				return o;
			}

			half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
			{
				#if defined(UNITY_NO_DXT5nm)
					return packednormal.xyz * 2 - 1;
				#else
					half3 normal;
					normal.xy = (packednormal.wy * 2 - 1);
					normal.xy *= bumpScale;
					normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
					return normal;
				#endif
			}

			float4 frag(v2f i) : SV_Target {

				SurfaceInfo info = (SurfaceInfo)0;

				fixed4 baseColor = _Color * tex2D(_MainTex, i.uv);
				info.baseColor = baseColor.rgb;
				info.transparent = 1 - baseColor.a;

				#if _METALLICGLOSSMAP
					float4 m_s = tex2D(_MetallicGlossMap, i.uv);
					info.metallic = m_s.r;
					info.smoothness = m_s.a * _GlossMapScale;
				#else
					info.metallic = _Metallic;
					info.smoothness = _Smoothness;
				#endif //_METALLICGLOSSMAP

				#if _NORMALMAP
					info.normal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
					float3 n = normalize(i.normal), t = normalize(i.tangent.xyz);
					float3 binormal = cross(n, t) * i.tangent.w;
					float3x3 rotation = float3x3(t, binormal, n);
					info.normal = mul(info.normal, rotation);
				#else
					info.normal = normalize(i.normal);
				#endif // _NORMALMAP
				#if _EMISSION
					info.emission = _EmissionColor * tex2D(_EmissionMap, i.uv);
				#else
					info.emission = 0;
				#endif // _EMISSION
				#if _AOMAP
					info.diffuseAO_specAO = (1 - (_AOScale * (1 - tex2D(_AOMap, i.uv).r))).xx;
				#else
					info.diffuseAO_specAO = (1).xx;
				#endif // _AOMAP
				
				info.gnormal = i.normal;

                float3 pos = i.wpos;
				float3 camPos = _V_Inv._m03_m13_m23;
                float3 view = normalize(camPos - pos);
				float2 screenUV = i.spos.xy / i.spos.w;

                float3 res = 0;

                BegineLocalLightsLoop(screenUV, pos, _VP_Inv);
                {
                    res += PBS(PBS_FULLY, info, light.dir, light.radiance, view);
                }
                EndLocalLightsLoop;

				for (int i = 0; i < _AreaLightCount; i++)
				{
					Light areaLight = _AreaLightBuffer[i];

					float3 lightZ = areaLight.mainDirection_id.xyz;
					float xz = sqrt(1 - areaLight.geometry.z * areaLight.geometry.z);
					float3 lightX = float3(xz * cos(areaLight.geometry.w), areaLight.geometry.z, xz * sin(areaLight.geometry.w));
					float3 lightY = cross(lightZ, lightX);

					if (areaLight.radiance_type.w == TUBE) {
						res += TubeLight(info, areaLight.radiance_type.xyz,
											areaLight.position_range.xyz,
											float4(lightZ, areaLight.geometry.x * 2),
											float4(lightX, areaLight.geometry.y * 2),
											pos, view);
					}
					else if (areaLight.radiance_type.w == QUAD) {
						res += QuadLight(info, areaLight.radiance_type.xyz,
											areaLight.position_range.xyz,
											float4(-lightX, areaLight.geometry.x),
											float4(lightY, areaLight.geometry.y),
											pos, view);
					}
					else if (areaLight.radiance_type.w == DISC) {
						res += DiscLight(info, areaLight.radiance_type.xyz,
											areaLight.position_range.xyz,
											float4(-lightX, areaLight.geometry.x * 2),
											float4(lightY, areaLight.geometry.x * 2),
											pos, view);
					}
				}

				res += info.emission;

				if (_Index != 1) {
					float3 offset = refract(-view, info.normal, 1 / _Index);
					float4 p = mul(UNITY_MATRIX_VP, float4(pos + offset, 1));
					p.xy /= p.w;
					p.xy = p.xy / 2 + 0.5;
					p.y = 1 - p.y;
					return float4(res + tex2Dlod(_ScreenColor, float4(p.xy, 0, lerp(8, 0, info.smoothness))) * info.transparent, 0);
				}
				else
					return float4(res, info.transparent);
			}

			ENDCG
		}
    }

	// Ray Tracing Shader
	SubShader
	{
		// VRender pass
		Pass
		{
			Name "RT"
			Tags{ "LightMode" = "RT" }

			CGPROGRAM

			#pragma raytracing test


			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP _
			#pragma shader_feature _AOMAP _

			//If not define Shading, then use LitShading
			//or un-comment next line to use custom shading function
			//#define Shading Lambert

			#include "./Includes/RT/Include/RTLitInclude.hlsl" 
		 

			//----------------------------------------------------------------------------------------
			//------- Material data input ------------------------------------------------------------
			//----------------------------------------------------------------------------------------
			float4       _Color;
			Texture2D 	_MainTex; SamplerState sampler_MainTex;
			float4      _MainTex_ST;
			float _Cutoff;

			#if _NORMALMAP

			Texture2D   _BumpMap; SamplerState sampler_BumpMap;
			half        _BumpScale;

			#endif // _NORMALMAP

			#if _METALLICGLOSSMAP

			Texture2D   _MetallicGlossMap;
			float       _GlossMapScale;

			#else

			half        _Metallic;
			float       _Smoothness;

			#endif // _METALLICGLOSSMAP

			#if _EMISSION

			float4      _EmissionColor;
			Texture2D   _EmissionMap;

			#endif // _EMISSION
			#if _AOMAP
			Texture2D	_AOMap;
			float		_AOScale;
			#endif // _AOMAP

			float _Index;

			float _ClearCoat;
			float _Sheen;

			float _MipScale;

			//struct SurfaceInfo {
			//	float3	baseColor;
			//	float	transparent;
			//	float	metallic;
			//	float	smoothness;
			//	float3	normal;
			//	float3	emission;
			//	float	clearCoat;
			//	float	sheen;
			//	float	index;
			//	float	Ld;
			//	bool	discarded;
			//};
			SurfaceInfo GetSurfaceInfo(inout FragInputs i) {
				SurfaceInfo IN;

				float2 uv = i.uv0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float4 baseColor = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
				IN.baseColor = _Color * baseColor.xyz;

				IN.transparent = 1 - baseColor.a * _Color.a;
								
				#if _METALLICGLOSSMAP
					float4 m_s = SampleTex(_MetallicGlossMap, uv, 0);
					IN.metallic = m_s.r;
					IN.smoothness = m_s.a * _GlossMapScale;
				#else
					IN.metallic = _Metallic;
					IN.smoothness = _Smoothness;
				#endif

				#if _NORMALMAP
					half3 normal = UnpackScaleNormal(_BumpMap.SampleLevel(sampler_BumpMap, uv, 0), _BumpScale);
					IN.normal = normalize(mul(normal * float3(-1,-1,1), i.tangentToWorld));
					IN.normal *= i.isFrontFace ? 1 : -1;
					i.tangentToWorld[2] = IN.normal;
					i.tangentToWorld[0] = cross(i.tangentToWorld[1], i.tangentToWorld[2]);
					i.tangentToWorld[1] = cross(i.tangentToWorld[2], i.tangentToWorld[0]);
				#else
					IN.normal = i.tangentToWorld[2];
					IN.normal *= i.isFrontFace ? 1 : -1;
					i.tangentToWorld[2] = IN.normal;
					i.tangentToWorld[0] = cross(i.tangentToWorld[1], i.tangentToWorld[2]);
					i.tangentToWorld[1] = cross(i.tangentToWorld[2], i.tangentToWorld[0]);
				#endif // _NORMALMAP

					IN.clearCoat = _ClearCoat * 0.25;
					IN.sheen = _Sheen * 2;

				#if _EMISSION
					IN.emission = _EmissionColor * SampleTex(_EmissionMap, uv, 0);
				#else
					IN.emission = 0;
				#endif

				#if _AOMAP
					IN.diffuseAO_specAO = 1 - (_AOScale * (1 - SampleTex(_AOMap, uv, 0).rr));
				#else
					IN.diffuseAO_specAO = (1).xx;
				#endif // _AOMAP

				IN.index = _Index;

				#if _SUBSURFACE
					IN.Ld = _Ld;
				#else
					IN.Ld = 0;
				#endif


 1;

				IN.discarded = baseColor.a < _Cutoff;

				return IN;
			}

			//----------------------------------------------------------------------------------------
			//------- Custom shading function --------------------------------------------------------
			//----------------------------------------------------------------------------------------
			void Lambert(FragInputs IN, const float3 viewDir,
				inout int4 sampleState, inout float4 weight, inout float3 position, inout float rayRoughness,
				out float3 directColor, out float3 nextDir, out float3 gN) {

				SurfaceInfo surface = GetSurfaceInfo(IN);

				int light_count = clamp(_LightCount, 0, 100);

				float2 rnd = float2(SAMPLE, SAMPLE);

				int picked_light = floor(min(rnd.x, 0.999) * light_count);

				Light light = _LightList[picked_light];

				float attenuation; float3 lightDir; float3 end_point;
				bool in_light_range = ResolveLight(light, position,
					/*inout*/sampleState,
					/*out*/attenuation, /*out*/lightDir, /*out*/end_point);

				float3 luminance = attenuation * light.color;

				float NdotL = saturate(dot(lightDir, surface.normal));

				float3 direct_light_without_shadow = NdotL * luminance;

				float3 shadow = TraceShadow(IN.position, end_point,
					/*inout*/sampleState);

				directColor = surface.baseColor * shadow * direct_light_without_shadow * light_count;

				nextDir = CosineSampleHemisphere(float2(SAMPLE, SAMPLE), surface.normal);
				weight.xyz = surface.baseColor / PI;
				gN = IN.gN;
			}

			//----------------------------------------------------------------------------------------
			//------- DXR Shader functions - don't change them unless you know what you are doing ----
			//----------------------------------------------------------------------------------------
			[shader("closesthit")]
			void ClosestHit(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes) {
				LitClosestHit(/*inout*/rayIntersection, attributeData);
			}

			[shader("anyhit")]
			void AnyHit(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
			{
				LitAnyHit(/*inout*/rayIntersection, attributeData);
			}

			ENDCG
		}
	}

    CustomEditor "LitEditor"
}
