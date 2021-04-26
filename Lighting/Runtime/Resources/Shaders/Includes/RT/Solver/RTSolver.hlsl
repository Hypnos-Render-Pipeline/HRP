#ifndef RTSOLVER_H_
#define RTSOLVER_H_

#include "../Include/Sampler.hlsl"
#include "../Include/RayTracingCommon.hlsl"
#include "../Include/TraceRay.hlsl"
#include "../Include/RTFog.hlsl"
#include "../Include/RTSky.hlsl"
#include "../Include/IrradianceCache.hlsl"
                  
//----------------------------------------------------
//---------Native path tracer-------------------------
//----------------------------------------------------
float3 PathTracer(const int maxDepth,
	const float3 origin, const float3 direction,
	inout int4 sampleState, bool traceFog = false, bool includeDirectional = false, float roughness = 0) {

	int depth = min(max(maxDepth, 1), 16);
	float3 res = 0;
	[branch]
	if (includeDirectional)
		res = LightLuminanceCameraWithFog(origin, direction, sampleState);
	float4 weight = 1;
	float4 nextWeight = 1;
	float3 pos = origin, dir = direction;
	float cutoff = 1;
    int fog_depth = 128;

    sampleState.w = 0;

    [loop]
	while (weight.w > 0 && depth-- > 0) {

		nextWeight = weight;
		float3 directColor;
		float3 nextDir;
		nextWeight.xyz *= cutoff;
		cutoff *= 0.8;
		 
		float4 t = TraceNextWithBackFace(pos, dir,
											/*inout*/sampleState, nextWeight, roughness,
											/*out*/directColor, nextDir);

		float3 fogColor, fogNextPos, fogNextDir; 
		float4 fogWeight = 1;
		fogColor = fogNextPos = fogNextDir = 0;

#ifdef _ENABLEFOG
		if (traceFog) {
            DeterminateNextVertex(pos, dir, t.x,
				/*inout*/sampleState, 
				/*out*/fogColor, fogWeight, fogNextPos, fogNextDir);
		}
#endif
         
		if (fogWeight.w) { //pick surface 

			pos = t.yzw;
			dir = nextDir;

			res += weight.xyz * fogWeight.xyz * directColor;

			weight.xyz *= nextWeight * fogWeight.xyz;
			weight.w = nextWeight.w;
		}
		else { // pick fog
            depth += fog_depth-- > 0;
			pos = fogNextPos;
			dir = fogNextDir;
			roughness = 1;

			res += weight.xyz * fogColor;

			weight.xyz *= fogWeight.xyz;
			weight.w = 1;
		}
	}

	return res;
}
       

//----------------------------------------------------
//---------Path tracer with Irr cache-----------------
//----------------------------------------------------            
float3 PathTracer_IrrCache(const int maxDepth,
	const float3 origin, const float3 direction,
	inout int4 sampleState, 
    bool traceFog = false, bool includeDirectional = false, bool debug = false, float roughness = 0) {

	int depth = min(max(maxDepth, 1), 12);
	float3 res = 0;
	[branch]
	if (includeDirectional)
		res = LightLuminanceCameraWithFog(origin, direction, sampleState);
	float4 weight = 1;
	float4 nextWeight = 1;
	float3 pos = origin, dir = direction;
	float cutoff = 1;
	int fog_depth = 128;
	float4 firstHit = 0;
	float3 firstLight = 0;
	bool firstFog = false;

	sampleState.w = 0;

	[loop]
	while (weight.w > 0 && depth-- > 0) {

		nextWeight = weight;
		float3 directColor;
		float3 nextDir;
		float r = roughness;
		nextWeight.xyz *= cutoff;
		cutoff *= 0.8;

		float4 t = TraceNextWithBackFace(pos, dir,
			/*inout*/sampleState, nextWeight, roughness,
			/*out*/directColor, nextDir);

		if (firstHit.w == 0)
		{
			firstHit = float4(t.yzw, 0);
		}
		else if (firstHit.w == 1)
		{
			firstLight = res;
		}
		firstHit.w++;

		float3 fogColor, fogNextPos, fogNextDir;
		float4 fogWeight = 1;
		fogColor = fogNextPos = fogNextDir = 0;

#ifdef _ENABLEFOG
		if (traceFog) {
			DeterminateNextVertex(pos, dir, t.x,
				/*inout*/sampleState,
				/*out*/fogColor, fogWeight, fogNextPos, fogNextDir);
		}
#endif

		float3 incre_res = 0;
		float4 decre_weight;
		// stop here, use irr cache
		if (firstHit.w > 1 && t.x > 2 && nextWeight.w > 0 && fogWeight.w) {
			float2 e = float2(SAMPLE, SAMPLE);
			if (e.x < min(t.x / 4, min(r, roughness)))
			{
				float4 irr = GetIrr(t.yzw);
				if (e.y < irr.w / 256)
				{
					res += (traceFog ? Tr(pos, dir, t.x, sampleState) : 1) * weight.xyz * (irr + directColor);
					firstHit.w == 0;
					break;
				}
			}
		}

		if (fogWeight.w) { //pick surface 

			pos = t.yzw;
			dir = nextDir;

			res += weight.xyz * fogWeight.xyz * directColor;

			weight.xyz *= nextWeight * fogWeight.xyz;
			weight.w = nextWeight.w;
		}
		else { // pick fog
			depth += fog_depth-- > 0;
			pos = fogNextPos;
			dir = fogNextDir;
			roughness = 1;

			res += weight.xyz * fogColor;

			weight.xyz *= fogWeight.xyz;
			weight.w = 1;
			if (firstHit.w == 1) firstFog = true;
		}
	}
	
	if (firstHit.w != 0)
		SetIrr(firstHit, res - firstLight);

	if (debug)
		return GetIrr(firstHit.xyz);
	return res;
}


#endif //RTSOLVER_H_