#ifndef RTSKY_H_
#define RTSKY_H_

#include "./RayTracingLight.hlsl"
#include "./Sampler.hlsl"
#include "./RTAtmo.hlsl"


int _Procedural;
float3 _Tint;
float _Exposure;
float _Rotation;
Texture2D _Skybox; SamplerState sampler_Skybox;
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
const float3 Scatter(float3 x, const float3 v, const float3 s) {
	float phi = atan(v.z / v.x) + (v.x > 0 ? (v.z < 0 ? 2 * 3.14159265 : 0) : 3.14159265);
	phi /= 2 * 3.14159265; phi = v.x == 0 ? (v.z > 0 ? 0.25 : -0.25) : phi;

	float rho;
	float horiz = length(x);
	horiz = -sqrt(horiz * horiz - _PlanetRadius * _PlanetRadius) / horiz;

	if (v.y < horiz) {
		rho = pow((v.y + 1) / (horiz + 1), 2) * 0.5;
	}
	else {
		float atmosphere_radius = _PlanetRadius + _AtmosphereThickness;
		if (length(x) > atmosphere_radius) {
			float ahoriz = length(x);
			ahoriz = -sqrt(ahoriz * ahoriz - atmosphere_radius * atmosphere_radius) / ahoriz;
			if (v.y > ahoriz) rho = -1;
			else rho = (v.y - horiz) / (ahoriz - horiz) * 0.5 + 0.5;
		}
		else {
			rho = pow((v.y - horiz) / (1 - horiz), 0.5) * 0.5 + 0.5;
		}
	}

	float3 scatter = rho >= 0 ? _Skybox.SampleLevel(sampler_Skybox, float2(phi, rho), 0).xyz : 0;

	// prevent error
	scatter = max(0, scatter);

	float3 sun = 1;
	sun = T(x, v);
	sun *= smoothstep(cos(_SunAngle + 0.005), cos(_SunAngle), dot(v, s));
	sun /= 0.2e4;

	return scatter + sun;
}

void SkyLight(inout RayIntersection rayIntersection, const int distance = 50) {
	if(rayIntersection.weight.w < 0){
		return;
	}
	rayIntersection.t = distance;
	rayIntersection.weight = 0; 
	if (_Procedural == 0) {
		float2 tc = ToRadialCoords(RotateAroundYInDegrees(WorldRayDirection(), -_Rotation));
		float3 x = mul(_V_Inv, float4(0, 0, 0, 1));
		x = float3(0, _PlanetRadius + max(95, x.y), 0);
		rayIntersection.directColor = Scatter(x, WorldRayDirection(), _SunDir) *_SunLuminance;


	//	float3 s = normalize(_SunDir);
	//	float3 x = float3(0, planet_radius + max(95, _WorldSpaceCameraPos.y), 0);
	//	float depth = Linear01Depth(tex2Dlod(_DepthTex, float4(i.uv, 0, 0)));

	//	float3 wpos = GetWorldPositionFromDepthValue(i.uv, depth);
	//	float3 v = wpos - _WorldSpaceCameraPos;
	//	bool sky_occ = depth != 1;
	//	depth = length(v);
	//	v /= depth;

	//	float3 x_0;
	//	X_0(x, v, x_0);
	//	depth = min(depth, distance(x, x_0));

	//	return lerp(ScatterTable(x, v, s) * _SunLuminance, Scatter(i.uv, depth), sky_occ ? 1 - smoothstep(0.9, 1, depth / _MaxDepth) : 0)
	//		+ (sky_occ ? tex2Dlod(_MainTex, float4(i.uv, 0, 0)).xyz : 0) * T(x, x + depth * v);
	}
	else if (_Procedural == 1) {
		float2 tc = ToRadialCoords(RotateAroundYInDegrees(WorldRayDirection(), -_Rotation));

		half4 tex = _Skybox.SampleLevel(sampler_Skybox, tc, rayIntersection.roughness * 7);
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
void SkyLight(inout RayIntersection_RTGI rayIntersection, const int distance = 50)
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