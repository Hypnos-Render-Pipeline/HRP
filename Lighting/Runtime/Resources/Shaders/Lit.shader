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

		[HideInInspector] _Mode("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    // Rasterization Shader
    SubShader
    {
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
			Tags { "LightMode" = "GBuffer" }

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
    }

    CustomEditor "LitEditor"
}
