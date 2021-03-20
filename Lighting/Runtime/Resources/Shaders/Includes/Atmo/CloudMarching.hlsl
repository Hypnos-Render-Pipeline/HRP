#ifndef CLOUD_MARCHING_H_
#define CLOUD_MARCHING_H_


#include "./Atmo.hlsl"

float _CloudCoverage;
float _CloudDensity;

float4 _Quality;

float4x4 _CloudMat;
float4x4 _CloudMat_Inv;
float4x4 _LightTransform;

float	  _CloudMapScale;
sampler2D _CloudShadowMap;
Texture2D _CloudMap;		SamplerState Bilinear_Mirror_Sampler;
sampler2D _HighCloudMap;
sampler2D _SpaceMap;
sampler3D _WorleyPerlinVolume;
sampler3D _WorleyVolume;
sampler2D _CurlNoise;

int _Clock;
float4 _Time;

#ifndef planet_radius
#define planet_radius 6371e3
#endif
static float2 cloud_radi = float2(1500, 4000);

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
	sampleIndex = (uint)_Clock % 1024 + hash((seed.x % 8 + seed.y % 8 * 8) / 64.) * 64;
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
	float a = (0.5 - abs(t - 0.5)) * 0.2 + 0.1;
	float b = lerp(0.2, 0.6, t);
	float c = lerp(0.3, 1, t);

	return rescale01(h, 0, a) * rescale10(h, b, c);
}

float3 Space(float3 v) {
	v = mul(_LightTransform, float4(v, 0)).xyz;
	float3 normalizedCoords = normalize(v);
	float latitude = acos(normalizedCoords.y);
	float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
	float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / 3.14159265359f, 1.0 / 3.14159265359f);
	return tex2Dlod(_SpaceMap, float4(float2(0.5, 1.0) - sphereCoords, 0, 0)).xyz * 0.2;
}

inline float4 HighCloud(const float3 p, const float3 v) {
	float dis = IntersectSphere(p, v, float3(0, 0, 0), planet_radius + cloud_radi.y + 2000);

	float3 hitP = p + dis * v;
	float3 nom_p = normalize(hitP);

	float data1 = tex2Dlod(_HighCloudMap, float4(nom_p.xz * 200 + _Time.x * 0.02, 0, 0)).x;

	float data2 = saturate(dis / 4000 - 1) * tex2Dlod(_HighCloudMap, float4(nom_p.xz * 100 + float2(-1, 1) * _Time.x * 0.01, 0, 0)).y;

	float control = tex2Dlod(_HighCloudMap, float4(nom_p.xz * 300 - _Time.x * 0.02, 0, 0)).z;

	return float4(max(data1 * control, data2), hitP);
}

inline float HeightDensity(float h, float y) {
	return smoothstep(0.1, lerp(0.5, 0.2, y), h);
}

inline float Cloud_Shape(float3 p, float fade = 1, float lod = 0) {

	// get height fraction  (be sure to create a cloud_min_max variable)
	float height_fraction = Cloud_H(p);
	float oh = height_fraction;

	height_fraction *= 0.8;

	// wind settings
	float3 wind_direction = float3(1.0, 0.0, 0.0);
	float cloud_speed = 600.0;

	// cloud_top offset - push the tops of the clouds along this wind direction by this many units.
	float cloud_top_offset = 500.0;

	// skew in wind direction
	p += height_fraction * wind_direction * cloud_top_offset;

	//animate clouds in wind direction and add a small upward bias to the wind direction
	p += (wind_direction + float3(0.0, 0.1, 0.0)) * _Time.x * cloud_speed;

	// read the low frequency Perlin-Worley and Worley noises
	float4 low_frequency_noises = tex3Dlod(_WorleyPerlinVolume, float4(p * .00018333, lod));

	// build an fBm out of  the low frequency Worley noises that can be used to add detail to the Low frequency Perlin-Worley noise
	float low_freq_fBm = dot(low_frequency_noises.gba, float3(0.625, 0.25, 0.125));

	// define the base cloud shape by dilating it with the low frequency fBm made of Worley noise.
	float base_cloud = rescale01(low_frequency_noises.r, low_freq_fBm, 1.0);

	float3 nom_p = normalize(p);
	float2 weather_data = _CloudMap.SampleLevel(Bilinear_Mirror_Sampler, nom_p.xz * 20 * _CloudMapScale + 0.5, lod).rg;
	base_cloud = lerp(1, base_cloud, fade);
	weather_data = lerp(1, weather_data, fade);

	// Get the density-height gradient using the density-height function (not included)
	float density_height_gradient = CloudType(height_fraction, weather_data.y);

	// apply the height function to the base cloud shape
	base_cloud *= density_height_gradient;

	// cloud coverage is stored in the weather_data’s red channel.
	float cloud_coverage = weather_data.r;
	cloud_coverage = rescale01(cloud_coverage, 1 - _CloudCoverage, 1);

	// apply anvil deformations
	cloud_coverage = pow(max(0, cloud_coverage), lerp(1, 0.2, min(1, height_fraction * 5) * weather_data.x));

	//Use remapper to apply cloud coverage attribute
	float base_cloud_with_coverage = rescale01(base_cloud, 1 - cloud_coverage, 1.0) * cloud_coverage;

	return base_cloud_with_coverage > 0.01;
}

inline float2 Cloud(float3 p, float fade = 1, float lod = 0, bool simple = false) {

	// get height fraction  (be sure to create a cloud_min_max variable)
	float height_fraction = Cloud_H(p);
	float oh = height_fraction;

	height_fraction *= 0.8;

	// wind settings
	float3 wind_direction = float3(1.0, 0.0, 0.0);
	float cloud_speed = 600.0;

	// cloud_top offset - push the tops of the clouds along this wind direction by this many units.
	float cloud_top_offset = 500.0;

	// skew in wind direction
	p += height_fraction * wind_direction * cloud_top_offset;

	//animate clouds in wind direction and add a small upward bias to the wind direction
	p += (wind_direction + float3(0.0, 0.1, 0.0)) * _Time.x * cloud_speed;

	// read the low frequency Perlin-Worley and Worley noises
	float4 low_frequency_noises = tex3Dlod(_WorleyPerlinVolume, float4(p * .00018333, lod));

	// build an fBm out of  the low frequency Worley noises that can be used to add detail to the Low frequency Perlin-Worley noise
	float low_freq_fBm = dot(low_frequency_noises.gba, float3(0.625, 0.25, 0.125));

	// define the base cloud shape by dilating it with the low frequency fBm made of Worley noise.
	float base_cloud = rescale01(low_frequency_noises.r, low_freq_fBm, 1.0);

	float3 nom_p = normalize(p);
	float2 weather_data = _CloudMap.SampleLevel(Bilinear_Mirror_Sampler, nom_p.xz * 20 * _CloudMapScale + 0.5, lod).rg;
	base_cloud = lerp(1, base_cloud, fade);
	weather_data = lerp(float2(1, 0.2), weather_data, fade);
	
	//return weather_data.r * weather_data.g * 0.001;

	// Get the density-height gradient using the density-height function (not included)
	float density_height_gradient = CloudType(height_fraction, weather_data.y);

	// apply the height function to the base cloud shape
	base_cloud *= density_height_gradient;

	// cloud coverage is stored in the weather_data’s red channel.
	float cloud_coverage = weather_data.r;
	cloud_coverage = rescale01(cloud_coverage, 1 - _CloudCoverage, 1);

	// apply anvil deformations
	cloud_coverage = pow(max(0, cloud_coverage), lerp(1, 0.2, min(1, height_fraction * 5) * weather_data.x));

	//Use remapper to apply cloud coverage attribute
	float base_cloud_with_coverage = rescale01(base_cloud, 1 - cloud_coverage, 1.0);

	//Multiply result by cloud coverage so that smaller clouds are lighter and more aesthetically pleasing.
	base_cloud_with_coverage *= cloud_coverage;

	//define final cloud value
	float final_cloud = base_cloud_with_coverage;

	// only do detail work if we are taking expensive samples!
	if (!simple)
	{

		// add some turbulence to bottoms of clouds using curl noise.  Ramp the effect down over height and scale it by some value (200 in this example)
		float2 curl_noise = tex2Dlod(_CurlNoise, float4(p.xz * 0.000528, 0, 0)).rg;
		p.xz += curl_noise * (1.0 - height_fraction) * 240;

		// sample high-frequency noises
		float3 high_frequency_noises = tex3Dlod(_WorleyVolume, float4 (p * 0.002, lod)).rgb;

		// build High frequency Worley noise fBm
		float high_freq_fBm = dot(high_frequency_noises, float3(0.625, 0.25, 0.125));

		// transition from wispy shapes to billowy shapes over height
		float high_freq_noise_modifier = lerp(high_freq_fBm, 1.0 - high_freq_fBm, saturate(height_fraction * 10.0));

		// erode the base cloud shape with the distorted high frequency Worley noises.
		final_cloud = rescale01(base_cloud_with_coverage, high_freq_noise_modifier * 0.5, 1.0);

	}

	return float2(final_cloud * HeightDensity(height_fraction, weather_data.y) * 0.05 * _CloudDensity * lerp(0.1, 1, weather_data.y), oh);
}

float4 CloudRender(float3 camP, float3 p, float3 v, out float cloud_dis, float d = 9999999) {
	cloud_dis = 0;
	const float max_dis = lerp(8000, 24000, _Quality.w);
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

	float fade = saturate(1 - (offset - 8000) / 14000);

	float lod = (offset - 8000) / 14000 * 4;
	//if (offset > 8000) return 1;
	float sun = 0;
	float amb = 0;
	float trans = 1;
	float2 av_dis = 0;
	bool first_hit = true;
	float2 hit_h = 0;
	
	float VdotS = dot(v, s);
	float phase = numericalMieFit(VdotS);
	float multiScatterPhase = phase + numericalMieFitMultiScatter();
	float InvVDotS01 = 1 - (VdotS + 1) / 2;
	float sunHeight = rescale01(dot(normalize(p), s), -0.4, 0.4);
	float rescaledPdotS01 = rescale01(dot(normalize(p), s), 0, 0.3);

	float alphaFallback = lerp(0.3, 0.05, _Quality.w);


	float stepLength = d / actual_sample_num;
	v *= stepLength;
	st += Rand() * v;
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
		//cloud_dis = 0;
		//return 1;
		//float extra_dis = start_index * stepLength;

		//st += v * start_index;
		//offset += extra_dis;
		//float scale = (d - extra_dis) / d;
		//v *= scale;
		//stepLength *= scale;
	}
	{
		int shadow_sample_num = _Quality.y;
		find_hit = false;
		[loop]
		for (int i = start_index; i < actual_sample_num; i++)
		{
			float3 pos = v * (i + Rand()) + st;

			float2 sh = Cloud(pos, fade, lod);
			float scatter = sh.x;
			if (scatter != 0) {

				scatter = exp(-scatter * stepLength);
				trans *= scatter;
				scatter = 1 - scatter;

				float depth_probability = pow(max(0, sh.x * 150), remap(sh.y, 0.3, 0.85, 0.3, 2));
				float vertical_probability = pow(remap(sh.y, 0.07, 0.14, 0.1, 1.0), 0.8);
				float in_scatter_probability = depth_probability * vertical_probability;

				float energy;
				{
					float shadowLength = 1200;
					float shadowScatter = 0;
					float inv_sample_num = 1.0 / shadow_sample_num;
					shadowLength *= inv_sample_num;
					float3 v_ = s * shadowLength;
					float3 vt_ = s_t * shadowLength * 0.5;
					float3 vbt_ = s_bt * shadowLength * 0.5;
					for (int j = 0; j < shadow_sample_num; j++)
					{
						float3 lpos_ = v_ * (j + Rand()) + pos;

						float2 rnd = frac(Roberts2_(j) + Rand());
						rnd.x *= (2 * 3.14159265359);
						float2 offset; sincos(rnd.x, offset.x, offset.y);
						offset *= rnd.y * j;
						lpos_ += vt_ * offset.x + vbt_ * offset.y;
						shadowScatter += Cloud(lpos_, fade, max(lod, (float)j / shadow_sample_num * 4)).x;
					}
					float forward_scatter = exp(-shadowScatter * shadowLength);					
					energy = pow(forward_scatter, 4) * 2 * (1 - InvVDotS01) + lerp(smoothstep(0.2, 0.7, sh.y) * 0.5 + 0.5, 1, smoothstep(0, 0.7, InvVDotS01)) * max(exp(-0.2 * shadowScatter * shadowLength) * 0.7, forward_scatter);
				}

				float light = energy;
				float msPhase = multiScatterPhase;
				float response = trans * scatter;
				
				sun += response * msPhase * light * in_scatter_probability;
				amb += lerp(1, 0.3 + 0.7 * smoothstep(0.2, 0.7, sh.y), sunHeight) * response * lerp(1, in_scatter_probability, rescaledPdotS01);
				hit_h += float2(sh.y * response, response);				
				av_dis += float2(i * response, response);
			}

			if (trans <= alphaFallback) {
				find_hit = true;
				start_index = i + 1;
				break;
			}
		}
	}
	if (find_hit)
	{
		int shadow_sample_num = _Quality.y / 2;
		[loop]
		for (int i = start_index; i < actual_sample_num; i++)
		{
			float3 pos = v * (i + Rand()) + st;

			float2 sh = Cloud(pos, fade, lod);
			float scatter = sh.x;
			if (scatter != 0) {

				scatter = exp(-scatter * stepLength);
				trans *= scatter;
				scatter = 1 - scatter;

				float depth_probability = pow(max(0, sh.x * 150), remap(sh.y, 0.3, 0.85, 0.3, 2));
				float vertical_probability = pow(remap(sh.y, 0.07, 0.14, 0.1, 1.0), 0.8);
				float in_scatter_probability = depth_probability * vertical_probability;

				float energy;
				{
					float shadowLength = 1200;
					float shadowScatter = 0;
					float inv_sample_num = 1.0 / shadow_sample_num;
					shadowLength *= inv_sample_num;
					float3 v_ = s * shadowLength;
					float3 vt_ = s_t * shadowLength * 0.5;
					float3 vbt_ = s_bt * shadowLength * 0.5;
					for (int j = 0; j < shadow_sample_num; j++)
					{
						float3 lpos_ = v_ * (j + Rand()) + pos;

						float2 rnd = frac(Roberts2_(j) + Rand());
						rnd.x *= (2 * 3.14159265359);
						float2 offset; sincos(rnd.x, offset.x, offset.y);
						offset *= rnd.y * j;
						lpos_ += vt_ * offset.x + vbt_ * offset.y;
						shadowScatter += Cloud(lpos_, fade, max(lod, (float)j / shadow_sample_num * 4)).x;
					}
					float forward_scatter = exp(-shadowScatter * shadowLength);
					energy = pow(forward_scatter, 4) * 2 * (1 - InvVDotS01) + lerp(smoothstep(0.2, 0.7, sh.y) * 0.5 + 0.5, 1, smoothstep(0, 0.7, InvVDotS01)) * max(exp(-0.2 * shadowScatter * shadowLength) * 0.7, forward_scatter);
				}

				float light = energy;
				float msPhase = multiScatterPhase;
				float response = trans * scatter;

				sun += response * msPhase * light * in_scatter_probability;
				amb += lerp(1, 0.3 + 0.7 * smoothstep(0.2, 0.7, sh.y), sunHeight) * response * lerp(1, in_scatter_probability, rescaledPdotS01);
				hit_h += float2(sh.y * response, response);
				av_dis += float2(i * response, response);
			}
			if (trans <= alphaFallback / 5) {
				break;
			}
		}
	}


	float4 res = float4(sun, amb, 0, trans);

	cloud_dis = av_dis.y != 0 ? start_index + (av_dis.x / max(0.0001, av_dis.y)) * stepLength + offset : 0;

	fade = saturate(1 - (cloud_dis - 10000) / 40000);
	res.xy *= fade;
	res.a = 1 - rescale10(res.a, alphaFallback / 5, 1) * fade;
	res.z = hit_h.y != 0 ? hit_h.x / hit_h.y : 0;

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