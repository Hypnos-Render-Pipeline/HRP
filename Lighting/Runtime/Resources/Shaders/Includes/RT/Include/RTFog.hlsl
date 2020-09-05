#ifndef RTFOG_H_
#define RTFOG_H_

#include "./RayTracingLight.hlsl"
#include "./Sampler.hlsl"


inline float Average(const float2 i) { return dot(i, 1./2); }
inline float Average(const float3 i) { return dot(i, 1./3); }
inline float Average(const float4 i) { return dot(i, 1./4); }



int _enableGlobalFog;
float4 _globalFogParameter;


void GetGlobalFog(float3 pos, out float3 sigmaS, out float3 sigmaE, out float G)
{
    float sigmaA = _globalFogParameter.y;
    
    sigmaS = max(0.0000001, _globalFogParameter.z);
    
    sigmaE = sigmaA + sigmaS;

    G = clamp(_globalFogParameter.w, -0.99, 0.99);
}
#define GetFog GetGlobalFog
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
	[loop]
	for (int i = 0; i < light_count; i++) //on the fly
	{
		Light light = _LightList[i];

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
			float3 shadow = TraceShadow(pos, end_point,
											/*inout*/sampleState); 

			if (any(shadow != 0)) {
				//shadow *= exp(-sigmaE * distance(pos, end_point));
			}

			res += shadow * direct_light_without_shadow;
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

float3 DeterminateNextVertex(float3 pos, float3 dir, float dis,
								inout int4 sampleState, 
								out float3 directColor, out float4 weight, out float3 nextPos, out float3 nextDir) {
	float3 sigmaS,sigmaE;
	float G;
    GetFog(pos, 
    		/*out*/sigmaS, /*out*/sigmaE, /*out*/G);


    // Because 'SAMPLE' is a finite array, and is not sufficient for thin fog sampling.
    // In fact is not correct, but it just looks better...
	float rk = /*sqrt*/frac(Roberts1(sampleState.x % 128 * 128 + sampleState.y % 128 + sampleState.z) + SAMPLE); sampleState.w++;
	 


    float t = Average(-1/sigmaE * log(1 - rk)); 

    if (t < dis) { // pick fog

	    float3 transmittance = exp(-sigmaE * t);
	    float pdf = Average(sigmaE * transmittance);

	    float3 scatterCoef = transmittance * sigmaS;

	    float2 sample_2D = float2(SAMPLE, SAMPLE);
	    nextDir = SampleHenyeyGreenstein(sample_2D, G);
	    
	    nextDir = mul(nextDir, GetMatrixFromNormal(dir));

	    weight = float4(scatterCoef / pdf, 0);
	    nextPos = t * dir + pos;

	    float3 S = EvaluateLight(nextPos, dir, sigmaE, G, 
	    							/*inout*/sampleState);
	    

	    directColor = S * weight.xyz;

    	return 0;
    }
    else { // pick surface
    	float3 transmittance = exp(-sigmaE * dis);
	    float pdf = Average(transmittance);

	    weight = float4(transmittance / pdf, 1);
	    directColor = nextPos = nextDir = 0;
    	return 0;
    }
}


#endif