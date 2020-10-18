#ifndef RTLIGHT_H_
#define RTLIGHT_H_

#include "./Sampler.hlsl"
#include "./TraceRay.hlsl"

#define DIRECTIONAL 0
#define POINT       1
#define SPOT        2
#define SPHERE      3
#define TUBE        4
#define QUAD        5
#define DISC        6
#define MESH        7

//light info
struct Light
{
	float3 position;
	float type;
	float4 b, c;
	float3 color;
	int mask;
};


int _LightCount;
StructuredBuffer<Light> _LightList;

int _UseAttenuationCureve;
Texture2D<float> _AttenuationTex; SamplerState sampler_linear_clamp;

bool ResolveLight(const Light light, const float3 position, inout int4 sampleState,
					out float att, out float3 L, out float3 end_point)
{
	float2 sample_2D;
	sample_2D.x = SAMPLE;
	sample_2D.y = SAMPLE;
	sample_2D -= 0.5;

	[branch]
	if (light.type == 2) { //Spot
		float3 lpos = light.position;
		L = (lpos - position);
		float range = light.c;
		float3 dir = light.b.xyz;
		float cos_angle = light.b.w;

		if (_UseAttenuationCureve) {
			att = length(L);
			L /= att;
			att = _AttenuationTex.SampleLevel(sampler_linear_clamp, float2((att / range / 10), 0.5), 0);
		}
		else {
			att = 1 / length(L);
			L *= att;
			att = range * att;
			att *= att;
		}

		att *= att;
		float k = saturate(1 - (1 - dot(L, dir)) / (1 - cos_angle));
		att *= sqrt(k);

		end_point = float3(sample_2D, SAMPLE) * 0.1 + light.position; sampleState.w++;
	}
	else if (light.type == 0) { //Directional
		L = light.b;
		att = 1;
		end_point = position + float3(sample_2D, SAMPLE) * 10 + L * 100; sampleState.w++;
	}
	else if (light.type == 1) { //Point
		float3 lpos = light.position;
		L = (lpos - position);
		float range = light.b;

		if (_UseAttenuationCureve) {
			att = length(L);
			L /= att;
			att = _AttenuationTex.SampleLevel(sampler_linear_clamp, float2((att / range / 10), 0.5), 0);
		}
		else {
			att = 1 / length(L);
			L *= att;
			att = range * att;
			att *= att;
		}

		end_point = float3(sample_2D, SAMPLE) * light.b.y * 2 + light.position; sampleState.w++;
	} 
	else if (light.type == 5) { //Rectangle
		float3 lpos = (light.b.xyz * sample_2D.x + light.c.xyz * sample_2D.y) + light.position;
		float3 lnormal = normalize(cross(light.c.xyz, light.b.xyz));

		L = (lpos - position);
		float range = light.b.w;

		float ldis = length(L);
		att = 1 / ldis;
		L *= att;
		att *= att;
		att *= saturate(dot(L, lnormal));
		att *= light.c.w;

		if (_UseAttenuationCureve) {
			att = ldis > range * 10 ? 0 : att;
		}

		end_point = lpos;
	}
	else if (light.type == 6) { //Disc
		float3 lnormal = light.b.xyz;
		float3 ltangent = dot(lnormal, float3(0, 0, 1)) > 0.9 ? normalize(cross(lnormal, float3(0, 1, 0))) : normalize(cross(lnormal, float3(0, 0, 1)));
		float3 lbitangent = cross(lnormal, ltangent);
		sample_2D = UniformSampleDisk(sample_2D + 0.5);
		float3 lpos = ltangent * sample_2D.x + lbitangent * sample_2D.y;
		lpos *= light.b.w;
		lpos += light.position;

		L = (lpos - position);
		float range = light.c;

		float ldis = length(L);
		att = 1 / ldis;
		L *= att;
		att *= att;
		att *= saturate(dot(L, lnormal));
		att *= light.b.w * light.b.w;

		if (_UseAttenuationCureve) {
			att = ldis > range * 10 ? 0 : att;
		}

		end_point = lpos;
	}
	
	return true;
}

float4 FindNearestToPoint(float3 pos, float3 dir, float3 p) {
	float3 l = p - pos;
	float t = dot(l, dir);
	float3 nPos = pos + t * dir;
	return float4(nPos, t > 0 ? distance(p, nPos) : -1);
}

float4 FindPlanePoint(float3 pos, float3 dir, float3 p, float3 n) {
	float3 l = p - pos;
	float t1 = dot(l, n);
	float t2 = dot(dir, n);
	t1 /= t2;
	float3 nPos = pos + t1 * dir;
	return float4(nPos, t1 > 0 ? distance(p, nPos) : -1);
}

bool ResolveLightWithDir(const Light light, const float3 position, const float3 direction,
							out float att, out float3 end_point)
{
	[branch]
	if (light.type == 2) { //Spot
		float4 lpos = FindNearestToPoint(position, direction, light.position);
		if (lpos.w > 0.1 || lpos.w < 0) {
			att = 0;
			end_point = 0;
			return false;
		}
		float3 L = lpos - position;
		float range = light.c;
		float3 dir = light.b.xyz;
		float cos_angle = light.b.w;

		att = 1 / length(L);
		L *= att;
		att = range * att;

		att *= att;
		float k = saturate(1 - (1 - dot(L, dir)) / (1 - cos_angle));
		att *= sqrt(k);

		end_point = lpos.xyz;
	}
	else if (light.type == 0) { //Directional
		float3 L = light.b;
		float4 lpos = FindNearestToPoint(position, direction, position + L * 100);
		if (lpos.w > 10 || lpos.w < 0) {
			att = 0;
			end_point = 0;
			return false;
		}

		att = dot(direction, L) * (10 - lpos.w) / 10;
		end_point = lpos.xyz;
	}
	else if (light.type == 1) { //Point
		float4 lpos = FindNearestToPoint(position, direction, light.position);
		if (lpos.w > light.b.y || lpos.w < 0) {
			att = 0;
			end_point = 0;
			return false;
		}

		float range = light.b.x;

		att = range;
		att *= att;

		end_point = lpos.xyz;
	}
	else if (light.type == 5) { //Rectangle
		float3 lnormal = normalize(cross(light.c.xyz, light.b.xyz));
		if (dot(direction, lnormal) < 0) return false;
		float4 lpos = FindPlanePoint(position, direction, light.position, lnormal);

		float3 pdir = lpos.xyz - light.position;
		if (abs(dot(pdir, light.b.xyz)) > dot(light.b.xyz,light.b.xyz) / 2
				|| abs(dot(pdir, light.c.xyz)) > dot(light.c.xyz,light.c.xyz) / 2
				|| lpos.w < 0) {
			att = 0;
			end_point = 0;
			return false;
		}
		  
		float range = light.b.w;

		att = range;
		att *= att;

		end_point = lpos;
	}
	else if (light.type == 6) { //Disc
		float3 lnormal = light.b.xyz;
		if (dot(direction, lnormal) < 0) return false;

		float4 lpos = FindPlanePoint(position, direction, light.position, lnormal);

		if (lpos.w > light.b.w || lpos.w < 0) {
			att = 0;
			end_point = 0;
			return false;
		}

		float range = light.c;

		att = range;
		att *= att;

		end_point = lpos;
	}

	return att > 0.0001;
}

float3 LightLuminance(float3 pos, float3 dir,
							inout int4 sampleState) {
	float rnd = SAMPLE; sampleState.w++;
	float3 direct_light = 0;
	int light_count = clamp(_LightCount, 0, 100);
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

			direct_light += shadow * luminance * light_count;
		}
	}
	return direct_light;
} 

float3 LightLuminanceCamera(float3 pos, float3 dir,
	inout int4 sampleState) {
	float rnd = SAMPLE; sampleState.w++;
	float3 direct_light = 0;
	int light_count = clamp(_LightCount, 0, 100);
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

			direct_light += shadow * luminance;
		}
	}
	return direct_light * light_count;
}


float3 LightLuminanceSpec(float3 pos, float3 dir,
	inout int4 sampleState) {
	float rnd = SAMPLE; sampleState.w++;
	float3 direct_light = 0;
	int light_count = clamp(_LightCount, 0, 100);
	{
		Light light = _LightList[floor(min(rnd, 0.99) * light_count)];

		if (light.type > SPOT) return 0;

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

			direct_light += shadow * luminance;
		}
	}
	return direct_light * light_count;
}
#endif