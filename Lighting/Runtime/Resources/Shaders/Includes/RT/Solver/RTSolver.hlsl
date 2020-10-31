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

    sampleState.w = 0;

    [loop]
	while (weight.w > 0 && depth-- > 0) {

		nextWeight = weight;
		float3 directColor;
		float3 nextDir;
		float3 normal;
		nextWeight.xyz *= cutoff;
		cutoff *= 0.8;
		 
		float4 t = TraceNextWithBackFace(pos, dir,
											/*inout*/sampleState, nextWeight, roughness,
											/*out*/directColor, nextDir, normal);


         
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

			pos = fogNextPos;
			dir = fogNextDir;
			roughness = 1;

			res += weight.xyz * fogColor;

			weight.xyz *= fogWeight.xyz;
			weight.w = 1;

			float co = min(1, Average(weight.xyz));
			if (SAMPLE > co) {
				break;
			}
            sampleState.w++;
			weight.xyz /= co;
		}
	}

	return res;
}
       

//----------------------------------------------------
//---------Path tracer with Irr cache-----------------
//----------------------------------------------------
struct PosIrr
{
    float3 pos;
    half3 irr;
    half3 weight;
};               
float3 PathTracer_IrrCache(const int maxDepth,
	const float3 origin, const float3 direction,
	inout int4 sampleState, 
    bool traceFog = false, bool includeDirectional = false, bool debug = false, float roughness = 0)
{

    float4 firstHit = 0;
    int depth = min(max(maxDepth, 1), 16);
    float3 res = 0;
	[branch]
    if (includeDirectional)
        res = LightLuminanceCameraWithFog(origin, direction, sampleState);
    float4 weight = 1;
    float4 nextWeight = 1;
    float3 pos = origin, dir = direction;
    float cutoff = 1;
    
    PosIrr cache;
    int cacheIndex = 0;
    bool firstFog = false;

    [loop]
    while (weight.w > 0 && depth-- > 0)
    {
        nextWeight = weight;
        float3 directColor;
        float3 nextDir;
        float3 normal;
        float r = roughness;
        nextWeight.xyz *= cutoff;
        cutoff *= 0.8;
        
        float4 t = TraceNextWithBackFace_ForceTrace(pos, dir,
											/*inout*/sampleState, nextWeight, roughness,
											/*out*/directColor, nextDir, normal);
        
        if (firstHit.w == 0)
        {
            firstHit = float4(t.yzw, 1);
        }
        float3 fogColor, fogNextPos, fogNextDir;
        float4 fogWeight = 1;
        fogColor = fogNextPos = fogNextDir = 0;

#ifdef _ENABLEFOG
        if (traceFog)
        {
            DeterminateNextVertex(pos, dir, t.x,
				/*inout*/sampleState,
				/*out*/fogColor, fogWeight, fogNextPos, fogNextDir);
        }
#endif
        
        float3 incre_res = 0;
        float4 decre_weight;
        bool use_cache = false;
		// stop here, use irr cache
        if (cacheIndex && nextWeight.w > 0 && fogWeight.w) {
            float2 e = float2(SAMPLE, SAMPLE);
            if (e.x < min(t.x, min(r, roughness)))
            {
                float4 irr = GetIrr(t.yzw);
                if (e.y < irr.w / 256)
                {
                    directColor = irr;
                    incre_res = weight.xyz * irr;
                    weight.w = -1;
                    use_cache = true;
                }
            }
        }
        
        if (!use_cache)
        {
            if (fogWeight.w)
            { //pick surface 

                pos = t.yzw;
                dir = nextDir;

                incre_res = weight.xyz * fogWeight.xyz * directColor;   
                decre_weight = float4(nextWeight.xyz * fogWeight.xyz, nextWeight.w);
            }
            else
            { // pick fog

                pos = fogNextPos;
                dir = fogNextDir;
                roughness = 1;

                incre_res = weight.xyz * fogColor;
                decre_weight = float4(fogWeight.xyz, 1);

                float co = min(1, Average(weight.xyz));
                if (SAMPLE > co)
                {
                    break;
                }
                sampleState.w++;
                decre_weight.xyz /= co;
            }
        }

#ifdef _ENABLEFOG
        res += incre_res * (traceFog ? Tr(pos, dir, t.x, sampleState) : 1);
#else
        res += incre_res;
#endif
        weight *= decre_weight;
        weight.w = decre_weight.w;
        if (cacheIndex < 1)
        {
            if (!fogWeight.w) firstFog = true;
            cache.pos = pos;
            cache.weight = decre_weight;
            cache.irr = directColor;
            cacheIndex++;
        }
        else
        {
            cache.irr += cache.weight * directColor;
            cache.weight *= decre_weight.w <= 0 ? 0 : decre_weight.xyz;
        }
        if (use_cache)
            break;
    }
    if (!firstFog) SetIrr(cache.pos, cache.irr);

    if (debug)
        return GetIrr(firstHit.xyz);
    return res;
}


#endif //RTSOLVER_H_