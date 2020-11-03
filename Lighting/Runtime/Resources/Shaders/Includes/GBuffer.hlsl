#ifndef GBUFFER_H_
#define GBUFFER_H_

#include "PBS.hlsl"

half3 EncodeNormal(float3 n) {
	half3 res;
	n.xy /= dot(1, abs(n));
	if (n.z < 0) n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? 1 : -1);
	float2 k = (n.xy + 1) / 2;
	uint2 kk = k * 4095;
	res.xy = (kk.xy & 255) / 255.0f;
	res.z = dot(1, (kk.xy >> 8) << uint2(4, 0)) / 255.0f;
	return res;
}

float3 DecodeNormal(half3 k) {
	uint3 a = k * 255;
	k.xy = (a.xy + (uint2(a.z >> 4, a.z & 15) << 8)) / 4095.0f;
	k.xy = k.xy * 2 - 1;
	float3 normal = float3(k.xy, 1 - dot(1, abs(k.xy)));
	if (normal.z < 0) normal.xy = (1 - abs(normal.yx)) * (normal.xy >= 0 ? 1 : -1);
	return normalize(normal);
}

half4 EncodeHDR(float3 e) {
	half m = max(max(max(e.x, e.y), e.z), 1);
	half3 rgb = min(1, e / m);
	half a = min(1, log2(m) / 10);
	return half4(rgb, a);
}

float3 DecodeHDR(half4 k) {
	return k.xyz * exp2(k.w * 10);
}

void Encode2GBuffer(half3 baseColor, half roughness, half metallic, float3 normal, float3 emission, float3 gnormal, float ao,
	out half4 target0, out half4 target1, out half4 target2, out half4 target3)
{
	target0 = half4(baseColor, roughness);

	target1.xyz = EncodeNormal(normal);
	target1.w = metallic;

	target2 = EncodeHDR(emission);

	target3.xyz = EncodeNormal(gnormal);
	target3.w = ao;
}



SurfaceInfo DecodeGBuffer(half4 target0, half4 target1, half4 target2, half4 target3)
{
	SurfaceInfo info = (SurfaceInfo)0;
	info.baseColor = target0.rgb;
	info.smoothness = 1 - target0.a;
	info.metallic = target1.a;
	info.normal = DecodeNormal(target1.xyz);
	info.emission = DecodeHDR(target2);
	info.gnormal = DecodeNormal(target3.xyz);
	info.diffuseAO_specAO = target3.ww;

	return info;
}



#endif