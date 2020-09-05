#ifndef COMMON_INCLUDE  
#define COMMON_INCLUDE  

//#include "../../PostProcessing/Shaders/StdLib.hlsl"
#include "UnityCG.cginc"

float4x4 _V, _V_Inv;
float4x4 _VP_Inv;

uint _Clock;

#define UV i.texcoord

#define TEX_SIZE _ScreenParams.xy

Texture2D _MainTex;
SamplerState sampler_MainTex;

inline float3 SampleColor(float2 uv)
{
	return _MainTex.SampleLevel(sampler_MainTex, uv, 0).rgb;
}

#ifdef CONV_VERT
#define CUSTOM_VERT

struct v2f
{
	float4 vertex : SV_POSITION;
	float2 texcoord : TEXCOORD0;
	float2 texcoordStereo : TEXCOORD1;
	half2 uv[9] : TEXCOORD2;
};

struct vert_data
{
	float3 vertex : POSITION;
	float2 uv : TEXCOORD0;
};

const static float w[9] = { 0.125, 0.075, 0.125, 0.075,0.2, 0.075, 0.125, 0.075, 0.125 };

v2f Vert(vert_data v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.texcoord = v.uv;

#if UNITY_UV_STARTS_AT_TOP
	o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
#endif
	float2 _Tex_Size = 1.0f / TEX_SIZE;
	half2 uv = o.texcoord;
	o.uv[0] = uv + _Tex_Size.xy * half2(-1, -1);
	o.uv[1] = uv + _Tex_Size.xy * half2(0, -2);
	o.uv[2] = uv + _Tex_Size.xy * half2(1, -1);
	o.uv[3] = uv + _Tex_Size.xy * half2(-2, 0);
	o.uv[4] = uv + _Tex_Size.xy * half2(0, 0);
	o.uv[5] = uv + _Tex_Size.xy * half2(2, 0);
	o.uv[6] = uv + _Tex_Size.xy * half2(-1, 1);
	o.uv[7] = uv + _Tex_Size.xy * half2(0, 2);
	o.uv[8] = uv + _Tex_Size.xy * half2(1, 1);

	o.texcoordStereo = TransformStereoScreenSpaceTex(o.texcoord, 1.0);

	return o;
}


#endif





#ifndef CUSTOM_VERT

struct v2f
{
	float4 vertex : SV_POSITION;
	float2 texcoord : TEXCOORD0;
};

struct vert_data
{
	float3 vertex : POSITION;
	float2 uv : TEXCOORD0;
};

v2f Vert(vert_data v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.texcoord = v.uv;

	return o;
}

#endif

//-------------------------------------------------

float2 CosSin(float theta)
{
	float sn, cs;
	sincos(theta, sn, cs);
	return float2(cs, sn);
}

#endif