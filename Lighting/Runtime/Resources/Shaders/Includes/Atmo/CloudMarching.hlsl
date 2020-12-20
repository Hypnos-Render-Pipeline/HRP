#ifndef CLOUD_MARCHING_H_
#define CLOUD_MARCHING_H_


#include "./Atmo.hlsl"

float _CloudCoverage;
float _CloudDensity;

float4x4 _CloudMat;
float4x4 _CloudMat_Inv;
sampler2D _CloudShadowMap;

sampler2D _CloudMap;

sampler3D _WorleyPerlinVolume;
sampler3D _WorleyVolume;
int _Clock;
float4 _Time;

#ifndef planet_radius
#define planet_radius 6371e3
#endif
static float2 cloud_radi = float2(1500, 2700);

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
	sampleIndex = (uint)_Clock % 16 + hash((seed.x % 8 + seed.y % 8 * 8) / 64.) * 64;
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

float Cloud_H(float3 p) {
	return (length(p) - (planet_radius + cloud_radi.x)) / (cloud_radi.y - cloud_radi.x);
}

float CloudType(float h, float t) {
	float a = lerp(0.01, 0.2, t);
	float b = lerp(0.15, 0.4, t);
	float c = lerp(0.1, 1, t);

	return remap(h, 0, a, 0, 1) * remap(h, b, b + c * 0.7, 1, 0) * t;
}

float2 Cloud(float3 p) {
	float h = Cloud_H(p);

	h /= 1.2f;

	float3 nom_p = normalize(p);
	float cov = tex2Dlod(_CloudMap, float4(nom_p.xz * 100 + _Time.x * 0.004, 0, 0)).x;

	cov = remap(cov, 1 - _CloudCoverage, 1, 0, 1);

	float ero_value = 1 - CloudType(h, cov);

	float noise0 = dot(tex3Dlod(_WorleyPerlinVolume, float4(p * 0.00057, 0)).xyz, float3(0.625, 0.25, 0.125));
	//noise0 = lerp(1 - noise0, noise0, saturate(h * 8));

	float noise1 = dot(tex3Dlod(_WorleyVolume, float4(p * 0.006, 0)).xyz, float3(0.625, 0.25, 0.125));

	noise1 = lerp(noise1, 1 - noise1, min(1, h * 3));

	float cloud = remap(noise0, ero_value, 1, 0, 1);

	cloud = cloud - (cloud < 0.4 && cloud > 0 ? remap(noise1, 0.2 + 2 * cloud, 1, 0, 1) : 0);

	return (cloud > 0) ? sqrt(cov) * saturate(h * 2) * 0.01 * _CloudDensity : 0;
}

float2 Cloud_Simple(float3 p) {
	float h = Cloud_H(p);

	h /= 1.2f;

	float3 nom_p = normalize(p);
	float cov = tex2Dlod(_CloudMap, float4(nom_p.xz * 100 + _Time.x * 0.004, 0, 0)).x;

	cov = remap(cov, 1 - _CloudCoverage, 1, 0, 1);

	float ero_value = 1 - CloudType(h, cov);

	float cloud = remap(0.784, ero_value, 1, 0, 1);

	return (cloud > 0) ? sqrt(cov) * saturate(h * 3) * 0.01 * _CloudDensity : 0;
}

float GetT(float samples, float3 p, float3 v, float d = 800) {

	float sample_num = max(4, floor(8 * samples));
	float bias = Rand();
	float trans = 1;
	float last_t = 0;
	v *= d;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = (i + bias) / sample_num;
		t = -0.333333 * log(1 - 0.9502129 * t);
		float3 pos = v * t + p;
		float scatter = Cloud(pos).x;
		trans *= GetT(scatter, (t - last_t) * d);
		last_t = t;
	}
	return trans;
}

float GetT(float samples, float3 p, float3 v, float3 vt, float3 vbt, float d = 400) {

	float sample_num = lerp(3, 6, samples);
	float bias = Rand();
	float trans = 1;
	float last_t = 0;
	v *= d;
	vt *= d * 0.3;
	vbt *= d * 0.3;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = (i + bias) / sample_num;
		//t = -0.333333 * log(1 - 0.9502129 * t);
		float3 pos = v * t + p;

		float2 rnd = frac(Roberts2_(i) + bias);
		rnd.x *= 2 * 3.14159265359;
		float2 offset = float2(sin(rnd.x), cos(rnd.x)) * rnd.y * t;
		pos += vt * offset.x + vbt * offset.y;
		float scatter = Cloud(pos).x;
		trans *= GetT(scatter, (t - last_t) * d);
		last_t = t;
	}
	return trans;
}

float GetT_Simple(float3 p, float3 v, float3 vt, float3 vbt, float d = 400) {

	float sample_num = 2;
	float bias = Rand();
	float trans = 1;
	float last_t = 0;
	v *= d;
	vt *= d * 0.3;
	vbt *= d * 0.3;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = (i + bias) / sample_num;
		//t = -0.333333 * log(1 - 0.9502129 * t);
		float3 pos = v * t + p;

		float2 rnd = frac(Roberts2_(i) + bias);
		rnd.x *= 2 * 3.14159265359;
		float2 offset = float2(sin(rnd.x), cos(rnd.x)) * rnd.y * t;
		pos += vt * offset.x + vbt * offset.y;
		float scatter = Cloud_Simple(pos).x;
		trans *= GetT(scatter, (t - last_t) * d);
		last_t = t;
	}
	return trans;
}

float4 CloudRender(float3 camP, float3 p, float3 v, float d = 9999999) {

	const float max_dis = 4000;
	const float sample_num = 96;

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

	if (d < 0 || offset > 10000) return float4(0, 0, 0, 1);

	float3 s = _SunDir;
	float3 s_t = normalize(cross(float3(0, -1, 0), s));
	if (s_t.x < 0) s_t = -s_t;
	float3 s_bt = cross(s, s_t);

	float fade = max(0, 1 - offset / 6000);
	float actual_sample_num = sample_num * (0.5 + fade * 0.5);

	float sun = 0;
	float amb = 0;
	float bias = Rand();
	float vDots = dot(v, s);
	float phase = numericalMieFit(vDots);
	float multiScatterPhase = phase + numericalMieFitMultiScatter();
	vDots = (vDots + 1) / 2;

	float trans = 1;
	float trans2 = 1;
	float2 av_dis = 0;

	float stepLength = d / actual_sample_num;
	v *= stepLength;
	st += bias * v;

	int start_index = 0;
	bool find_hit = false;
	[loop]
	for (int i = 0; i < actual_sample_num; i+=6)
	{
		float3 test = v * i + st;
		if (Cloud_Simple(test).x != 0) {
			start_index = max(0, i - 5);
			find_hit = true;
			break;
		}
	}
	if (!find_hit) return float4(0, 0, 0, 1);
	find_hit = false;
	[loop]
	for (int i = start_index; i < actual_sample_num; i++)
	{
		float3 pos = v * i + st;

		float2 s_h = Cloud(pos);
		float scatter = s_h.x;
		if (scatter != 0) {

			trans *= GetT(scatter, stepLength);
			trans2 *= GetT(scatter * 4, stepLength);
			float l_t = GetT(trans, pos, s, s_t, s_bt);

			scatter = 1 - exp(-scatter * stepLength);

			float msPhase = multiScatterPhase;
			float response = trans * lerp(1, (1 - trans2), 1-vDots) * scatter * l_t * msPhase;
			sun += response;
			amb += trans * (1 - trans2) * scatter;
			av_dis += float2(i * response, response);
		}
		if (trans <= 0.3) {
			find_hit = true;
			start_index = i + 1;
			break;
		}
	}

	[branch]
	if (find_hit) {
		[loop]
		for (int i = start_index; i < actual_sample_num; i++)
		{
			float3 pos = v * i + st;

			float2 s_h = Cloud(pos);
			float scatter = s_h.x;
			if (scatter != 0) {

				trans *= GetT(scatter, stepLength);
				trans2 *= GetT(scatter * 4, stepLength);
				float l_t = GetT_Simple(pos, s, s_t, s_bt);

				scatter = 1 - exp(-scatter * stepLength);

				float msPhase = multiScatterPhase;
				sun += trans * scatter * l_t * msPhase;
			}
			if (trans <= 0.05) break;
		}
	}

	float4 res = float4(sun, amb, 0, trans);
	
	fade = max(0, 1 - offset / 10000);
	res.xyz *= fade;
	res.a = 1 - (1 - res.a) * fade;
	
	res.b = av_dis.y != 0 ? start_index + (av_dis.x / max(0.0001, av_dis.y)) * stepLength + offset : 0;
	
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