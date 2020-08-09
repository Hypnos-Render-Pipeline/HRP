#ifndef GBUFFER_H_
#define GBUFFER_H_


void Encode2GBuffer(fixed3 baseColor, fixed roughness, fixed metallic, float3 normal, float3 emission,
	out fixed4 target0, out fixed4 target1, out fixed4 target2)
{
	target0 = fixed4(baseColor, roughness);

	target1.w = metallic;

	float3 n = normal;
	n.xy /= dot(1, abs(n));
	if (n.z < 0) n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? 1 : -1);
	float2 k = (n.xy + 1) / 2;
	uint2 kk = k * 4095;
	target1.xy = (kk.xy & 255) / 255.0f;
	target1.z = dot(1, (kk.xy >> 8) << uint2(4, 0)) / 255.0f;


	half m = max(max(max(emission.x, emission.y), emission.z), 1);
	half3 rgb = min(1, emission / m);
	half a = min(1, log2(m) / 10);
	target2 = fixed4(rgb, a);
}

float3 DecodeNormal(fixed3 k) {
	uint3 a = k * 255;
	k.xy = (a.xy + (uint2(a.z >> 4, a.z & 15) << 8)) / 4095.0f;
	k.xy = k.xy * 2 - 1;
	float3 normal = float3(k.xy, 1 - dot(1, abs(k.xy)));
	if (normal.z < 0) normal.xy = (1 - abs(normal.yx)) * (normal.xy >= 0 ? 1 : -1);
	return normalize(normal);
}

float3 DecodeHDR(fixed4 k) {
	return k.xyz * exp2(k.w * 10);
}


void DecodeGBuffer(fixed4 target0, fixed4 target1, fixed4 target2,
	out fixed3 baseColor, out fixed roughness, out fixed metallic, out float3 normal, out float3 emission)
{
	baseColor = target0.rgb;
	roughness = target0.a;
	metallic = target1.a;
	normal = DecodeNormal(target1.xyz);
	emission = DecodeHDR(target2);
}







#endif