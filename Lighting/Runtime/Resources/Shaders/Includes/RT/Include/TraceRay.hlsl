#ifndef TRACE_RAY_H_
#define TRACE_RAY_H_

#include "./Sampler.hlsl"

#define TRACE_SELF (-1)
#define TRACE_SHADOW (-2)
#define TRACE_FOG_VOLUME (-3)
#define END_TRACE (-4)

//---------------------------------------------
//----------Expensive Version------------------
//---------------------------------------------
float3 TraceShadow(const float3 start, const float3 end, 
					inout int4 sampleState) {

		RayDesc rayDescriptor;
		rayDescriptor.Origin = start;
		rayDescriptor.Direction = end - start;
		rayDescriptor.TMin = 0.001;
		rayDescriptor.TMax = length(rayDescriptor.Direction) - 0.002;
		rayDescriptor.Direction = normalize(rayDescriptor.Direction);
		
		RayIntersection rayIntersection;
		rayIntersection.t = rayDescriptor.TMax;
		rayIntersection.weight = float4(-1, -1, 0, TRACE_SHADOW);
		rayIntersection.sampleState = sampleState;
		rayIntersection.directColor = 1;

		TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);

		return rayIntersection.directColor;
}

float3 TraceShadow_PreventSelfShadow(const float3 start, const float3 end,
	inout int4 sampleState) {

	RayDesc rayDescriptor;
	rayDescriptor.Origin = start;
	rayDescriptor.Direction = end - start;
	rayDescriptor.TMin = 0;
	rayDescriptor.TMax = length(rayDescriptor.Direction);
	rayDescriptor.Direction = normalize(rayDescriptor.Direction);

	RayIntersection rayIntersection;
	rayIntersection.t = rayDescriptor.TMax;
	rayIntersection.weight = float4(InstanceID(), PrimitiveIndex(), 0, TRACE_SHADOW);
	rayIntersection.sampleState = sampleState;
	rayIntersection.directColor = 1;

	TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);

	return rayIntersection.directColor;
}

float4 TraceNext(const float3 start, const float3 dir,
	inout int4 sampleState, inout float4 weight, inout float roughness,
	out float3 directColor, out float3 nextDir, out float3 normal) {
	sampleState.w++;
	float rnd_num = SAMPLE;
	float importance = min(max(weight.x, max(weight.y, weight.z)), 1);
	if (rnd_num < importance) {
		RayDesc rayDescriptor;
		rayDescriptor.Origin = start;
		rayDescriptor.Direction = normalize(dir);
		rayDescriptor.TMin = 0.001;
		rayDescriptor.TMax = 10000;

		RayIntersection rayIntersection;
		rayIntersection.t = rayDescriptor.TMax;
		rayIntersection.sampleState = sampleState;
		rayIntersection.weight = weight;
		rayIntersection.roughness = roughness;
		rayIntersection.directColor = 0;

		TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE | RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);
		weight.xyz = rayIntersection.weight / importance;
		weight.w = rayIntersection.weight.w;
		roughness = rayIntersection.roughness;
		directColor = rayIntersection.directColor / importance;
		nextDir = rayIntersection.nextDir;
		normal = rayIntersection.normal;
		return rayIntersection.t;
	}
	directColor = 0;
	weight = END_TRACE;
	return -1;
}
   
float4 TraceNextWithBackFace(const float3 start, const float3 dir, 
								inout int4 sampleState, inout float4 weight, inout float roughness,
								out float3 directColor, out float3 nextDir, out float3 normal) {
	sampleState.w++;
	float rnd_num = SAMPLE;
	float importance = min(max(weight.x, max(weight.y, weight.z)), 1);
	if (rnd_num < importance) {
		RayDesc rayDescriptor;
		rayDescriptor.Origin = start;
		rayDescriptor.Direction = normalize(dir); 
		rayDescriptor.TMin = 0.001;
		rayDescriptor.TMax = 10000;

		RayIntersection rayIntersection;
		rayIntersection.t = rayDescriptor.TMax;
		rayIntersection.sampleState = sampleState;
		rayIntersection.weight = weight;
		rayIntersection.roughness = roughness;
		rayIntersection.directColor = 0;

		TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);
		weight.xyz = rayIntersection.weight / importance;
		weight.w = rayIntersection.weight.w;
		roughness = rayIntersection.roughness;
		directColor = rayIntersection.directColor / importance;
		nextDir = rayIntersection.nextDir;
		normal = rayIntersection.normal;
		return rayIntersection.t;
	}
	directColor = 0;
	weight = END_TRACE;
	return -1;
}


float4 TraceNextWithBackFace_ForceTrace(const float3 start, const float3 dir,
								inout int4 sampleState, inout float4 weight, inout float roughness,
								out float3 directColor, out float3 nextDir, out float3 normal)
{
    RayDesc rayDescriptor;
    rayDescriptor.Origin = start;
    rayDescriptor.Direction = normalize(dir);
    rayDescriptor.TMin = 0.001;
    rayDescriptor.TMax = 10000;

    RayIntersection rayIntersection;
    rayIntersection.t = rayDescriptor.TMax;
    rayIntersection.sampleState = sampleState;
    rayIntersection.weight = weight;
    rayIntersection.roughness = roughness;
    rayIntersection.directColor = 0;

    TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);
    weight.xyz = rayIntersection.weight;
    weight.w = rayIntersection.weight.w;
    roughness = rayIntersection.roughness;
    directColor = rayIntersection.directColor;
    nextDir = rayIntersection.nextDir;
    normal = rayIntersection.normal;
    return rayIntersection.t;
}

struct SubsurfaceHitInfo {
	float3 albedo;
	float3 normal;
	float3 gN;
	float4 t;
};

float4 TraceSelf(const float3 start, const float3 dir, const float max_dis,
					inout int4 sampleState,
					out int num, out float3 albedo, out float3 normal, out float3 gN) {
	RayDesc rayDescriptor;
	rayDescriptor.Origin = start;
	rayDescriptor.Direction = normalize(dir);
	rayDescriptor.TMin = 0;
	rayDescriptor.TMax = max_dis;

	RayIntersection rayIntersection;
	rayIntersection.t = rayDescriptor.TMax;
	rayIntersection.sampleState = sampleState;
	rayIntersection.weight = float4(InstanceID(), 0, 0, TRACE_SELF);
	rayIntersection.directColor = 0;
	rayIntersection.nextDir = -1;

	SubsurfaceHitInfo infos[5]; // we can use stack free sample strategy, but it will consume more random numbers.

	num = 0;

	int max_hit = 5;

	while (max_hit-- > 0) {
		TraceRay(_RaytracingAccelerationStructure, RAY_FLAG_FORCE_NON_OPAQUE, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);
		if (rayIntersection.t.x == rayDescriptor.TMax) break;
		
		infos[num].albedo = rayIntersection.directColor;
		infos[num].normal = rayIntersection.normal;
		infos[num].gN = rayIntersection.nextDir;
		infos[num].t = rayIntersection.t;

		rayDescriptor.Origin = rayIntersection.t.yzw;
		rayDescriptor.TMin = 0.01;
		rayDescriptor.TMax = max_dis - rayIntersection.t.x;

		rayIntersection.t = rayDescriptor.TMax;

		num++;
	}
	sampleState = rayIntersection.sampleState;
	int pick = (SAMPLE - 0.0001) * num;
	sampleState+=1;
	
	albedo = infos[pick].albedo;
	normal = infos[pick].normal;
	gN = infos[pick].gN;
	return infos[pick].t;
}

					
//---------------------------------------------
//-----------Realtime Version------------------
//---------------------------------------------
				 				 
bool TraceShadow_RTGI(const float3 start, const float3 end)
{
	RayDesc rayDescriptor;
	rayDescriptor.Origin = start;
	rayDescriptor.Direction = end - start;
	rayDescriptor.TMin = 0.001;
	rayDescriptor.TMax = length(rayDescriptor.Direction) - 0.002;
	rayDescriptor.Direction = normalize(rayDescriptor.Direction);
		
	RayIntersection_RTGI rayIntersection;
	rayIntersection.t = rayDescriptor.TMax;
	rayIntersection.data1 = 1;
	//rayIntersection.albedo_flags = 0;
	
	TraceRay(_RaytracingAccelerationStructure, /*RAY_FLAG_CULL_BACK_FACING_TRIANGLES*/0, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);

	return rayIntersection.data1 & 1;
}

GBuffer_RTGI TraceNext_RTGI(const float3 start, const float3 dir)
{
	RayDesc rayDescriptor;
	rayDescriptor.Origin = start;
	rayDescriptor.Direction = normalize(dir);
	rayDescriptor.TMin = 0.001;
	rayDescriptor.TMax = 10000;

	RayIntersection_RTGI rayIntersection;
	rayIntersection.t = rayDescriptor.TMax;
	rayIntersection.data1 = 1;

	TraceRay(_RaytracingAccelerationStructure, /*RAY_FLAG_CULL_BACK_FACING_TRIANGLES*/0, 0xFF, 0, 1, 0, rayDescriptor, rayIntersection);
	
	return DecodeIData2GBuffer(rayIntersection);
}

#endif