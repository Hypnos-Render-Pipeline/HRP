#ifndef RTATMO_H_
#define RTATMO_H_

float2 _TLutResolution;
float _PlanetRadius, _AtmosphereThickness;
float3 _GroundAlbedo, _RayleighScatter;
float _MeiScatter, _OZone, _SunAngle, _MultiScatterStrength;
float3 _SunDir, _SunLuminance;
Texture2D _TLut; SamplerState sampler_TLut;
#define zip_order 4
float unzip(float x) {
	return x < 0.5 ? (x * pow(abs(2 * x), zip_order - 1)) : ((1 - x) * pow(2 * x - 2, zip_order - 1) + 1);
}
float zip(float x) {
	return x < 0.5 ? pow(abs(x / (1 << zip_order - 1)), 1.0f / zip_order) : (1 - pow(abs((1 - x) / (1 << zip_order - 1)), 1.0f / zip_order));
}
float3 T(const float2 uv) {
	float2 clamped_uv = saturate(uv);
	clamped_uv = (clamped_uv * (_TLutResolution - 1) + 0.5) / _TLutResolution;
	return _TLut.SampleLevel(sampler_TLut, clamped_uv, 0).xyz;
}
inline const float Altitude(const float3 x) {
	return length(x) - _PlanetRadius;
}
const float3 T(const float3 x, const float3 v) {

	float horiz = length(x);
	horiz = -saturate(sqrt(horiz * horiz - _PlanetRadius * _PlanetRadius) / horiz);

	float cos = clamp(-1, 1, dot(v, normalize(x)));
	float2 uv;
	if (cos < horiz) {
		return 0;
	}
	uv.x = saturate(Altitude(x) / _AtmosphereThickness);
	uv.y = (cos - horiz) / (1 - horiz) / 2;
	uv.y = zip(uv.y);
	return T(uv);
}


#endif