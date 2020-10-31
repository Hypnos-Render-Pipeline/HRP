#ifndef RTFOG_H_
#define RTFOG_H_

#include "./RayTracingLight.hlsl"
#include "./Sampler.hlsl"


inline float Average(const float2 i) { return dot(i, 1. / 2); }
inline float Average(const float3 i) { return dot(i, 1. / 3); }
inline float Average(const float4 i) { return dot(i, 1. / 4); }

#ifdef _ENABLEFOG

struct SmokeInfo
{
	float2 scale;
	int width;
	int index;
	float g;
	float scatter;
	float absorb;
	float maxDensity;
};

StructuredBuffer<SmokeInfo> _MaterialArray;

Texture2DArray<float> _VolumeAtlas; SamplerState linear_clamp_VolumeAtlas_sampler;
float2 _VolumeAtlasPixelSize;

struct RayIntersection_FogVolume {
	float4 world2Object[3];
	float t;
	int objIndex;
	int materialIndex;
	bool inter;
};

struct VolumeRec {
	float3x4 w2o;
	float2 range;
	int objIndex;
	int materialIndex;
};

struct VolumeRecs {
	int count;
	VolumeRec recs[3];
};

bool TraceFog(float3 pos, float3 dir, out VolumeRec rec) {

	RayDesc rayDescriptor;
	rayDescriptor.Origin = pos;
	rayDescriptor.Direction = dir;
	rayDescriptor.TMin = 0.002;
	rayDescriptor.TMax = 99999;

	RayIntersection rayIntersection;
	rayIntersection.sampleState = 0;
	rayIntersection.t = 99999;
	rayIntersection.weight = TRACE_FOG_VOLUME;

	TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE, 0xFF, 0, 1, 1, rayDescriptor, rayIntersection);

	if (rayIntersection.t.x >= 0) {
		rec.w2o._m00_m10_m20 = rayIntersection.directColor;
		rec.w2o._m01_m11_m21 = rayIntersection.weight.xyz;
		rec.w2o._m02_m12_m22 = rayIntersection.nextDir;
		rec.w2o._m03_m13_m23 = rayIntersection.normal;
		rec.range.x = rayIntersection.t.x;
		rec.materialIndex = rayIntersection.t.y;
		rec.objIndex = rayIntersection.t.z;
		rec.range.y = rayIntersection.t.w;
		return true;
	}
	return false;
}

void RemoveRec(inout VolumeRecs recs, int index) {
	recs.count--;
	recs.recs[index] = recs.recs[recs.count];
}

void AddRec(inout VolumeRecs recs, VolumeRec rec) {
	bool new_flag = true;
	[loop]
	for (int i = 0; i < recs.count; i++)
	{
		if (recs.recs[i].objIndex == rec.objIndex) {
			recs.recs[i].range[rec.range.y] = rec.range.x;
			new_flag = false;
			break;
		}
	}
	if (new_flag) {
		int index = rec.range.y;
		float v = rec.range.x;
		rec.range = -1;
		rec.range[index] = v;
		recs.recs[recs.count++] = rec;
	}
}

float2 InitVolumeRecs(float3 pos, float3 dir, out VolumeRecs recs) {
	recs.count = 0;
	VolumeRec rec;
	float t = 0;
	int k = 20;
	[loop]
	while (k-- > 0 && TraceFog(pos + t * dir, dir, rec)) {
		t += rec.range.x;
		rec.range.x = t;
		AddRec(recs, rec);
	}

	float2 min_max = float2(999999, 0);
	[loop]
	for (int i = 0; i < recs.count; i++)
	{
		if (recs.recs[i].range[1] == -1) {
			RemoveRec(recs, i);
			i--;
		}
		else {
			if (recs.recs[i].range[0] == -1) {
				recs.recs[i].range[0] = 0;
			}
			min_max.x = min(min_max.x, recs.recs[i].range[0]);
			min_max.y = max(min_max.y, recs.recs[i].range[1]);
		}
	}

	return min_max;
}

bool SampleVolume(VolumeRec rec, SmokeInfo si, float3 pos, inout float sigmaS, inout float sigmaE, inout float sigmaTMax, inout float G) {
	float3 volume_uv = mul(rec.w2o, float4(pos, 1));
	sigmaTMax = max(sigmaTMax, si.maxDensity * (si.scatter + si.absorb));
	if (any(abs(volume_uv) > 0.495)) return false;
	volume_uv += 0.5;
	float zslice = volume_uv.z * (si.width >> 16);
	int width = si.width & 0xFFFF;
	int z = floor(zslice);
	float2 offset = _VolumeAtlasPixelSize / 2 + volume_uv.xy * (si.scale - _VolumeAtlasPixelSize);
	float2 uv1 = float2(z % width, z / width) * si.scale; uv1.y = 1 - uv1.y - si.scale.y;
	uv1 += offset;
	z += 1;
	float2 uv2 = float2(z % width, z / width) * si.scale; uv2.y = 1 - uv2.y - si.scale.y;
	uv2 += offset;
	float density1 = _VolumeAtlas.SampleLevel(linear_clamp_VolumeAtlas_sampler, float3(uv1, si.index), 0);
	float density2 = _VolumeAtlas.SampleLevel(linear_clamp_VolumeAtlas_sampler, float3(uv2, si.index), 0);
	float density = max(0, lerp(density1, density2, frac(zslice)));

	float s = sigmaS + si.scatter * density;
	float e = sigmaE + si.absorb * density;
	G = lerp(si.g, G, sigmaS / max(s, 0.0000001));
	sigmaS = s;
	sigmaE = e;
	
	return true;
}

void GetFog(const VolumeRecs recs, float3 pos, inout float t, out float sigmaS, out float sigmaE, out float sigmaTMax, out float G) {
	sigmaS = 0;
	sigmaE = 0;
	sigmaTMax = 0.0001;
	G = 0;
	bool flag = false;
	float jump = 99999;
	[loop]
	for (int i = 0; i < recs.count; i++)
	{
		VolumeRec rec = recs.recs[i];

		jump = min(jump, rec.range.y < t ? 99999 : rec.range.x);

		float3 volume_uv = mul(rec.w2o, pos) + 0.5;
		int materialIndex = rec.materialIndex;

		SmokeInfo si = _MaterialArray[materialIndex];

		if (SampleVolume(rec, si, pos, sigmaS, sigmaE, sigmaTMax, G)) flag = true;
	}
	sigmaE += sigmaS;
	if (!flag) t = max(t, jump);
}

#endif

float Tr(float3 pos, float3 dir, float dis, inout int4 sampleState, float delta = 0.00001) {

#ifndef _ENABLEFOG
	return 1;
#else

	VolumeRecs all_recs;
	float2 min_max = InitVolumeRecs(pos, dir, all_recs); if (all_recs.count == 0) return 1;

	dis = min(dis, min_max.y);

	float sigmaS, sigmaE, sigmaTMax;
	float G;
	float t = min_max.x;
	float tr = 1;

	GetFog(all_recs, pos + t * dir, t, /*out*/sigmaS, /*out*/sigmaE, /*out*/sigmaTMax, /*out*/G);

	int loop_num = 2048;
	[loop]
	while (loop_num-- > 0 && tr > delta) {
		float rk = frac(Roberts1(sampleState.x % 128 * 128 + sampleState.y % 128 + sampleState.z) + SAMPLE);
		t -= log(1 - rk) / sigmaTMax;

		GetFog(all_recs, pos + t * dir, t, /*out*/sigmaS, /*out*/sigmaE, /*out*/sigmaTMax, /*out*/G);

		if (t > dis)
			break;
		
		tr *= 1 - max(0, sigmaE / sigmaTMax);
	}

	return tr;

#endif
}

float3 TraceShadowWithFog(const float3 start, const float3 end,
	inout int4 sampleState) {
	float3 shadow = TraceShadow(start, end, sampleState);

#ifdef _ENABLEFOG
	if (any(shadow != 0)) {
		float3 dir = end - start;
		float len = length(dir);
		dir /= len;
		shadow *= Tr(start, dir, len, sampleState);
	}
#endif
	return shadow;
}

float3 TraceShadowWithFog_PreventSelfShadow(const float3 start, const float3 end,
	inout int4 sampleState) {
	float3 shadow = TraceShadow_PreventSelfShadow(start, end, sampleState);

#ifdef _ENABLEFOG
	if (any(shadow != 0)) {
		float3 dir = end - start;
		float len = length(dir);
		dir /= len;
		shadow *= Tr(start, dir, len, sampleState);
	}
#endif
	return shadow;
}

float3 LightLuminanceWithFog(float3 pos, float3 dir,
	inout int4 sampleState) {
	int light_count = clamp(_LightCount, 0, 100);
	if (light_count == 0) return 0;
	float rnd = SAMPLE; sampleState.w++;
	float3 direct_light = 0;
	{
		Light light = _LightList[floor(min(rnd, 0.99) * light_count)];

		float attenuation;
		float3 lightDir;
		float3 end_point;

		bool in_light_range = ResolveLightWithDir(light, pos, dir,
			/*out*/attenuation, /*out*/end_point);

		[branch]
		if (in_light_range) {
			float3 luminance = attenuation * light.color;
			float3 shadow = TraceShadow(pos, end_point,
				/*inout*/sampleState);

			direct_light = shadow * luminance * light_count;
#ifdef _ENABLEFOG
			if (any(direct_light != 0)) direct_light *= Tr(pos, dir, distance(pos, end_point), sampleState);
#endif
		}
	}
	return direct_light;
}

float3 LightLuminanceCameraWithFog(float3 pos, float3 dir,
	inout int4 sampleState) {
	int light_count = clamp(_LightCount, 0, 100);
	if (light_count == 0) return 0;
	float rnd = SAMPLE; sampleState.w++;
	float3 direct_light = 0;
	{
		Light light = _LightList[floor(min(rnd, 0.99) * light_count)];

		if (light.type <= SPOT) return 0;

		float attenuation;
		float3 lightDir;
		float3 end_point;

		bool in_light_range = ResolveLightWithDir(light, pos, dir,
			/*out*/attenuation, /*out*/end_point);
		 
		[branch]
		if (in_light_range) {
			float3 luminance = attenuation * light.color;
			float3 shadow = TraceShadow(pos, end_point,
				/*inout*/sampleState);

			direct_light = shadow * luminance;
#ifdef _ENABLEFOG
			if (any(direct_light != 0)) direct_light *= Tr(pos, dir, distance(pos, end_point), sampleState);
#endif
		}
	}
	return direct_light * light_count;
}

#ifdef _ENABLEFOG
float PhaseFunction(float3 i, float3 o, float g)
{
   	//return 1.0/(4.0*3.14);
    float mu = clamp(dot(i, o),-1,1);
    return 1 / (4 * 3.14159265359) * (1 - g*g) / pow((1 + g * g - 2 * g * mu), 1.5);
    //return 3 / (8 * 3.14159265359) * 0.3758 * (1 + mu * mu) / 2.6241 / pow((1.6241 - 1.58 * mu), 1.5);
}

float3 EvaluateLight(float3 pos, float3 dir, float3 sigmaE, float G, inout int4 sampleState) {
	float3 res = 0;
	int light_count = clamp(_LightCount, 0, 100);
	if (light_count == 0) return 0;
	{
		float2 rand_num_light = SAMPLE;
		Light light = _LightList[floor(min(rand_num_light.x, 0.99) * light_count)];

		float attenuation;
		float3 lightDir;
		float3 end_point;

		bool in_light_range = ResolveLight(light, pos,
			/*inout*/sampleState,
			/*out*/attenuation, /*out*/lightDir, /*out*/end_point);

		[branch]
		if (in_light_range) {

			float3 luminance = attenuation * light.color;
			float3 direct_light_without_shadow = luminance * PhaseFunction(lightDir, dir, G);
			float3 shadow = TraceShadowWithFog(pos, end_point,
											/*inout*/sampleState); 

			res += shadow * direct_light_without_shadow * light_count;
		}
	}
	return res;
}


float SampleHeneyGreenstein(float s, float g) {
	if(abs(g) < 0.0001) return s * 2.0 - 1.0;
	float g2 = g*g;
	float t0 = (1 - g2) / (1 - g + 2 * g * s);
	float cosAng = (1 + g2 - t0*t0) / (2 * g);

	return cosAng;
}

float3 SampleHenyeyGreenstein(const float2 s, const float g) {
	float CosTheta = SampleHeneyGreenstein(s.x, g);

	float Phi = 2 * PI * s.y;
	float SinTheta = sqrt(max(0, 1 - CosTheta * CosTheta));

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;
	return H;
}
#endif

void DeterminateNextVertex(float3 pos, float3 dir, float dis,
	inout int4 sampleState,
	out float3 directColor, out float4 weight, out float3 nextPos, out float3 nextDir) {

#ifndef _ENABLEFOG
	weight = float4(1, 1, 1, 1);
	directColor = nextPos = nextDir = 0;
	return;
#else

	VolumeRecs all_recs;
	float2 min_max = InitVolumeRecs(pos, dir, all_recs);

	if (all_recs.count == 0) {
		weight = float4(1, 1, 1, 1);
		directColor = nextPos = nextDir = 0;
		return;
	}

	float sigmaS, sigmaE, sigmaTMax;
	float G;
	float t = min_max.x;

	dis = min(dis, min_max.y);

	GetFog(all_recs, pos + t * dir, t, /*out*/sigmaS, /*out*/sigmaE, /*out*/sigmaTMax, /*out*/G);

	int loop_num = 2048;
	[loop]
	while (loop_num-- > 0) {
		float rk = frac(Roberts1(sampleState.x % 128 * 128 + sampleState.y % 128 + sampleState.z) + SAMPLE);
		t -= log(1 - rk) / sigmaTMax;

		GetFog(all_recs, pos + t * dir, t, /*out*/sigmaS, /*out*/sigmaE, /*out*/sigmaTMax, /*out*/G);

		if (t > dis) {
			sampleState.w++;
			weight = float4(1, 1, 1, 1);
			directColor = nextPos = nextDir = 0;
			return;
		}
		else {
			rk = frac(Roberts1(sampleState.x % 128 * 128 + sampleState.y % 128 + sampleState.z) + SAMPLE);

			if (sigmaE / sigmaTMax > rk) {
				break;
			}
		}
	}

	float2 sample_2D = float2(SAMPLE, SAMPLE);
	nextDir = SampleHenyeyGreenstein(sample_2D, G);

	nextDir = mul(nextDir, GetMatrixFromNormal(dir));

	weight = float4((sigmaS / max(0.0000001, sigmaE)).xxx, 0);
	nextPos = t * dir + pos;

	float3 S = 0;
	if (any(weight.xyz != 0)) {
		S = EvaluateLight(nextPos, dir, sigmaE, G,
			/*inout*/sampleState);
	}
	directColor = S * weight.xyz;
#endif
}

#endif