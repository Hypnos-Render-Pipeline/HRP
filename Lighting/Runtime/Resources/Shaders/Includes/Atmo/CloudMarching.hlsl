#ifndef CLOUD_MARCHING_H_
#define CLOUD_MARCHING_H_


#include "./Atmo.hlsl"

float _CloudCoverage;
float _CloudDensity;

float4 _Quality;

float4x4 _CloudMat;
float4x4 _CloudMat_Inv;
float4x4 _LightTransform;

sampler2D _CloudShadowMap;
sampler2D _CloudMap;
sampler2D _HighCloudMap;
sampler2D _SpaceMap;
sampler3D _WorleyPerlinVolume;
sampler3D _WorleyVolume;

int _Clock;
float4 _Time;

#ifndef planet_radius
#define planet_radius 6371e3
#endif
static float2 cloud_radi = float2(1500, 2700);

float3 aces_tonemap(float3 color) {
	float3x3 m1 = float3x3(
		0.59719, 0.35458, 0.04823,
		0.07600, 0.90834, 0.01566,
		0.02840, 0.13383, 0.83777
		);
	float3x3 m2 = float3x3(
		1.60475, -0.53108, -0.07367,
		-0.10208, 1.10813, -0.00605,
		-0.00327, -0.07276, 1.07602
		);
	float3 v = mul(m1, color);
	float3 a = v * (v + 0.0245786) - 0.000090537;
	float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
	return clamp(mul(m2, (a / b)), 0.0, 1.0);
}

uint sampleIndex = 0;
float hash(float n)
{
	return frac(sin(n) * 43758.5453);
}
float Roberts1_(uint n) {
	const float g = 1.6180339887498948482;
	const float a = 1.0 / g;
	return frac(0.5 + a * n);
}
float2 Roberts2_(uint n) {
	const float g = 1.32471795724474602596;
	const float2 a = float2(1.0 / g, 1.0 / (g * g));
	return  frac(0.5 + a * n);
}
void RandSeed(uint2 seed) {
	sampleIndex = (uint)_Clock % 24 + hash((seed.x % 8 + seed.y % 8 * 8) / 64.) * 64;
}

float Rand()
{
	return Roberts1_(sampleIndex++);
}

float IntersectSphere(float3 p, float3 v, float3 o, float r)
{
	float3 oc = p - o;
	float b = 2.0 * dot(v, oc);
	float c = dot(oc, oc) - r * r;
	float disc = b * b - 4.0 * c;
	if (disc < 0.0)
		return 0;
	float q = (-b + ((b < 0.0) ? -sqrt(disc) : sqrt(disc))) / 2.0;
	float t0 = q;
	float t1 = c / q;
	if (t0 > t1) {
		float temp = t0;
		t0 = t1;
		t1 = temp;
	}
	if (t1 < 0.0)
		return 0;

	return (t0 < 0.0) ? t1 : t0;
}

float2 IntersectSphere2(float3 p, float3 v, float3 o, float r)
{
	float3 oc = p - o;
	float b = 2.0 * dot(v, oc);
	float c = dot(oc, oc) - r * r;
	float disc = b * b - 4.0 * c;
	if (disc < 0.0)
		return 0;
	float q = (-b + ((b < 0.0) ? -sqrt(disc) : sqrt(disc))) / 2.0;
	float t0 = q;
	float t1 = c / q;
	return float2(t0, t1);
}

inline float GetT(float s, float t) {
	return exp(-t * s);
}
float GetPhase(float g, float dot) {
	return (1 - g * g) / pow(1 + g * g + 2 * g * dot, 1.5);
}
float numericalMieFit(float costh)
{
	// This function was optimized to minimize (delta*delta)/reference in order to capture
	// the low intensity behavior.
	float bestParams[10];
	bestParams[0] = 9.805233e-06;
	bestParams[1] = -6.500000e+01;
	bestParams[2] = -5.500000e+01;
	bestParams[3] = 8.194068e-01;
	bestParams[4] = 1.388198e-01;
	bestParams[5] = -8.370334e+01;
	bestParams[6] = 7.810083e+00;
	bestParams[7] = 2.054747e-03;
	bestParams[8] = 2.600563e-02;
	bestParams[9] = -4.552125e-12;

	float p1 = costh + bestParams[3];
	float4 expValues = exp(float4(bestParams[1] * costh + bestParams[2], bestParams[5] * p1 * p1, bestParams[6] * costh, bestParams[9] * costh));
	float4 expValWeight = float4(bestParams[0], bestParams[4], bestParams[7], bestParams[8]);
	return dot(expValues, expValWeight) * 0.25;
}
inline float numericalMieFitMultiScatter() {
	// This is the acossiated multi scatter term used to simulate multi scatter effect.
	return 0.1026;
}

float remap(float v, float a, float b, float c, float d) {
	return (clamp(v, a, b) - a) / max(0.0001, b - a) * (d - c) + c;
}

float rescale01(float v, float a, float b) {
	return (clamp(v, a, b) - a) / max(0.0001, b - a);
}

float rescale10(float v, float a, float b) {
	return 1 - (clamp(v, a, b) - a) / max(0.0001, b - a);
}

float Cloud_H(float3 p) {
	return (length(p) - (planet_radius + cloud_radi.x)) / (cloud_radi.y - cloud_radi.x);
}

float CloudType(float h, float t) {
	float a = lerp(0.01, 0.2, t);
	float b = lerp(0.15, 0.4, t);
	float c = lerp(0.1, 1, t);

	return rescale01(h, 0, a) * rescale10(h, b, b + c * 0.7) * t;
}

float3 Space(float3 v) {
	v = mul(_LightTransform, float4(v, 0));
	float3 normalizedCoords = normalize(v);
	float latitude = acos(normalizedCoords.y);
	float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
	float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / 3.14159265359f, 1.0 / 3.14159265359f);
	return tex2Dlod(_SpaceMap, float4(float2(0.5, 1.0) - sphereCoords, 0, 0));
}

inline float4 HighCloud(const float3 p, const float3 v) {
	float dis = IntersectSphere(p, v, float3(0, 0, 0), planet_radius + cloud_radi.y + 1000);

	float3 hitP = p + dis * v;
	float3 nom_p = normalize(hitP);
	
	float data1 = tex2Dlod(_HighCloudMap, float4(nom_p.xz * 300 + _Time.x * 0.005, 0, 0)).x;

	float data2 = saturate(dis / 4500 - 1) * tex2Dlod(_HighCloudMap, float4(nom_p.xz * 400 + float2(-1, 1) * _Time.x * 0.01, 0, 0)).y;

	float control = tex2Dlod(_HighCloudMap, float4(nom_p.xz * 100 - _Time.x * 0.01, 0, 0)).z;

	return float4(max(data1 * control, data2), hitP);
}

inline float Cloud_Shape(const float3 p, const float fade = 1) {
	float h = Cloud_H(p);

	h *= 0.833;

	float3 nom_p = normalize(p);
	float cov = tex2Dlod(_CloudMap, float4(nom_p.xz * 100 + _Time.x * 0.004, 0, 0)).x;

	cov = rescale01(cov, 1 - _CloudCoverage, 1);
	cov = lerp(1, cov, fade);

	float ero_value = 1 - CloudType(h, cov);

	float cloud = rescale01(0.67, ero_value, 1);

	return (cloud > 0) ? 1 : 0;
}

inline float Cloud(const float3 p, const float fade = 1) {
	float h = Cloud_H(p);

	h *= 0.833;

	float3 nom_p = normalize(p);
	float cov = tex2Dlod(_CloudMap, float4(nom_p.xz * 100 + _Time.x * 0.004, 0, 0)).x;

	cov = rescale01(cov, 1 - _CloudCoverage, 1);
	cov = lerp(1, cov, fade);

	float ero_value = 1 - CloudType(h, cov);

	float noise0 = dot(tex3Dlod(_WorleyPerlinVolume, float4(p * 0.00057, 0)).xyz, float3(0.625, 0.25, 0.125));

	float noise1 = dot(tex3Dlod(_WorleyVolume, float4(p * 0.006, 0)).xyz, float3(0.625, 0.25, 0.125));

	noise1 = lerp(noise1, 1 - noise1, min(1, h * 3));

	float cloud = rescale01(noise0, ero_value, 1);

	cloud = cloud - (cloud < 0.4 && cloud > 0 ? rescale01(noise1, 0.2 + 2 * cloud, 1) : 0);

	return (cloud > 0) ? sqrt(cov) * smoothstep(0, 0.6, h) * 0.02 * _CloudDensity : 0;
}

inline float Cloud_Simple(const float3 p, const float fade = 1) {
	//return Cloud(p);
	float h = Cloud_H(p);

	h *= 0.833;

	float3 nom_p = normalize(p);
	float cov = tex2Dlod(_CloudMap, float4(nom_p.xz * 100 + _Time.x * 0.004, 0, 0)).x;
	cov = lerp(1, cov, fade);

	cov = remap(cov, 1 - _CloudCoverage, 1, 0, 1);

	float ero_value = 1 - CloudType(h, cov);

	float noise0 = dot(tex3Dlod(_WorleyPerlinVolume, float4(p * 0.00057, 0)).xyz, float3(0.625, 0.25, 0.125));

	float cloud = rescale01(noise0, ero_value, 1);

	return (cloud > 0) ? sqrt(cov) * smoothstep(0, 0.6, h) * 0.02 * _CloudDensity : 0;
}

inline float GetT(const float samples, const float3 p, const float3 v, const float d = 800) {

	float sample_num = max(4, floor(8 * samples));
	float bias = Rand();
	float trans = 1;
	float last_t = 0;
	float3 v_ = v * d;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = (i + bias) / sample_num;
		t = -0.333333 * log(1 - 0.9502129 * t);
		float3 pos = v_ * t + p;
		float scatter = Cloud(pos).x;
		trans *= GetT(scatter, (t - last_t) * d);
		last_t = t;
	}
	return trans;
}

inline float GetT(const float samples, const float3 p, const float3 v, const float3 vt, const float3 vbt, const float vs, const float fade = 1, const float d = 1600) {

	int sample_num = lerp(_Quality.y / 4, _Quality.y, samples);
	float bias = Rand();
	float trans = 1;
	float trans2 = 1;
	float last_t = 0;
	float3 v_ = v * d;
	float3 vt_ = vt * d * 0.3;
	float3 vbt_ = vbt * d * 0.3;
	float inv_sample_num = 1.0 / sample_num;
	bias *= inv_sample_num;
	//[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = i * inv_sample_num + bias;
		t = -0.333333 * log(-0.9502129 * t + 1);
		float3 pos = v_ * t + p;

		float2 rnd = frac(Roberts2_(i) + bias);
		rnd.x *= (2 * 3.14159265359);
		float2 offset; sincos(rnd.x, offset.x, offset.y);
		offset *= rnd.y * t;
		pos += vt_ * offset.x + vbt_ * offset.y;
		float scatter = Cloud(pos, fade);
		float delta = (t - last_t) * d;
		trans *= GetT(scatter * 0.8, delta);
		trans2 *= GetT(scatter * 1.6, delta);
		last_t = t;
	}
	return trans * (1 - trans2 * vs);
}

inline float GetT_Simple(const float3 p, const float3 v, const float3 vt, const float3 vbt, const float vs, const float fade = 0, const float d = 400) {

	float sample_num = 2;
	float bias = Rand();
	float trans = 1;
	float trans2 = 1;
	float last_t = 0;
	float3 v_ = v * d;
	float3 vt_ = vt * d * 0.3;
	float3 vbt_ = vbt * d * 0.3;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = (i + bias) / sample_num;
		t = -0.333333 * log(1 - 0.9502129 * t);
		float3 pos = v_ * t + p;

		float2 rnd = frac(Roberts2_(i) + bias);
		rnd.x *= 2 * 3.14159265359;
		float2 offset = float2(sin(rnd.x), cos(rnd.x)) * rnd.y * t;
		pos += vt_ * offset.x + vbt_ * offset.y;
		float scatter = Cloud_Simple(pos, fade).x;
		float delta = (t - last_t) * d;
		trans *= GetT(scatter * 0.8, delta);
		trans2 *= GetT(scatter * 1.6, delta);
		last_t = t;
	}
	return trans * (1 - trans2 * vs);
}

float4 CloudRender(float3 camP, float3 p, float3 v, float d = 9999999) {

	const float max_dis = 6000;
	const float sample_num = _Quality.z;

	float3 st;
	float dis = max_dis;
	float offset = 0;
	float h = Cloud_H(p);
	float frac_h = frac(h);
	if (min(1 - frac_h, frac_h) < 0.0001) p += frac_h > 0.5 ? -0.00001 : 0.00001;

	if (h > 1) {
		offset = IntersectSphere(p, v, float3(0, 0, 0), planet_radius + cloud_radi.y);
		st = p + v * offset;
		dis = min(dis, IntersectSphere(st, v, float3(0, 0, 0), planet_radius + cloud_radi.x));
	}
	else if (h > 0) {
		st = p;
		offset = IntersectSphere(st, v, float3(0, 0, 0), planet_radius + cloud_radi.x);
		dis = offset == 0 ? dis : min(offset, dis);
		offset = IntersectSphere(st, v, float3(0, 0, 0), planet_radius + cloud_radi.y);
		dis = offset == 0 ? dis : min(offset, dis);
		offset = 0;
	}
	else {
		offset = IntersectSphere(p, v, float3(0, 0, 0), planet_radius + cloud_radi.x);
		st = offset * v + p;
		dis = min(dis, IntersectSphere(st, v, float3(0, 0, 0), planet_radius + cloud_radi.y));
	}

	if (h <= 0 && IntersectSphere(p, v, float3(0, 0, 0), planet_radius) > 0)
		return float4(0, 0, 0, 1);

	d -= offset;
	d = min(d, dis);
	dis = max_dis;

	if (d < 0 || offset > 50000) return float4(0, 0, 0, 1);

	float3 s = _SunDir;
	float3 s_t = normalize(cross(float3(0, -1, 0), s));
	if (s_t.x < 0) s_t = -s_t;
	float3 s_bt = cross(s, s_t);

	float actual_sample_num = clamp(d / _Quality.x, sample_num * 0.2, sample_num);
	actual_sample_num *= max(0.15, saturate(1 - (offset - 8000) / 28000));

	float fade = saturate(1 - offset / 25000);

	float sun = 0;
	float amb = 0;
	float bias = Rand();
	float vDots = dot(v, s);
	float phase = numericalMieFit(vDots);
	float multiScatterPhase = phase + numericalMieFitMultiScatter();
	vDots = 1 - (vDots + 1) / 4;

	float alphaFallback = lerp(0.5, 0.1, _Quality.w);

	float trans = 1;
	float2 av_dis = 0;

	float stepLength = d / actual_sample_num;
	v *= stepLength;
	st += bias * v;
	float maxScatter = 1 - exp(-0.01 * _CloudDensity * stepLength);

	int start_index = 0;
	bool find_hit = false;
	{
		[loop]
		for (int i = 0; i < actual_sample_num; i += 8)
		{
			float3 test = v * i + st;
			if (Cloud_Shape(test, fade) != 0) {
				start_index = max(0, i - 7);
				find_hit = true;
				break;
			}
		}
		if (!find_hit) return float4(0, 0, 0, 1);
	}
	{
		find_hit = false;
		float l_t_cache = 1;
		[loop]
		for (int i = start_index; i < actual_sample_num; i++)
		{
			float3 pos = v * i + st;

			float scatter = Cloud(pos, fade);
			if (scatter != 0) {

				scatter = GetT(scatter, stepLength);
				trans *= scatter;
				scatter = 1 - scatter;

				float l_t = GetT(min(scatter / maxScatter, trans), pos, s, s_t, s_bt, vDots, fade);
				l_t = lerp(l_t_cache, l_t, lerp(0.75, 0.99, _Quality.w));
				l_t_cache = l_t;

				float msPhase = multiScatterPhase;
				float response = trans * scatter * l_t * msPhase;
				sun += response;
				amb += trans * scatter;
				av_dis += float2(i * response, response);
			}
			else l_t_cache = 1;

			if (trans <= alphaFallback) {
				find_hit = true;
				start_index = i + 1;
				break;
			}
		}
	}
	[branch]
	if (find_hit) {
		[loop]
		for (int i = start_index; i < actual_sample_num; i++)
		{
			float3 pos = v * i + st;

			float scatter = Cloud_Simple(pos, fade);
			if (scatter != 0) {

				scatter = GetT(scatter, stepLength);
				trans *= scatter;
				scatter = 1 - scatter;

				float l_t = GetT_Simple(pos, s, s_t, s_bt, vDots, fade);

				float msPhase = multiScatterPhase;
				sun += trans * scatter * l_t * msPhase;
			}
			if (trans <= 0.05) break;
		}
	}

	float4 res = float4(sun, amb, 0, trans);
	
	res.z = av_dis.y != 0 ? start_index + (av_dis.x / max(0.0001, av_dis.y)) * stepLength + offset : 0;

	fade = max(0, 1 - res.z / 50000);
	res.xy *= fade;
	res.a = 1 - (1 - res.a) * fade;
	return res;
}

float CloudShadow(float3 p) {
	float2 cloud_uv = mul(_CloudMat, float4(p, 1)).xy;
	if (any(cloud_uv > 1) || any(cloud_uv < 0)) return 1;
	return tex2Dlod(_CloudShadowMap, float4(cloud_uv, 0, 0)).x;
}

float VolumeLight(float depth, float3 x, float3 x_0, float3 x_1, float3 x_2) {
	float dirSampleNum = 24;
	float3 s = _SunDir;
	bool moon = s.y < -0.05;
	s = moon ? -s : s;
	float res = 0;
	float bias = Rand();
	float trans = 1;
	float last_t = 0;
	float dis = distance(x, x_0);
	for (int i = 0; i < dirSampleNum; i++)
	{
		float t = (i + bias) / dirSampleNum;
		t = -log(1 - (1 - 1 / 2.718281828459) * t);
		if (t * dis > depth) break;
		float3 p = lerp(x, x_0, t);
		float3 p_c = lerp(x_1, x_2, t);

		float delta_t = t - last_t;
		float scatter = Cloud(p_c).x;
		trans *= GetT(scatter, dis * delta_t);

		if (trans < 0.1) break;
		float cloud_shadow = lerp(saturate(1 - (1 - CloudShadow(p_c)) * 2), 1, saturate(Cloud_H(p_c) * 2));
		cloud_shadow *= IntersectSphere(p_c, s, float3(0, 0, 0), planet_radius) == 0 ? 1 : 0;
		//if (i == dirSampleNum - 1) return cloud_shadow;
		float terrian_shadow = 1;// GetShadow(float4(p, 1)).x;
		terrian_shadow = moon ? 1 : terrian_shadow;
		res += trans * terrian_shadow * cloud_shadow * delta_t;
		last_t = t;
	}

	return res;
}

#endif