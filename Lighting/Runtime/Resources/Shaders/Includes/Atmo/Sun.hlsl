#ifndef SUN_INCLUDE_
#define SUN_INCLUDE_

struct SunLight {
	float3 dir;
	float angle;
	float3 color;
};

RWStructuredBuffer<SunLight> _Sun;

#endif