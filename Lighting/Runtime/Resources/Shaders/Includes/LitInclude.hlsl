#ifndef LITINCLUDE_H_
#define LITINCLUDE_H_

#undef SAMPLE_DEPTH_TEXTURE
#undef SAMPLE_DEPTH_TEXTURE_LOD
#undef TRANSFORM_TEX

#include "UnityShaderVariables.cginc"

#undef SAMPLE_DEPTH_TEXTURE
#undef SAMPLE_DEPTH_TEXTURE_LOD
#undef TRANSFORM_TEX

float4x4 _V, _V_Inv;
float4x4 _P, _P_Inv;
float4x4 _VP, _VP_, _VP_Inv;
float4x4 _Last_VP, _Last_VP_Inv;

#define TRANSFORM_TEX(tex,name) (tex.xy * name##_ST.xy + name##_ST.zw)
// Tranforms position from world to homogenous space
inline float4 UnityWorldToClipPos(in float3 pos)
{
	return mul(_VP, float4(pos, 1.0));
}

// Tranforms position from view to homogenous space
inline float4 UnityViewToClipPos(in float3 pos)
{
	return mul(_P, float4(pos, 1.0));
}

// Tranforms position from object to camera space
inline float3 UnityObjectToViewPos(in float3 pos)
{
	return mul(_V, mul(unity_ObjectToWorld, float4(pos, 1.0))).xyz;
}
inline float3 UnityObjectToViewPos(float4 pos) // overload for float4; avoids "implicit truncation" warning for existing shaders
{
	return UnityObjectToViewPos(pos.xyz);
}
inline float4 UnityObjectToClipPos(in float3 pos)
{
	return mul(_VP_, mul(unity_ObjectToWorld, float4(pos, 1.0)));
}
// Tranforms position from world to camera space
inline float3 UnityWorldToViewPos(in float3 pos)
{
	return mul(_V, float4(pos, 1.0)).xyz;
}

// Transforms direction from object to world space
inline float3 UnityObjectToWorldDir(in float3 dir)
{
	return normalize(mul((float3x3)unity_ObjectToWorld, dir));
}

// Transforms direction from world to object space
inline float3 UnityWorldToObjectDir(in float3 dir)
{
	return normalize(mul((float3x3)unity_WorldToObject, dir));
}

// Transforms normal from object to world space
inline float3 UnityObjectToWorldNormal(in float3 norm)
{
#ifdef UNITY_ASSUME_UNIFORM_SCALING
	return UnityObjectToWorldDir(norm);
#else
	// mul(IT_M, norm) => mul(norm, I_M) => {dot(norm, I_M.col0), dot(norm, I_M.col1), dot(norm, I_M.col2)}
	return normalize(mul(norm, (float3x3)unity_WorldToObject));
#endif
}

// Computes world space light direction, from world space position
inline float3 UnityWorldSpaceLightDir(in float3 worldPos)
{
#ifndef USING_LIGHT_MULTI_COMPILE
	return _WorldSpaceLightPos0.xyz - worldPos * _WorldSpaceLightPos0.w;
#else
#ifndef USING_DIRECTIONAL_LIGHT
	return _WorldSpaceLightPos0.xyz - worldPos;
#else
	return _WorldSpaceLightPos0.xyz;
#endif
#endif
}

// Computes world space light direction, from object space position
// *Legacy* Please use UnityWorldSpaceLightDir instead
inline float3 WorldSpaceLightDir(in float4 localPos)
{
	float3 worldPos = mul(unity_ObjectToWorld, localPos).xyz;
	return UnityWorldSpaceLightDir(worldPos);
}

// Computes object space light direction
inline float3 ObjSpaceLightDir(in float4 v)
{
	float3 objSpaceLightPos = mul(unity_WorldToObject, _WorldSpaceLightPos0).xyz;
#ifndef USING_LIGHT_MULTI_COMPILE
	return objSpaceLightPos.xyz - v.xyz * _WorldSpaceLightPos0.w;
#else
#ifndef USING_DIRECTIONAL_LIGHT
	return objSpaceLightPos.xyz - v.xyz;
#else
	return objSpaceLightPos.xyz;
#endif
#endif
}

// Computes world space view direction, from object space position
inline float3 UnityWorldSpaceViewDir(in float3 worldPos)
{
	return _WorldSpaceCameraPos.xyz - worldPos;
}

// Computes world space view direction, from object space position
// *Legacy* Please use UnityWorldSpaceViewDir instead
inline float3 WorldSpaceViewDir(in float4 localPos)
{
	float3 worldPos = mul(unity_ObjectToWorld, localPos).xyz;
	return UnityWorldSpaceViewDir(worldPos);
}

// Computes object space view direction
inline float3 ObjSpaceViewDir(in float4 v)
{
	float3 objSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
	return objSpaceCameraPos - v.xyz;
}

inline float4 ComputeNonStereoScreenPos(float4 pos) {
	float4 o = pos * 0.5f;
	o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
	o.zw = pos.zw;
	return o;
}

inline float4 ComputeScreenPos(float4 pos) {
	float4 o = ComputeNonStereoScreenPos(pos);
#if defined(UNITY_SINGLE_PASS_STEREO)
	o.xy = TransformStereoScreenSpaceTex(o.xy, pos.w);
#endif
	return o;
}

#include "./GBuffer.hlsl"
#include "./Light.hlsl"
#include "./LTCLight.hlsl"
#include "./PBS.hlsl"
#include "./Atmo/Sun.hlsl"

struct VertexInfo {
	float3 oOffset;
	float3 oNormal;
	float4 oTangent;
	float2 uv;
};

VertexInfo GetVertexInfo(float2 uv, float4 vertex, float3 oNormal, float4 oTangent, float4 color);
SurfaceInfo GetSurfaceInfo(float2 uv, float3 wPos, float4 screenPos, float3 wNormal, float4 wTangent);

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

struct Lit_a2v {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;
	float4 color : COLOR;
};

struct Lit_v2f {
	float4 vertex : SV_POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;
	float3 wpos : TEXCOORD1;
	float4 spos : TEXCOORD2;
};

float4 PreZ_vert(Lit_a2v i) : SV_POSITION {
	VertexInfo info = GetVertexInfo(i.uv, i.vertex, i.normal, i.tangent, i.color);
	return UnityObjectToClipPos(i.vertex + info.oOffset);
}
void PreZ_frag() {}

Lit_v2f Lit_vert(Lit_a2v i) {

	VertexInfo info = GetVertexInfo(i.uv, i.vertex, i.normal, i.tangent, i.color);

	Lit_v2f o;
	o.vertex = UnityObjectToClipPos(i.vertex + info.oOffset);
	o.normal = UnityObjectToWorldNormal(info.oNormal);
	o.tangent = float4(UnityObjectToWorldDir(info.oTangent.xyz), info.oTangent.w);
	o.uv = info.uv; 
	o.wpos = mul(unity_ObjectToWorld, i.vertex);
	o.spos = ComputeScreenPos(o.vertex);
	return o;
}

void GBuffer_frag(Lit_v2f i, out fixed4 target0 : SV_Target0, out fixed4 target1 : SV_Target1, out fixed4 target2 : SV_Target2, out fixed4 target3 : SV_Target3, out fixed4 target4 : SV_Target4) {

	SurfaceInfo info = GetSurfaceInfo(i.uv, i.wpos, i.spos, i.normal, i.tangent);

	Encode2GBuffer(info.diffuse, 0, 1 - info.smoothness, info.specular, info.normal, info.emission, 
				i.normal, info.diffuseAO_specAO.x, target0, target1, target2, target3, target4
#if _IRIDESCENCE
		, true
#endif
	);
}


Texture2D _ScreenColor;
SamplerState trilinear_clamp_sampler;
float4 _ScreenParameters;

void Transparent_frag(Lit_v2f i, out fixed4 target0 : SV_Target0, out fixed4 target1 : SV_Target1, out fixed4 target2 : SV_Target2, out fixed4 target3 : SV_Target3, out fixed4 target4 : SV_Target4, out fixed target5 : SV_Target5) {

	SurfaceInfo info = GetSurfaceInfo(i.uv, i.wpos, i.spos, i.normal, i.tangent);

	Encode2GBuffer(info.diffuse, info.transparent, 1 - info.smoothness, info.specular, info.normal, info.emission,
		i.normal, info.diffuseAO_specAO.x, info.index,
		target0, target1, target2, target3, target4, target5
#if _IRIDESCENCE
		, true
#endif
	);

}

#endif