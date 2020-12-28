#ifndef LITINCLUDE_H_
#define LITINCLUDE_H_

#include "UnityCG.cginc"
#include "./GBuffer.hlsl"
#include "./Light.hlsl"
#include "./LTCLight.hlsl"
#include "./PBS.hlsl"
#include "./Atmo/Sun.hlsl"

float4x4 _V,		_V_Inv;
float4x4 _P,		_P_Inv;
float4x4 _VP,		_VP_Inv;
float4x4 _Last_VP,	_Last_VP_Inv;

struct VertexInfo {
	float3 oOffset;
	float3 oNormal;
	float4 oTangent;
	float2 uv;
};

VertexInfo GetVertexInfo(float2 uv, float4 vertex, float3 oNormal, float4 oTangent);
SurfaceInfo GetSurfaceInfo(float2 uv, float3 wPos, float3 screenPos, float3 wNormal, float4 wTangent);

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
	VertexInfo info = GetVertexInfo(i.uv, i.vertex, i.normal, i.tangent);
	return UnityObjectToClipPos(i.vertex + info.oOffset);
}
void PreZ_frag() {}

Lit_v2f Lit_vert(Lit_a2v i) {

	VertexInfo info = GetVertexInfo(i.uv, i.vertex, i.normal, i.tangent);

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

	Encode2GBuffer(info.diffuse, 1 - info.smoothness, info.specular, info.normal, info.emission, 
				i.normal, info.diffuseAO_specAO.x, target0, target1, target2, target3, target4
#if _IRIDESCENCE
		, true
#endif
	);
}


Texture2D _ScreenColor;
SamplerState trilinear_clamp_sampler;
float4 _ScreenParameters;

float4 Transparent_frag(Lit_v2f i) : SV_Target{

	SurfaceInfo info = GetSurfaceInfo(i.uv, i.wpos, i.spos, i.normal, i.tangent);

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

	res += PBS(PBS_FULLY, info, sunDir, sunColor, view);

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

	float3 result = 0;
	float3 F = FresnelTerm(info.specular, dot(view, info.normal));
	float3 trans = info.diffuse * info.transparent * (1 - F);

	if (info.index != 1) {
		float3 offset = refract(-view, info.normal, 1 / info.index);
		offset = 2 * offset + view;
		float4 p = mul(UNITY_MATRIX_VP, float4(pos + offset, 1));
		p.xy /= p.w;
		p.xy = p.xy / 2 + 0.5;
		p.y = 1 - p.y;
		float lod = lerp(0, 4, min(1, (info.index - 1) * 4) * (1 - info.smoothness));
		return float4(res + _ScreenColor.SampleLevel(trilinear_clamp_sampler, p.xy, lod) * trans, 1);
	}
	else
		return float4(res + _ScreenColor.SampleLevel(trilinear_clamp_sampler, screenUV, 0) * trans, 1);
}

#endif