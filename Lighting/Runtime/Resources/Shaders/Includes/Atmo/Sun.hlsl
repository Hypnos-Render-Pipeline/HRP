#ifndef SUN_INCLUDE_
#define SUN_INCLUDE_

struct SunLight {
	float3 dir;
	float angle;
	float3 color;
};

cbuffer _Sun {
	float3 sunDir;
	float sunAngle;
	float3 sunColor;
};

#endif