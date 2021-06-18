Shader "HRP/Lit"
{
    Properties
    {
		_MainTex("Albedo", 2D) = "white" {}
		_Cutoff("Alpha Cutoff",Range(0,1)) = 0
		_Color("Color", Color) = (1,1,1,1)

		_MetallicGlossMap("Metallic Smoothness", 2D) = "white" {}
		[Gamma] _Metallic("Metallic", Range(0,1)) = 0.0

		_AOMap("AO （R)", 2D) = "white" {}
		_AOScale("AO", Range(0,1)) = 1

		[Toggle]_Iridescence("Iridescent", int) = 0
		_Index2("Iridescent IOR", Range(1, 3)) = 1
		_Dinc("Iridescent Thickness(mm)", Range(0, 6)) = 1
		_DincMap("Iridescent Thickness Map(R)", 2D) = "white" {}
		
		_AnisoMap("Metallic Smoothness", 2D) = "black" {}
		_AnisoStrength("Anisotropic Strength", Range(-1, 1)) = 0.0

		_Smoothness("Smoothness", Range(0,1)) = 0.5
		_GlossMapScale("SmoothnessMapScale", Range(0,1)) = 1.0

		_BumpMap("Normal Map", 2D) = "bump" {}
		_BumpScale("Scale", Range(-10,10)) = 1.0

		_EmissionMap("EmissionMap", 2D) = "white" {}
		[HDR]_EmissionColor("EmissionColor", Color) = (0,0,0)

		_Index("IOR", Range(1, 3)) = 1.45

		_IndexRate("Relative IOR", Range(0, 1)) = 0.5

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
		HLSLINCLUDE

			#include "./Includes/LitInclude.hlsl"

			//-------------------------------------------------
			//---------  Material Define ----------------------
			//-------------------------------------------------
			CBUFFER_START(UnityPerMaterial)
				float4			_Color;
				sampler2D 		_MainTex;
				float4			_MainTex_ST;
				sampler2D		_BumpMap;
				half			_BumpScale;
				sampler2D		_MetallicGlossMap;
				float			_GlossMapScale;
				half			_Metallic;
				float			_Smoothness;
				float4			_EmissionColor;
				sampler2D		_EmissionMap;
				sampler2D		_AOMap;
				float			_AOScale;
				float			_Index;
				float			_Index2;
				float			_Dinc;
				sampler2D		_DincMap;
			CBUFFER_END

			VertexInfo GetVertexInfo(float2 uv, float4 vertex, float3 oNormal, float4 oTangent, float4 color) {
				VertexInfo info;
				info.uv = TRANSFORM_TEX(uv, _MainTex);
				info.oOffset = 0;
				info.oNormal = oNormal;
				info.oTangent = oTangent;

				return info;
			}

			SurfaceInfo GetSurfaceInfo(float2 uv, float3 wPos, float4 screenPos, float3 normal, float4 tangent) {
				SurfaceInfo info = (SurfaceInfo)0;

				fixed4 diffuse = _Color * tex2D(_MainTex, uv);
				info.diffuse = diffuse.rgb;
				info.transparent = 1 - diffuse.a;

				#if _METALLICGLOSSMAP
					float4 m_s = tex2D(_MetallicGlossMap, uv);
					float metallic = m_s.r;
					info.smoothness = m_s.a * _GlossMapScale;
				#else
					float metallic = _Metallic;
					info.smoothness = _Smoothness;
				#endif //_METALLICGLOSSMAP

				#if _NORMALMAP
					info.normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
					float3 n = normalize(normal), t = normalize(tangent.xyz);
					float3 binormal = cross(n, t) * tangent.w;
					float3x3 rotation = float3x3(t, binormal, n);
					info.normal = mul(info.normal, rotation);
				#else
					info.normal = normalize(normal);
				#endif // _NORMALMAP
				#if _EMISSION
					info.emission = _EmissionColor * tex2D(_EmissionMap, uv);
				#else
					info.emission = 0;
				#endif // _EMISSION
				#if _AOMAP
					info.diffuseAO_specAO = (1 - (_AOScale * (1 - tex2D(_AOMap, uv).r))).xx;
				#else
					info.diffuseAO_specAO = (1).xx;
				#endif // _AOMAP

				info.diffuse = DiffuseAndSpecularFromMetallic(info.diffuse, metallic, /*out*/ info.specular);

				#if _IRIDESCENCE
					float3 cpos = _V_Inv._m03_m13_m23;
					float3 v = normalize(cpos - wPos);
					float cosTheta1 = dot(normal, v);
					float cosTheta2 = sqrt(1.0 - (1 - cosTheta1 * cosTheta1) / (_Index * _Index));
					float dinc = _Dinc * tex2D(_DincMap, uv).r;
					info.specular *= IridescenceFresnel(cosTheta1, cosTheta2, _Index, _Index2, metallic, dinc);
				#endif

				info.gnormal = normal;
				info.index = _Index;

				return info;
			}

		ENDHLSL


		//-------------------------------------------------
		//-------------------- Pass -----------------------
		//-------------------------------------------------
		Pass
		{
			ColorMask 0
			Name "PreZ"
			Tags { "LightMode" = "PreZ" }
			HLSLPROGRAM
			#pragma vertex			PreZ_vert
			#pragma fragment		PreZ_frag
			ENDHLSL
		}
		Pass
		{
			Name "GBuffer_Equal"
			Tags { "LightMode" = "GBuffer_Equal" }
			ZWrite off
			ZTest Equal
			HLSLPROGRAM
			#pragma vertex			Lit_vert
			#pragma fragment		GBuffer_frag

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _AOMAP
			#pragma shader_feature _IRIDESCENCE
			ENDHLSL
		}
		Pass
		{
			Name "GBuffer_LEqual"
			Tags { "LightMode" = "GBuffer_LEqual" }
			HLSLPROGRAM
			#pragma vertex			Lit_vert
			#pragma fragment		GBuffer_frag

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _AOMAP
			#pragma shader_feature _IRIDESCENCE
			ENDHLSL
		}
		Pass
		{
			Name "Transparent"
			Tags { "LightMode" = "Transparent" }
			//Blend off
			//ZTest on
			//ZWrite on
			//Cull back
			HLSLPROGRAM
			#pragma vertex			Lit_vert
			#pragma fragment		Transparent_frag

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _AOMAP
			#pragma shader_feature _IRIDESCENCE
			ENDHLSL
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
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _ANISOMAP
			#pragma shader_feature _AOMAP
			#pragma shader_feature _SUBSURFACE
			#pragma shader_feature _CLEARCOAT
			#pragma shader_feature _IRIDESCENCE
			#pragma shader_feature _ENABLEFOG
		

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

			#if _ANISOMAP

			Texture2D _AnisoMap; SamplerState sampler_PointRepeat;

			#endif

			float _AnisoStrength;

			#if _EMISSION

			float4      _EmissionColor;
			Texture2D   _EmissionMap;

			#endif // _EMISSION
			#if _AOMAP
			Texture2D	_AOMap;
			float		_AOScale;
			#endif // _AOMAP
						
			#if _IRIDESCENCE
			float _Index2;
			float _Dinc;
			Texture2D _DincMap;
			#endif

			float _Index;

			float _ClearCoat;
			float _Sheen;

			float _MipScale;

			//struct SurfaceInfo {
			//	float3	diffuse;
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
				float4 diffuse = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
				IN.diffuse = _Color * diffuse.xyz;

				IN.transparent = 1 - diffuse.a * _Color.a;
								
				#if _METALLICGLOSSMAP
					float4 m_s = SampleTex(_MetallicGlossMap, uv, 0);
					float metallic = m_s.r;
					IN.smoothness = m_s.a * _GlossMapScale;
				#else
					float metallic = _Metallic;
					IN.smoothness = _Smoothness;
				#endif
				
				#if _ANISOMAP
					float3 aniso = SampleTex(_AnisoMap, uv, 0).xyz;
					if (aniso.z > 0.5)
						aniso = _AnisoMap.SampleLevel(sampler_PointRepeat, uv, 0);
					IN.aniso = aniso.x * _AnisoStrength;
					float aniso_angle = aniso.y;
				#else
					IN.aniso = _AnisoStrength;
					float aniso_angle = 0;
				#endif

				IN.diffuse = DiffuseAndSpecularFromMetallic(IN.diffuse, metallic, /*out*/ IN.specular);
				
				IN.gnormal = i.tangentToWorld[2];

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
					
				#if _IRIDESCENCE
					IN.iridescence = true;
					IN.index2 = _Index2;
					IN.dinc = _Dinc * SampleTex(_DincMap, uv, 0).r;
				#endif

				float2 xy; sincos(aniso_angle * 2 * PI, xy.y, xy.x);
				IN.tangent = mul(float3(xy, 0), i.tangentToWorld);

				IN.discarded = diffuse.a < _Cutoff;
				
				IN.clearCoat *= 1 - IN.transparent;

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

				directColor = surface.diffuse * shadow * direct_light_without_shadow * light_count;

				nextDir = CosineSampleHemisphere(float2(SAMPLE, SAMPLE), surface.normal);
				weight.xyz = surface.diffuse;
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

		// Realtime pass
		Pass
		{
			Name "RTGI"
			Tags{ "LightMode" = "RTGI" }

			CGPROGRAM

			#pragma raytracing test

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _EMISSION
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _AOMAP

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
			float _IndexRate;

			float _ClearCoat;
			float _Sheen;

			float _MipScale;

			void GetSurfaceInfo(inout FragInputs i, out float3 albedo, out float transparent, out float index, out float index_rate, out float metallic, out float smoothness, out float3 normal, out float3 emission) {
				float2 uv = i.uv0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float4 diffuse = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
				albedo = _Color * diffuse.xyz;

				transparent = 1 - diffuse.a * _Color.a;

				#if _METALLICGLOSSMAP
					float4 m_s = SampleTex(_MetallicGlossMap, uv, 0);
					metallic = m_s.r;
					smoothness = m_s.a * _GlossMapScale;
				#else
					metallic = _Metallic;
					smoothness = _Smoothness;
				#endif

				#if _NORMALMAP
					normal = UnpackScaleNormal(SampleTex(_BumpMap, uv, 0), _BumpScale);
					normal = normalize(mul(normal * float3(-1,-1,1), i.tangentToWorld));
					normal *= i.isFrontFace ? 1 : -1;
				#else
					normal = i.tangentToWorld[2];
					normal *= i.isFrontFace ? 1 : -1;
				#endif // _NORMALMAP

				#if _EMISSION
					emission = _EmissionColor * SampleTex(_EmissionMap, uv, 0);
				#else
					emission = 0;
				#endif

				index = _Index;
				index_rate = _IndexRate;
			}

			[shader("closesthit")]
			void ClosestHit(inout RayIntersection_RTGI rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
			{
				CALCULATE_DATA(fragInput, viewDir);

				GBuffer_RTGI gbuffer;

				gbuffer.dis = RayTCurrent();
				GetSurfaceInfo(fragInput, gbuffer.albedo, gbuffer.transparent, gbuffer.index, gbuffer.index_rate, gbuffer.metallic, gbuffer.smoothness, gbuffer.normal, gbuffer.emission);
				gbuffer.front = fragInput.isFrontFace;
				rayIntersection = EncodeGBuffer2RIData(gbuffer);
			}

			[shader("anyhit")]
			void AnyHit(inout RayIntersection_RTGI rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
			{
				//CALCULATE_DATA(fragInput, viewDir);
				//if (abs(dot(fragInput.tangentToWorld[2], WorldRayDirection())) < 0.13) {
				//	IgnoreHit(); return;
				//}
				rayIntersection.data1 = 0;
				//AcceptHitAndEndSearch();
			}

			ENDCG
		}
	}
    CustomEditor "HypnosRenderPipeline.LitEditor" 
}
