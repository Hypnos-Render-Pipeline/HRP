#ifndef _CAMERA_
#define _CAMERA_

#include "../../Lighting/Runtime/Resources/Shaders/Includes/RT/Solver/RTSolver.hlsl"

float2 _DOF;
int _sqrt_spp;
int _Max_Frame;
int _Max_depth;

int _PreferFrameRate;
int _CacheIrradiance;
int _SubFrameIndex;

float _NearPlane;

Texture2D<float4> _Response; SamplerState sampler_Response;
RWTexture2D<float4> Target;
RWTexture2D<float4> History;
RWTexture2D<float4> Record;
RWTexture2D<float4> Length;


float Luminance(float3 col) {
	return col.r * 0.299 + col.g * 0.587 + col.b * 0.114;
}
float3 ToneMap(float3 color) {
	return color / (1 + Luminance(color));
}
float3 UnToneMap(float3 color) {
	return color / (1 - Luminance(color));
}

[shader("raygeneration")]
void RayGeneration()
{
	int2 dispatchIdx = DispatchRaysIndex().xy;
	int frameIndex = _PreferFrameRate ? _Frame_Index / 4 : _Frame_Index / 2;
	if (_PreferFrameRate)
		dispatchIdx = dispatchIdx * 2 + int2((_SubFrameIndex & 1), (_SubFrameIndex + (_SubFrameIndex >> 1)) & 1);
	else
		dispatchIdx.y = dispatchIdx.y * 2 + (dispatchIdx.x + _SubFrameIndex) % 2;

	float4 old = History[dispatchIdx], rec = Record[dispatchIdx];
	float variance = 1;
	if (old.w != 0 && frameIndex != 0)
	{
		variance = rec.a;
		variance += Record[max(dispatchIdx - int2(0, 1), 0)].a;
		variance += Record[min(dispatchIdx + int2(0, 1), _Pixel_WH)].a;
		variance += Record[max(dispatchIdx - int2(1, 0), 0)].a;
		variance += Record[min(dispatchIdx - int2(1, 0), _Pixel_WH)].a;
		variance += Record[max(dispatchIdx - int2(0, 1), 0)].a;
		variance += Record[min(dispatchIdx + int2(0, 1), _Pixel_WH)].a;
		variance += Record[max(dispatchIdx - int2(1, 0), 0)].a;
		variance += Record[min(dispatchIdx - int2(1, 0), _Pixel_WH)].a;
		variance /= 5;

		variance = clamp(variance * 2, 0, 1);
	}
	int sqrt_spp = min(_sqrt_spp, 4);
	int spp = sqrt_spp * sqrt_spp;

	float3 color = 0;
	float3 rnd_length = Length[dispatchIdx].xyz * 10;

	int sample_turn = frameIndex / 255;
	float2 roberts = Roberts2(sample_turn);
	int4 sampleState = int4(dispatchIdx + int2(roberts * 128), frameIndex, 0);

	float rnd = Roberts1(frameIndex);

	if (rnd > variance) return;

	for (int i = 0; i < spp; i++) {
		float2 offset = Roberts2(frameIndex * spp + i);


		float2 dispatch_uv = (dispatchIdx + offset) / _Pixel_WH.xy * 2 - 1;

		float4 dispatch_dir = mul(_P_Inv, float4(dispatch_uv, 1, 1));
		dispatch_dir /= dispatch_dir.w;
		float dotCV = -normalize(dispatch_dir.xyz).z;
		dispatch_dir = mul(_V_Inv, float4(dispatch_dir.xyz, 0));

		float3 dispatch_pos = mul(_V_Inv, float4(0, 0, 0, 1));

		float3 origin, direction;
		origin = dispatch_pos;
		direction = normalize(dispatch_dir.xyz);
		float3 response = 1;

		if (_DOF.y != 0) {
			float2 aperture_offset = frac(float2(SAMPLE, SAMPLE) + roberts);//UniformSampleRegularPolygon(6, frac(float2(SAMPLE, SAMPLE) + roberts));
			response = _Response.SampleLevel(sampler_Response, aperture_offset, 0).xyz;
			int max_resample_num = 10;
			while (max_resample_num-- > 0 && all(response == 0)) {
				aperture_offset = frac(aperture_offset + offset);
				response = _Response.SampleLevel(sampler_Response, aperture_offset, 0).xyz;
			}
			aperture_offset = (aperture_offset - 0.5) * 2;
			aperture_offset *= _DOF.y;
			float3 aim_point = origin + direction * _DOF.x;

			origin += mul(_V_Inv, aperture_offset);
			direction = normalize(aim_point - origin);
		}

		int4 pixelSampleState = sampleState + int4(i % sqrt_spp, i / sqrt_spp, 0, 0);

		float3 ori_offset = origin + _NearPlane / dotCV * direction;

		if (_CacheIrradiance != 0)
		{
			color += response * PathTracer_IrrCache(_Max_depth,
				ori_offset, direction,
				/*inout*/pixelSampleState,
				true, true, _CacheIrradiance == 2);
		}
		else {
			color += response * PathTracer(_Max_depth,
				ori_offset, direction,
				/*inout*/pixelSampleState,
				true, true);
		}
	}
	color /= spp;

	if (any(isnan(color))) color = float3(100000, 0, 100000);

	color = clamp(color, 0, 100);
	color = UnToneMap(ToneMap(color));

	float3 ex = (old.xyz * old.w + color) / old.w;
	float3 ex2 = (rec * old.w + color * color) / old.w;

	if (old.w <= 512) {
		variance = dot(abs(ex2 - ex * ex), 1) / dot(ex2 + 0.001, 1); // actual value should divide by '(old.w + 1)', but it works better...
		variance = lerp(1, variance, min(1, (old.w * sqrt_spp) / 256.0));
	}
	else {
		variance = rec.w;
	}

	int count = min(old.w + 1, _Max_Frame);
	if (old.w != 0 && frameIndex != 0) {
		History[dispatchIdx] = float4((old.xyz * old.w + color) / (old.w + 1), count);
		Record[dispatchIdx] = float4((rec.xyz * old.w + color * color) / (old.w + 1), variance);
	}
	else {
		History[dispatchIdx] = float4(color, 1);
		Record[dispatchIdx] = float4(color * color, 1);
	}

	Target[dispatchIdx] = float4(color, 1);//float4(ac_data, k + 1);
}

[shader("closesthit")]
void ClosestHit(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
{
	rayIntersection.directColor = float3(1000, 0, 1000);
	rayIntersection.weight = 0;
}

[shader("anyhit")]
void AnyHitMain(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
{
	rayIntersection.directColor = float3(1000, 0, 1000);
	rayIntersection.weight = 0;
	AcceptHitAndEndSearch();
}





[shader("miss")]
void Miss(inout RayIntersection rayIntersection : SV_RayPayload)
{
	SkyLight(/*inout*/ rayIntersection);
}

[shader("miss")]
void Miss_FogVolume(inout RayIntersection rayIntersection : SV_RayPayload)
{
	rayIntersection.t = -1;
}

#endif