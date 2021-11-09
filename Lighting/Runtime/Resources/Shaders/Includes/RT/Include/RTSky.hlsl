#ifndef RTSKY_H_
#define RTSKY_H_

#include "./RayTracingLight.hlsl"
#include "./Sampler.hlsl"
#include "./RTAtmo.hlsl"


int _Procedural;
float3 _Tint;
float _Exposure;
float _Rotation;
half4 _MainTex_HDR;

float3 RotateAroundYInDegrees (float3 vertex, float degrees)
{
    float alpha = degrees * 3.14159265359f / 180.0f;
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float3(mul(m, vertex.xz), vertex.y).xzy;
}

inline half3 DecodeHDR (half4 data, half4 decodeInstructions)
{
    half alpha = decodeInstructions.w * (data.a - 1.0) + 1.0;	
	return (decodeInstructions.x * pow(alpha, decodeInstructions.y)) * data.rgb;
}

inline float2 ToRadialCoords(float3 coords)
{
    float3 normalizedCoords = normalize(coords);
    float latitude = acos(normalizedCoords.y);
    float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
    float2 sphereCoords = float2(longitude, latitude) * float2(0.5/3.14159265359f, 1.0/3.14159265359f);
    return float2(0.5,1.0) - sphereCoords;
}
		   
   
//---------------------------------------------
//----------Expensive Version------------------
//---------------------------------------------
void SkyLight(inout RayIntersection rayIntersection, const int distance = 10000) {
	if(rayIntersection.weight.w < 0){
		return;
	}
	rayIntersection.t = distance;
	rayIntersection.weight = 0; 
	if (_Procedural == 0) {
		float2 tc = ToRadialCoords(RotateAroundYInDegrees(WorldRayDirection(), -_Rotation));
		float3 x = mul(_V_Inv, float4(0, 0, 0, 1));
		x.y += _PlanetRadius;
		rayIntersection.directColor = Atmo(x, WorldRayDirection(), _SunDir);
	}
	else if (_Procedural == 1) {
		float2 tc = ToRadialCoords(RotateAroundYInDegrees(WorldRayDirection(), -_Rotation));

		half4 tex = _Skybox.SampleLevel(sampler_Skybox, tc, rayIntersection.roughness * 3);
		half3 c = DecodeHDR(tex, _MainTex_HDR);
		c = c * _Tint.rgb;
		c *= _Exposure;
		rayIntersection.directColor = c;
	}
	else {
		rayIntersection.directColor = lerp(_MainTex_HDR, _Tint, smoothstep(-0.1, 0.1, WorldRayDirection().y));
	}
}

     
		  			
//---------------------------------------------
//-----------Realtime Version------------------
//---------------------------------------------
void SkyLight(inout RayIntersection_RTGI rayIntersection, const int distance = 10000)
{
	half3 c;
	
	if (_Procedural > 0.5)
	{
		c = lerp(_MainTex_HDR, _Tint, smoothstep(-0.1, 0.1, WorldRayDirection().y));
	}
	else
	{
		float2 tc = ToRadialCoords(RotateAroundYInDegrees(WorldRayDirection(), -_Rotation));

		half4 tex = _Skybox.SampleLevel(sampler_Skybox, tc, 7);
		c = DecodeHDR(tex, _MainTex_HDR);
		c = c * _Tint.rgb;
		c *= _Exposure;
	}

	rayIntersection.t = distance;
	rayIntersection.data1 = 1; //miss flag
	rayIntersection.data2 = EncodeHDR2Int(c);
}

#endif