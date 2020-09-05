Shader "Hidden/Custom/Atmo"
{
	Properties{ _MainTex("Texture", 2D) = "white" {} }
	HLSLINCLUDE


	#define T T_TAB
	#define L L_0
	#define J_L J_L_0
	#define S_L S_L_SHADE
		
	#include "../Includes/PostCommon.hlsl"
	#include "../Includes/Atmo/Atmo.hlsl"
	#include "../Includes/Atmo/CloudMarching.hlsl"

	float _SunRadiance;

	Texture2D _Depth; SamplerState sampler_Depth;

	float VolumeLight(float depth, float3 x, float3 x_0) {
		float dirSampleNum = 32;
		float3 res = 0;
		float bias = Rand();
		float last_t = 0;
		float dis = distance(x, x_0);
		[loop]
		for (int i = 0; i < dirSampleNum; i++)
		{
			float t = (i + bias) / dirSampleNum;
			t = -log(1 - (1 - 1 / 2.718281828459) * t);
			if (t * dis > depth) break;
			float3 p = lerp(x, x_0, t);

			float delta_t = t - last_t;
			res += 1;// GetShadow(float4(p, 1)).x* delta_t;
			last_t = t;
		}
		return res;
	}

	float3 main(v2f i) : SV_Target
	{
		RandSeed(i.vertex.xy);
		sampleIndex1D = sampleIndex;

		float3 sunRadiance = _SunRadiance;

		float3 camPos = _V_Inv._m03_m13_m23;
		float4 v_ = mul(_VP_Inv, float4(UV * 2 - 1, 0, 1)); v_ /= v_.w;
		float3 v = normalize(v_.xyz - camPos);
		float dotvc = dot(v, -_V._m20_m21_m22);
		float3 s = normalize(-_LightDir);
		float3 x = float3(camPos.x, planet_radius + max(95, camPos.y), camPos.z);
		float depth = LinearEyeDepth(_Depth.SampleLevel(sampler_Depth, UV, 0));
		depth = depth /= dotvc;
		float max_depth = (1.0 / _ZBufferParams.w) * 0.998;

		if (length(x) > atmosphere_radius-1) {
			float space_offset = IntersectSphere(x, v, float3(0, 0, 0), atmosphere_radius-2);
			if (space_offset == 0) {
				return (L(x, v, s) + L(x, v, -s) * 0.0001) * sunRadiance;
			}
			x += v * space_offset;
		}

		float k = min(dot(normalize(x), v), 0);
		float fade = 1 - saturate((length(x) * sqrt(1 - k * k) - planet_radius) / atmosphere_thickness);
		fade = pow(fade, 2);
		float far_clip_fade = smoothstep(0.9, 1, depth / max_depth);

		float3 x_0;
		X_0(x, v, x_0); 
		float dis = distance(x, x_0);
		dis = lerp(min(dis, depth), dis, far_clip_fade);
		x_0 = dis * v + x;

		float3 trans = T(x, x + v * dis);
		float3 sunLight = depth > max_depth ? (L(x, v, s) + L(x, v, -s) * 0.0001) : 0;
		
		float3 atmo_scatter;
		[branch]
		if (s.y < -0.05) atmo_scatter = S_L_Night(x, x_0, -s, 6);
		else atmo_scatter = S_L(x, x_0, s, 8);

		return (atmo_scatter + sunLight) * sunRadiance + (depth > max_depth ? 0 : SampleColor(UV) * trans);
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM

				#pragma vertex Vert
				#pragma fragment main

			ENDHLSL
		}
	}
}