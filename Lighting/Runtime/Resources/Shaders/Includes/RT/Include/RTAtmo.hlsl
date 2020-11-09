#ifndef RTATMO_H_
#define RTATMO_H_

Texture2D _Skybox; SamplerState sampler_Skybox;
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

	float c = clamp(-1, 1, dot(v, normalize(x)));
	float2 uv;
	if (c < horiz) {
		return 0;
	}
	uv.x = saturate(Altitude(x) / _AtmosphereThickness);
	uv.y = (c - horiz) / (1 - horiz) / 2;
	uv.y = zip(uv.y);
	return T(uv);
}

const float3 AtmoTrans(const float3 x, const float3 y) {
	const float3 v = normalize(y - x);

	float3 a = T(x, v);
	float3 b = T(y, v);

	return min(1.0, a / b);
}

const float3 Atmo(float3 x, const float3 v, const float3 s) {
	float phi = acos(clamp(dot(normalize(v.xz), normalize(s.xz)), -1, 1)) / 3.14159265359;

	float rho;
	float horiz = length(x);
	horiz = -sqrt(horiz * horiz - _PlanetRadius * _PlanetRadius) / horiz;

	if (v.y < horiz) {
		rho = pow((v.y + 1) / (horiz + 1), 2) * 0.5;
	}
	else {
		float atmosphere_radius = _PlanetRadius + _AtmosphereThickness;
		if (length(x) > atmosphere_radius) {
			float ahoriz = length(x);
			ahoriz = -sqrt(ahoriz * ahoriz - atmosphere_radius * atmosphere_radius) / ahoriz;
			if (v.y > ahoriz) rho = -1;
			else rho = (v.y - horiz) / (ahoriz - horiz) * 0.5 + 0.5;
		}
		else {
			rho = pow((v.y - horiz) / (1 - horiz), 0.5) * 0.5 + 0.5;
		}
	}

	float3 scatter = rho >= 0 ? _Skybox.SampleLevel(sampler_Skybox, float2(phi, rho), 0).xyz : 0;

	// prevent error
	scatter = max(0, scatter);

	float3 sun = 1;
	sun = T(x, v);
	sun *= smoothstep(cos(_SunAngle + 0.005), cos(_SunAngle), dot(v, s));
	sun /= 0.2e4;

	return (scatter + sun) * _SunLuminance;
}

const float3 Sunlight(const float3 x, const float3 s) {

	float horiz = length(x);
	horiz = -saturate(sqrt(horiz * horiz - _PlanetRadius * _PlanetRadius) / horiz);

	float c = clamp(-1, 1, dot(s, normalize(x)));
	float2 uv;
	if (c < horiz) {
		return 0;
	}
	uv.x = saturate(Altitude(x) / _AtmosphereThickness);
	uv.y = (c - horiz) / (1 - horiz) / 2;
	uv.y = zip(uv.y);
	return T(uv) * (1 - cos(_SunAngle)) * 39810;
}


#endif