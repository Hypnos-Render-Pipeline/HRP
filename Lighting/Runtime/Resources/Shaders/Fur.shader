Shader "HRP/Fur"
{
    Properties
    {
		_MainTex("Albedo", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)

		_Smoothness("Smoothness", Range(0,1)) = 0.5
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
				float			_Smoothness;
			CBUFFER_END

			VertexInfo GetVertexInfo(float2 uv, float4 vertex, float3 oNormal, float4 oTangent, float4 color) {
				VertexInfo info;
				info.uv = TRANSFORM_TEX(uv, _MainTex);
				info.oOffset = 0;
				info.oNormal = oNormal;
				info.oTangent = oTangent;

				return info;
			}

			SurfaceInfo GetSurfaceInfo(float2 uv, float3 wPos, float4 screenPos, float3 normal, float4 tangent, float3 vColor) {
				SurfaceInfo info = (SurfaceInfo)0;

				fixed4 diffuse = _Color * tex2D(_MainTex, uv);
				info.diffuse = diffuse.rgb;
				info.transparent = 0;

				float metallic = 0;
				info.smoothness = _Smoothness;

				float3 camPos = _V_Inv._m03_m13_m23;
				float3 view = normalize(camPos - wPos);
				info.normal = cross(normalize(cross(normal, view)), normal);
				
				info.diffuseAO_specAO = (1).xx;

				info.diffuse = DiffuseAndSpecularFromMetallic(info.diffuse, metallic, /*out*/ info.specular);

				info.gnormal = info.normal;

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
			#define Shading Lambert

			#include "./Includes/RT/Include/RTLitInclude.hlsl" 
		
			//----------------------------------------------------------------------------------------
			//------- Material data input ------------------------------------------------------------
			//----------------------------------------------------------------------------------------
			float4       _Color;
			Texture2D 	_MainTex; SamplerState sampler_MainTex;
			float4      _MainTex_ST;
			float       _Smoothness;

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

				SurfaceInfo info = (SurfaceInfo)0;

				float2 uv = i.uv0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float4 diffuse = _Color * _MainTex.SampleLevel(sampler_MainTex, uv, 0);

				info.specular = info.diffuse = diffuse.rgb;
				info.transparent = 0;

				float metallic = 0;
				info.smoothness = _Smoothness;

				info.tangent = i.tangentToWorld[2];

				float3 view = -WorldRayDirection();
				info.normal = i.tangentToWorld[2];
				info.normal = cross(normalize(cross(info.normal, view)), info.normal);
				i.tangentToWorld[0] = cross(i.tangentToWorld[2], info.normal);
				i.tangentToWorld[2] = info.normal;
				i.tangentToWorld[1] = cross(i.tangentToWorld[2], i.tangentToWorld[0]);

				info.diffuseAO_specAO = (1).xx;

				info.gnormal = i.gN;

				return info;
			}
			
			float3 ShiftTangent(float3 T, float3 N, float shift)
			{
				float3 shiftedT = T + (shift * N);
				return normalize(shiftedT);
			}
			float StrandSpecular(float3 T, float3 V, float L, float exponent)
			{
				float3 H = normalize(L + V);
				float dotTH = dot(T, H);
				float sinTH = sqrt(1.0 - dotTH * dotTH);
				float dirAtten = smoothstep(-1.0, 0.0, dot(T, H));

				return dirAtten * pow(sinTH, exponent);
			}

			//----------------------------------------------------------------------------------------
			//------- Custom shading function --------------------------------------------------------
			//----------------------------------------------------------------------------------------
			void Lambert(FragInputs IN, const float3 viewDir,
				inout int4 sampleState, inout float4 weight, inout float3 position, inout float rayRoughness,
				out float3 directColor, out float3 nextDir) {

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

				float3 t1 = ShiftTangent(surface.tangent, surface.normal, 0);
				float3 t2 = ShiftTangent(surface.tangent, surface.normal, -0.2);


				float NdotL = dot(surface.tangent, lightDir);
				NdotL = sqrt(1 - NdotL * NdotL);

				// diffuse lighting
				float3 diffuse = lerp(0.25, 1.0, saturate(NdotL));

				// specular lighting
				float3 specular = 0;
				specular += 0.5 * StrandSpecular(t1, viewDir, lightDir, 100);
				specular += surface.specular * StrandSpecular(t2, viewDir, lightDir, 100);

				float3 direct_light_without_shadow = (specular + diffuse * surface.diffuse) * luminance;

				float3 shadow = TraceShadow(IN.position, end_point,
					/*inout*/sampleState);

				directColor = shadow * direct_light_without_shadow * light_count;
 
				nextDir = CosineSampleHemisphere(float2(SAMPLE, SAMPLE), surface.normal);
				weight.xyz = surface.diffuse;
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
}
