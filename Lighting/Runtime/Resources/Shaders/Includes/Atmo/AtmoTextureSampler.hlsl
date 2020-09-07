#ifndef ATMO_TS_INCLUDE_
#define ATMO_TS_INCLUDE_

float4 tex2D(const sampler2D tex, const int2 xy, const float2 uv) {
	float2 clamped_uv = saturate(uv);
	clamped_uv = (clamped_uv * (xy - 1) + 0.5) / xy;
	return tex2Dlod(tex, float4(clamped_uv, 0, 0));
}
 
float4 uvTex(float4 uv) {
	return float4(uv.xyz, 1);
}

float4 tex4D(const sampler3D tex, const int4 xyzw, const float4 uv) {

	float4 clamped_uv = saturate(uv);

	float2 z = (0.5 + clamped_uv.z * (xyzw.z - 1)) / xyzw.z;

	float2 xy = (clamped_uv.xy * (xyzw.xy - 1) + 0.5) / xyzw.xy;

	float w_ = clamped_uv.w * (xyzw.w - 1);
	int low = floor(w_);
	int high = min(xyzw.w - 1, low + 1);

	z = (float2(low, high) + z) / xyzw.w;

	return lerp(tex3Dlod(tex, float4(xy, z.x, 0)), tex3Dlod(tex, float4(xy, z.y, 0)), w_ - low);
	return lerp(uvTex(float4(xy, z.x, 0)), uvTex(float4(xy, z.y, 0)), w_ - low);
}

#endif