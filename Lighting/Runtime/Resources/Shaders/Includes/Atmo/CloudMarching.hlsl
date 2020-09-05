
#ifndef CLOUD_MARCHING_H_
#define CLOUD_MARCHING_H_

//#include "../../../Atmo/Resources/Shaders/BuildinShadowHelper.cginc"

float3 _LightDir;

float _CloudCoverage;
float _CloudDensity;
float _CloudTexScale;

float4x4 _CloudMat;
float4x4 _CloudMat_Inv;
sampler2D _CloudShadowMap;


sampler2D _WeatherTex;
sampler2D _CurlNoise;
sampler3D _WorleyPerlinVolume;
sampler3D _WorleyVolume;

#ifndef planet_radius
#define planet_radius 6371e3
#endif
static float3 cloud_radi = float3(2500, 5000, 6500);

uint sampleIndex = 0;
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
	sampleIndex = ((seed.x + _Clock) % 3 + seed.y % 3 * 3);
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

float GetT(float s, float t) {
	return exp(-t * s);
}
float GetPhase(float g, float dot) {
	return (1 - g * g) / pow(1 + g * g - 2 * g * dot, 1.5) * 0.25;
}

float remap(float v, float a, float b, float c, float d) {
	return (clamp(v, a, b) - a) / max(0.0001, b - a) * (d - c) + c;
}

float hash(float3 p)  // replace this by something better
{
	p = frac(p*0.3183099 + .1);
	p *= 17.0;
	return frac(p.x*p.y*p.z*(p.x + p.y + p.z));
}

float noise(in float3 x)
{
	float3 i = floor(x);
	float3 f = frac(x);
	f = f * f*(3.0 - 2.0*f);

	return lerp(lerp(lerp(hash(i + float3(0, 0, 0)),
		hash(i + float3(1, 0, 0)), f.x),
		lerp(hash(i + float3(0, 1, 0)),
			hash(i + float3(1, 1, 0)), f.x), f.y),
		lerp(lerp(hash(i + float3(0, 0, 1)),
			hash(i + float3(1, 0, 1)), f.x),
			lerp(hash(i + float3(0, 1, 1)),
				hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

// Hash functions by Dave_Hoskins
float hash12(float2 p)
{
	uint2 q = uint2(int2(p)) * uint2(1597334673U, 3812015801U);
	uint n = (q.x ^ q.y) * 1597334673U;
	return float(n) * (1.0 / float(0xffffffffU));
}

float2 hash22(float2 p)
{
	uint2 q = uint2(int2(p)) * uint2(1597334673U, 3812015801U);
	q = (q.x ^ q.y) * uint2(1597334673U, 3812015801U);
	return float2(q) * (1.0 / float(0xffffffffU));
}

float perlin(float2 uv) {
	float2 id = floor(uv);
	float2 gv = frac(uv);

	// Four corners in 2D of a tile
	float a = hash12(id);
	float b = hash12(id + float2(1.0, 0.0));
	float c = hash12(id + float2(0.0, 1.0));
	float d = hash12(id + float2(1.0, 1.0));

	float2 u = gv * gv * (3.0 - 2.0 * gv);

	return lerp(a, b, u.x) +
		(c - a) * u.y * (1.0 - u.x) +
		(d - b) * u.x * u.y;
}

float2 curl(float2 uv)
{
	float2 eps = float2(0., 1.);

	float n1, n2, a, b;
	n1 = perlin(uv + eps);
	n2 = perlin(uv - eps);
	a = (n1 - n2) / (2. * eps.y); // ∂x1/∂y

	n1 = perlin(uv + eps.yx);
	n2 = perlin(uv - eps.yx);
	b = (n1 - n2) / (2. * eps.y); // ∂y1/∂x

	return float2(a, -b);
}

float3 curl3(float3 uv)
{
	return float3(curl(uv.xy), curl(uv.zx + float2(0.129, 0.753)).x);
}

float Cloud_H(float3 p) {
	return (length(p) - (planet_radius + cloud_radi.x)) / (cloud_radi.y - cloud_radi.x);
}

bool Cloud_Simple(float3 p, float mip = 0) {
	float h = Cloud_H(p);

	float3 nom_p = normalize(p);
	float a = tex2Dlod(_WeatherTex, float4((nom_p.xz * 100 + _Time.x * 0.004) * _CloudTexScale, 0, mip)).x;

	[branch]
	if (h < 0 || h > 1 || a < max(1 - _CloudCoverage, 0.001)) return false;
	return true;
}

float CloudType(float h, float type) {
	return saturate(0.201 + 0.799 * type - h) / (0.201 + 0.799 * type)* remap(h, 0, 0.1 + (0.5 - abs(type - 0.5)) * 0.1, 0, 1) * remap(h, 0.2 + 0.6 * type, 0.201 + 0.799 * type, 1, 0);
}

float2 Cloud(float3 p, float lod = 0) {
	float h = Cloud_H(p);
	if (h > 1 || h < 0) return float2(0, h);

	float3 nom_p = normalize(p);
	float3 cloud = tex2Dlod(_WeatherTex, float4((nom_p.xz * 100  +_Time.x * 0.004) * _CloudTexScale, 0, lod));
	float cov = cloud.x;
	float type = cloud.y;
	cov = remap(cov, 1 - _CloudCoverage, 1, 0, 1);

	float3 low_frequency_noises = tex3Dlod(_WorleyPerlinVolume, float4(p * 0.0008 * 0.37, 0));


	float low_freq_FBM = dot(low_frequency_noises, float3(0.625, 0.25, 0.125));

	float base_cloud = remap(low_frequency_noises.r, -(1.0 - low_freq_FBM),
		1.0, 0.0, 1.0);

	float sa = CloudType(h, type) * cov * lerp(0.8, 1, type);

	float base_cloud_with_coverage = remap(base_cloud, 1 - sa, 1.0, 0.0, 1.0);

	base_cloud_with_coverage *= lerp(0.5, 1, type);

	if (lod > 3) return float2(base_cloud_with_coverage * lerp(0.2, 1, type) * _CloudDensity, h);

	float3 high_frequency_noises = tex3Dlod(_WorleyVolume, 
			float4(p * 0.004 + 	tex2Dlod(_CurlNoise, float4((p.xz + _Time.x * 100) * 0.0012, 0, 0)).rgb * h * 0.75,
			0)
	);
	float high_freq_FBM = dot(high_frequency_noises, float3(0.625, 0.25, 0.125)) * (1 - h * 0.5);

	float high_freq_noise_modifier = lerp(high_freq_FBM, 1.0 - high_freq_FBM, saturate(h * 10));

	float final_cloud = remap(base_cloud_with_coverage, high_freq_noise_modifier * 0.2,
		1.0, 0.0, 1.0);

	return float2(final_cloud * lerp(0.2, 1, type) * _CloudDensity, h);
}

float GetT(float samples, float3 p, float3 v, float d = 800) {

	float sample_num = max(2, floor(36 * samples));
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

		float scatter;
		scatter = Cloud(pos, lerp(4, 0, trans));
		trans *= GetT(scatter, (t - last_t) * d);
		last_t = t;
	}
	return trans;
}

float GetT(float3 p, float3 v, float d = 800) {
	float sample_num = 8;
	float bias = Rand();
	float trans = 1;
	float last_t = 0;
	float far_dis = lerp(d, 1600, Rand());
	float far_t = GetT(Cloud(p + v * far_dis), far_dis);
	v *= d;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float t = (i + bias) / sample_num;
		t = -log(1 - (1 - 1 / 2.718281828459) * t);
		float3 pos = v * t + p;
		float3 disc_bias = UniformSampleSphere(Roberts2_(i)) * pow(Roberts1_(i), 1/3.0f) * t * d;
		pos += disc_bias;
		float scatter = Cloud(pos);
		if (scatter < 0.00001) {
			last_t = t;
			i++;
			continue;
		}
		trans *= GetT(scatter, (t - last_t) * d);
		last_t = t;
	}
	return trans * far_t;
}
float _A;
float4 CloudRender(float3 camP, float3 p, float3 v, float d = 9999999) {

	float3 st = 0;
	float offset = 0;

	float dis = 12000;
	const float sample_num = 160;

	float h = Cloud_H(p);

	if (h > 1) {
		offset = IntersectSphere(p, v, float3(0, 0, 0), planet_radius + cloud_radi.y);
		st = p + v * offset;
		dis =IntersectSphere(st, v, float3(0, 0, 0), planet_radius + cloud_radi.x);
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
		if (IntersectSphere(p, v, float3(0, 0, 0), planet_radius) > 0)
			return float4(0, 0, 0, 1);
		offset = IntersectSphere(p, v, float3(0, 0, 0), planet_radius + cloud_radi.x);
		st = offset * v + p;
		dis = min(dis, IntersectSphere(st, v, float3(0, 0, 0), planet_radius + cloud_radi.y));
	}

	d -= offset;
	d = min(d, dis);

	if (d < 0) return float4(0, 0, 0, 1);

	float3 s = normalize(-_LightDir);
	bool moon = s.y < -0.05;
	s = moon ? -s : s;
	s = s.y < -0.05 ? -s : s;
	float3 s_t = normalize(s.y > 0.5 ? cross(float3(1, 0, 0), s) : cross(float3(0, 1, 0), s));
	float3 s_bt = cross(s, s_t);

	/*float fade = saturate(1 - offset / 40000);*/
	float step_ = max(d / sample_num, 4);
	/*float max_dis = 16 * sample_num;*/
	float step = step_;
	float sun = 0;
	float amb = 0;
	dis = Rand() * step;
	float phase = (GetPhase(0.65, dot(v, s)) + GetPhase(0.15, dot(v, s))) * 1.4 + 1.2;

	float trans = 1;
	float trans2 = 1;
	int skiping = 0;
	float dis_in_cloud = 0;
	[loop]
	for (int i = 0; i < sample_num; i++)
	{
		float3 pos = st + dis * v;
		bool in_cloud = true;
		[branch]
		if (skiping >= 3) {
			in_cloud = Cloud_Simple(pos, 1);
			skiping = in_cloud ? 0 : min(6, skiping + 1);
			dis_in_cloud = 0;
			dis += in_cloud ? 0 : step;
		}
		[branch]
		if (in_cloud) {
			float2 s_h = Cloud(pos);
			float scatter = s_h.x/* * fade * (max_dis - dis) / max_dis*/;
			if (scatter < 0.00001) {
				skiping++;
				dis_in_cloud = 0;
			}
			else {
				dis_in_cloud += step;
				trans *= GetT(scatter, step);
				trans2 *= GetT(scatter, 2 * step);
				float2 shadow = 1;// GetShadow(float4(camP + pos - p, 1));
				shadow = moon ? 1 : shadow;
				float l_t = GetT(pos, s, max(lerp(600, 100, saturate(scatter)), dis_in_cloud)) * lerp(shadow.x, 1, saturate(shadow.y));;
				amb += trans * (1 - trans2) * scatter * step * saturate(s_h.y + 0.3);
				sun += trans * (1 - trans2) * scatter * step * l_t * phase;
			}
		}
		if (trans <= 0.001 || dis > d) break;
		//float t = ((i + 1) / sample_num);
		//step = step_ * ( 2 * t * t + 1.0f/3.0f);
		dis += step;
	}

	float3 sun_trans = pow(float3(sun, amb, trans), 1 / 2.2f);
	return float4(sun_trans.x, sun_trans.y, 0, sun_trans.z);
}

float CloudShadow(float3 p) {
	float2 cloud_uv = mul(_CloudMat, float4(p, 1)).xy;
	if (any(cloud_uv > 1) || any(cloud_uv < 0)) return 1;
	return tex2Dlod(_CloudShadowMap, float4(cloud_uv, 0, 0));
}

float VolumeLight(float depth, float3 x, float3 x_1, float3 v) {
	float3 s = normalize(-_LightDir);
	bool moon = s.y < -0.05;
	s = moon ? -s : s;
	float bias = Rand();
	float trans = 1;
	float last_t = 0;

	float dirSampleNum = 96;
	float dis = 2048;
	float3 x_0 = x + v * dis;
	float res1 = 0;
	{
		for (int i = 0; i < dirSampleNum; i++)
		{
			float t = (i + bias) / dirSampleNum;
			t = -log(1 - (1 - 1 / 2.718281828459) * t);
			if (t * dis > depth) break;
			float3 p = lerp(x, x_0, t);

			float delta_t = t - last_t;

			float terrian_shadow = 1;// GetShadow(float4(p, 1)).x;
			terrian_shadow = moon ? 1 : terrian_shadow;
			res1 += terrian_shadow * delta_t;
			last_t = t;
		}
	}
	last_t = 0;
	dirSampleNum = 24;
	dis = 4096;
	float3 x_2 = x_1 + v * dis;
	float res2 = 0;
	{
		for (int i = 0; i < dirSampleNum; i++)
		{
			float t = (i + bias) / dirSampleNum;
			t = -log(1 - (1 - 1 / 2.718281828459) * t);
			float3 p_c = lerp(x_1, x_2, t);

			float delta_t = t - last_t;
			float scatter;
			scatter = Cloud(p_c);
			trans *= GetT(scatter, dis * delta_t);

			if (trans < 0.1) break;
			float cloud_shadow = lerp(saturate(1 - (1 - CloudShadow(p_c)) * 2), 1, saturate(Cloud_H(p_c)));
			//cloud_shadow *= IntersectSphere(p_c, s, float3(0, 0, 0), planet_radius) == 0 ? 1 : 0;

			res2 += trans * cloud_shadow * delta_t;
			last_t = t;
		}
	}


	return res1 * res2;
}

#endif