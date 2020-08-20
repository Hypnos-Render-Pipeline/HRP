#ifndef GBUFFER_H_
#define GBUFFER_H_


fixed3 EncodeNormal(float3 n) {
	fixed3 res;
	n.xy /= dot(1, abs(n));
	if (n.z < 0) n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? 1 : -1);
	float2 k = (n.xy + 1) / 2;
	uint2 kk = k * 4095;
	res.xy = (kk.xy & 255) / 255.0f;
	res.z = dot(1, (kk.xy >> 8) << uint2(4, 0)) / 255.0f;
	return res;
}

float3 DecodeNormal(fixed3 k) {
	uint3 a = k * 255;
	k.xy = (a.xy + (uint2(a.z >> 4, a.z & 15) << 8)) / 4095.0f;
	k.xy = k.xy * 2 - 1;
	float3 normal = float3(k.xy, 1 - dot(1, abs(k.xy)));
	if (normal.z < 0) normal.xy = (1 - abs(normal.yx)) * (normal.xy >= 0 ? 1 : -1);
	return normalize(normal);
}

fixed4 EncodeHDR(float3 e) {
	half m = max(max(max(e.x, e.y), e.z), 1);
	half3 rgb = min(1, e / m);
	half a = min(1, log2(m) / 10);
	return fixed4(rgb, a);
}

float3 DecodeHDR(fixed4 k) {
	return k.xyz * exp2(k.w * 10);
}

void Encode2GBuffer(fixed3 baseColor, fixed roughness, fixed metallic, float3 normal, float3 emission, float3 gnormal, float ao,
	out fixed4 target0, out fixed4 target1, out fixed4 target2, out fixed4 target3)
{
	target0 = fixed4(baseColor, roughness);

	target1.xyz = EncodeNormal(normal);
	target1.w = metallic;

	target2 = EncodeHDR(emission);

	target3.xyz = EncodeNormal(gnormal);
	target3.w = ao;
}



void DecodeGBuffer(fixed4 target0, fixed4 target1, fixed4 target2, fixed4 target3,
	out fixed3 baseColor, out fixed roughness, out fixed metallic, out float3 normal, out float3 emission, out float3 gnormal, out float ao)
{
	baseColor = target0.rgb;
	roughness = target0.a;
	metallic = target1.a;
	normal = DecodeNormal(target1.xyz);
	emission = DecodeHDR(target2);
	gnormal = DecodeNormal(target3.xyz);
	ao = target3.w;
}

inline float OneMinusReflectivityFromMetallic(const float metallic)
{
	float oneMinusDielectricSpec = 1.0 - 0.04;
	return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}


inline float3 DiffuseAndSpecularFromMetallic(const float3 albedo, const float metallic, out float3 specColor)
{
	specColor = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
	float oneMinusReflectivity = OneMinusReflectivityFromMetallic(metallic);
	return albedo * oneMinusReflectivity;
}





#endif