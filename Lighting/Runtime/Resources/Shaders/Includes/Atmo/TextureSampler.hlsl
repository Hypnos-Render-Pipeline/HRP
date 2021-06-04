#ifndef ATMO_TS_INCLUDE_
#define ATMO_TS_INCLUDE_

float4 tex2D(const sampler2D tex, const int2 xy, const float2 uv) {
	float2 clamped_uv = saturate(uv);
	clamped_uv = (clamped_uv * (xy - 1) + 0.5) / xy;
	return tex2Dlod(tex, float4(clamped_uv, 0, 0));
}

#endif