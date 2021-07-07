#ifndef LitInclude_H_
#define LitInclude_H_

#include "./RayTracingCommon.hlsl"
#include "./Sampler.hlsl"
#include "../../PBS.hlsl"
#include "./TraceRay.hlsl"
#include "./RTFog.hlsl"
//#include "./RayTracingUtile.hlsl"
#include "./RayTracingLight.hlsl"

#ifndef Shading 
#define Shading LitShading
#endif

half3 UnpackScaleNormalRGorAG(half4 packednormal, half bumpScale)
{
	half3 normal;
	normal.xy = (packednormal.wy * 2 - 1);

	// SM2.0: instruction count limitation
	// SM2.0: normal scaler is not supported
	normal.xy *= bumpScale;

	normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
	return normal;
}

half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
{
	return UnpackScaleNormalRGorAG(packednormal, bumpScale);
}

#define SampleTex(tex, uv, mip) (tex.SampleLevel(sampler_MainTex, uv, mip))

float bum_alpha2(float3 gN, float3 sN) {
	float cos_d = min(abs(dot(gN, sN)), 1);
	float tan2_d = (1 - cos_d * cos_d) / (cos_d * cos_d);
	return saturate(0.125 * tan2_d);
}
float bump_shadowing_function(float3 gN, float3 L, float alpha2) {
	float cos_i = max(abs(dot(gN,L)), 1e-6);
	float tan2_i = (1 - cos_i * cos_i) / (cos_i * cos_i);
	return 2.0f / (1 + sqrt(1 + alpha2 * tan2_i));
}

#if _SUBSURFACE

float _Ld;
Texture2D   _ScatterProfile; SamplerState sampler_ScatterProfile;

float Rd(float r, float v) {
	return exp(-r * r / 2 / v) / (2 * PI * v);
}

float RdDiskPdf(float r, float v) {
	return Rd(r, v)/* / (1 - exp(- 2 / 12.46))*/;
}

float SampleRd(float v, float E) {
	return sqrt(-2 * v * log(1 - E * (1 - exp(-2 / 12.46))));
}

float3 SampleRd(float v, float2 E) {
	float Theta = 2.0f * (float)PI * E.x;
	float Radius = SampleRd(v, E.y);
	return float3(Radius * cos(Theta), Radius * sin(Theta), Radius);
}

void SampleSS(float3 E, float3 ns, float3 ss, float3 ts, float3 p, float Ld,
					inout float3 diffuse, inout int4 sampleState,
					out float pdf, out float3 hitP, out float3 hitN) {

	// Choose projection axis for BSSRDF sampling
	float3 vx, vy, vz;

	vx = ss;
	vy = ts;
	vz = ns;
	if (E.x < 0.5f) {
		vx = ss;
		vy = ts;
		vz = ns;
	}
	else if (E.x < 0.75f) {
		vz = normalize(ts + ns * 2);
		vx = normalize(cross(vz, ss));
		vy = cross(vx, vz);
		//vx = ns;
		//vy = ss;
		//vz = ts;
	}
	else {
		vz = normalize(ss + ns * 2);
		vy = normalize(cross(ts, vz));
		vx = cross(vz, vy);
		//vx = ts;
		//vy = ns;
		//vz = ss;
	}
	// Compute d
	float3 d_ = max(0.0001, dFromALd(Ld * Ld * Ld * 10, 1));
	float d = dot(d_, 0.3333333);
	// Compute BSSRDF profile bounds and intersection height
	float3 sampleRd = SampleRd(d, E.yz);
	float max_d = SampleRd(d, 1) + 0.01;
	float l = sqrt(max_d * max_d - sampleRd.z * sampleRd.z);
	// Compute BSSRDF sampling ray segment
	float3 pos = p + (vx * sampleRd.x + vy * sampleRd.y) - l * vz * 0.5f;
	float3 albedo;

	int hitNum;
	float4 t = TraceSelf(pos, vz, l,
							sampleState,
							hitNum, albedo, hitN);

	if (hitNum == 0) {
		pdf = 1;
		hitP = /*vz * l +*/ pos;
		hitN = vz;
		return;
	}
	//hitNum = 1;

	// Compute pdf
	hitP = t.yzw;
	float hitT = length(hitP - p);
	float3 nLocal = abs(float3(dot(hitN, normalize(ts + ns * 2)), dot(hitN, normalize(ss + ns * 2)), dot(hitN, ns)));

	//pdf = RdDiskPdf(sampleRd.z, d) / Rd(hitT, d) * dot(abs(nLocal), float3(0, 0, 1)) / hitNum;
	pdf = RdDiskPdf(sampleRd.z, d) / Rd(hitT, d) * dot(abs(nLocal), float3(0.25, 0.25, 0.5)) / hitNum;
	diffuse = sqrt(diffuse * albedo);
	float v = hitT / sqrt(d + 0.01) / 0.5665927;

	diffuse *= _ScatterProfile.SampleLevel(sampler_ScatterProfile, float2(v, 0.5), 0);
}
#endif

SurfaceInfo GetSurfaceInfo(inout FragInputs i);

void Shading(FragInputs IN, const float3 viewDir,
	inout int4 sampleState, inout float4 weight, inout float3 position, inout float rayRoughness,
	out float3 directColor, out float3 nextDir);

void LitShading(FragInputs IN, const float3 viewDir, 
				inout int4 sampleState, inout float4 weight, inout float3 position, inout float rayRoughness,
				out float3 directColor, out float3 nextDir)
{
	SurfaceInfo surface = GetSurfaceInfo(IN);

	// increase bias but remove many flare points.
	surface.smoothness = min(1 - rayRoughness, surface.smoothness);


	directColor = surface.emission;
	
	//directColor = InstanceID() / 10.0;
	//weight = END_TRACE;
	//return;

	//----------------------------------------------------------------------------------------
	//--------- direct light -----------------------------------------------------------------
	//----------------------------------------------------------------------------------------
	bool useSpecLightDir = SAMPLE < surface.smoothness;
	{
		int light_count = clamp(_LightCount, 0, 100);
		if (light_count)
		{
			float2 rand_num_light = SAMPLE;
			Light light = _LightList[floor(min(rand_num_light.x, 0.99) * light_count)];

			if (IsDeltaLight(light)) useSpecLightDir = false;

			float3 attenuation;
			float3 lightDir;
			float3 end_point;

			bool in_light_range = ResolveLight(light, IN.position,
				/*inout*/sampleState,
				/*out*/attenuation, /*out*/lightDir, /*out*/end_point);

			float3 luminance = attenuation * light.color;
#ifdef _SUBSURFACE
			float3 direct_light_without_shadow = PBS(PBS_SS_SPEC, surface, lightDir, luminance, viewDir);
#else
			float3 direct_light_without_shadow;
			if (useSpecLightDir) direct_light_without_shadow = max(0, dot(surface.normal, lightDir)) * PBS(PBS_DIFFUSE, surface, lightDir, luminance, viewDir);
			else direct_light_without_shadow = PBS(PBS_FULLY, surface, lightDir, luminance, viewDir);
#endif
			[branch]
			if (in_light_range && dot(direct_light_without_shadow, 1) > 0) {
				float3 position_offset = IN.position;

				float3 shadow = TraceShadowWithFog_PreventSelfShadow(position_offset, end_point,
					/*inout*/sampleState);

				directColor += shadow * direct_light_without_shadow * light_count;
			}
		}
		else sampleState.w++;
	}
#ifndef _SUBSURFACE
	if (useSpecLightDir) {
		float2 sample_2D;
		sample_2D.x = SAMPLE;
		sample_2D.y = SAMPLE;

		float3 v_t = float3(dot(IN.tangentToWorld[0], viewDir), dot(IN.tangentToWorld[1], viewDir), dot(IN.tangentToWorld[2], viewDir));
		float2 r2 = CalculateAnisoRoughness(1 - surface.smoothness, surface.aniso);
		float3 n = ImportanceSampleAnisoVGGX(sample_2D, v_t, r2);
		n = mul(n, IN.tangentToWorld);
		nextDir = reflect(-viewDir, n);

		if (dot(nextDir, surface.normal) > 0) {
			directColor += PBS(PBS_SPECULAR, surface, nextDir, LightLuminanceCameraWithFog(IN.position, nextDir, sampleState), viewDir);
		}
	}
#endif

	//----------------------------------------------------------------------------------------
	//--------- indirect light ---------------------------------------------------------------
	//----------------------------------------------------------------------------------------		
	weight.w = 1;
	float2 rand_num = float2(SAMPLE, SAMPLE);
				
	// choice tracing type based on surface data
	float3 specular = surface.specular;
	float3 diffuse = surface.diffuse;

	float3 F = FresnelTerm(specular, saturate(dot(viewDir, surface.normal)));
	float3 diff = diffuse;
	float max_diffuse = max(max(diff.x, diff.y), diff.z);
	float max_ref = max(max(F.x, F.y), F.z);
				  
	float4 refr_diff_refl_coat;
	refr_diff_refl_coat.x = max_diffuse * surface.transparent * (1 - surface.clearCoat);
	refr_diff_refl_coat.y = max_diffuse * (1 - surface.transparent) * (1 - surface.clearCoat);
	refr_diff_refl_coat.z = max_ref * (1 - surface.clearCoat);
	refr_diff_refl_coat.w = surface.clearCoat;
	float sum_w = dot(refr_diff_refl_coat, 1);
	refr_diff_refl_coat /= sum_w;
	float3 threashold = refr_diff_refl_coat.xyz;
	threashold.y += threashold.x;
	threashold.z += threashold.y;
				
	if (rand_num.x <= threashold.x) //透射
	{
		float2 sample_2D;
		sample_2D.x = SAMPLE;
		sample_2D.y = SAMPLE;

		float2 r2 = CalculateAnisoRoughness(1 - surface.smoothness, surface.aniso);
		float3 n = ImportanceSampleAnisoGGX(sample_2D, r2);
		n = mul(n, IN.tangentToWorld);

#if _OIT
		float3 coef = IN.isFrontFace ? (1 - F) : diffuse * surface.transparent;
#else
		float3 coef = IN.isFrontFace ? (1 - F) : pow(max(0.01, diffuse * surface.transparent), RayTCurrent());
#endif
		weight.xyz = coef / threashold.x;

		float IOR = IN.isFrontFace ? (1.0f / surface.index) : surface.index;
#if _OIT
		IOR = 1;
#endif
		float3 next_dir = refract(-viewDir, n, IOR);
		bool all_reflect = length(next_dir) < 0.5;

		float r_thre = IN.isFrontFace ? 0 : PhysicsFresnel(1.0 / IOR, viewDir, surface.normal);
		rayRoughness = surface.index != 1 ? 1 - surface.smoothness : rayRoughness;
		if (all_reflect) r_thre = 1;
		if (rand_num.y < r_thre) {
			nextDir = reflect(-viewDir, n);
		}
		else {
			nextDir = next_dir;
			weight *= IOR;
			if (!IN.isFrontFace) {
				directColor += weight.xyz * LightLuminanceCameraWithFog(IN.position, nextDir, sampleState);
			}
		}
	}
	else if (rand_num.x <= threashold.y) { //漫射
#if _SUBSURFACE
		if (IN.isFrontFace) {
			float pdf = 1;

			float3x3 mat = GetMatrixFromNormal(surface.normal, float2(SAMPLE, SAMPLE));

			SampleSS(float3(SAMPLE, SAMPLE, SAMPLE), mat[2], mat[0], mat[1], IN.position, surface.Ld, 
				surface.diffuse, sampleState,
				pdf, position, surface.normal);

			{
				float3 direct_light = 0;
				float2 rand_num_light = SAMPLE; /*sampleState.w++;*/
				int light_count = clamp(_LightCount, 0, 100);
				if (light_count)
				{
					Light light = _LightList[floor(min(rand_num_light.y, 0.99) * light_count)];

					float3 attenuation;
					float3 lightDir;
					float3 end_point;

					bool in_light_range = ResolveLight(light, position,
						/*inout*/sampleState,
						/*out*/attenuation, /*out*/lightDir, /*out*/end_point);

					float3 luminance = attenuation * light.color;

					float NdotL = saturate(dot(lightDir, surface.normal));

					float3 direct_light_without_shadow = NdotL * PBS(PBS_DIFFUSE, surface, lightDir, luminance, viewDir);

					[branch]
					if (in_light_range && dot(direct_light_without_shadow, 1) > 0) {
						float3 shadow = TraceShadowWithFog_PreventSelfShadow(position, end_point,
							/*inout*/sampleState);

						direct_light += shadow * direct_light_without_shadow * light_count;
					}
				}
				directColor += direct_light / refr_diff_refl_coat.y / (rayRoughness == 0 ? pdf : 1);
			}

			{
				float2 sample_2D;
				sample_2D.x = SAMPLE;
				sample_2D.y = SAMPLE;

				nextDir = CosineSampleHemisphere(sample_2D, surface.normal);

				float3 coef = PBS(PBS_DIFFUSE, surface, nextDir, 1, viewDir);

				weight.xyz = (1 - surface.transparent) * coef / refr_diff_refl_coat.y / (rayRoughness == 0 ?  pdf : 1);
				rayRoughness = 1;
			}
		}
		else {
#endif
			float2 sample_2D; sampleState.w += 2;
			sample_2D.x = SAMPLE;
			sample_2D.y = SAMPLE;

			nextDir = CosineSampleHemisphere(sample_2D, surface.normal);
			rayRoughness = 1;

			float3 coef = PBS(PBS_DIFFUSE, surface, nextDir, 1, viewDir);

			weight.xyz = (1 - surface.transparent) * coef / refr_diff_refl_coat.y;

#if _SUBSURFACE
		}
#endif
		weight.xyz *= 1 - surface.clearCoat;
	} 
	else { // 反射

		float2 sample_2D;
		sample_2D.x = SAMPLE;
		sample_2D.y = SAMPLE;

		float3 X = surface.tangent;
		float3 Y = normalize(cross(X, surface.normal));
		X = cross(surface.normal, Y);
		IN.tangentToWorld = float3x3(X, Y, surface.normal);
#if _CLEARCOAT
		if (rand_num.x <= threashold.z) {
#endif
			float3 v_t = float3(dot(IN.tangentToWorld[0], viewDir), dot(IN.tangentToWorld[1], viewDir), dot(IN.tangentToWorld[2], viewDir));
			float2 r2 = CalculateAnisoRoughness(1 - surface.smoothness, surface.aniso);
			float3 n = ImportanceSampleAnisoVGGX(sample_2D, v_t, r2);
			n = mul(n, IN.tangentToWorld);
			nextDir = reflect(-viewDir, n);
			rayRoughness = 1 - surface.smoothness;

			if (dot(nextDir, surface.normal) > 0 && dot(nextDir, IN.gN) > 0) {
				float coat = surface.clearCoat;
				surface.clearCoat = 0;
				float3 coef = PBS(PBS_SPECULAR, surface, nextDir, 1.0, viewDir);

				weight.xyz = coef * (1 - coat) / refr_diff_refl_coat.z;
			}
			else {
				weight = END_TRACE;
			}
#if _CLEARCOAT
		}
		else {
			surface.smoothness = min(1 - rayRoughness, 1);

			float3 v_t = float3(dot(IN.tangentToWorld[0], viewDir), dot(IN.tangentToWorld[1], viewDir), dot(IN.tangentToWorld[2], viewDir));
			float3 n = ImportanceSampleAnisoVGGX(sample_2D, v_t, max((1 - surface.smoothness), 0.008));
			n = mul(n, IN.tangentToWorld);
			nextDir = reflect(-viewDir, n);
			rayRoughness = 1 - surface.smoothness;

			if (dot(nextDir, surface.normal) > 0 && dot(nextDir, IN.gN) > 0) {
				float coat = surface.clearCoat;
				surface.clearCoat = 0;
				surface.specular = min(1 - rayRoughness, 1);
				surface.diffuse = min(1 - rayRoughness, 1);
				float3 coef = PBS(PBS_SPECULAR, surface, nextDir, 1.0, viewDir);

				weight.xyz = coef * coat / refr_diff_refl_coat.w;
			}
			else {
				weight = END_TRACE;
			}

			directColor += weight.xyz * LightLuminanceCameraWithFog(IN.position, nextDir, sampleState);
		}
#endif

	}

//#else
//	weight.w = END_TRACE;
//#endif //_TRACE_INDIRECT
}


void LitClosestHit(inout RayIntersection rayIntersection, AttributeData attributeData) {
	rayIntersection.t.x = RayTCurrent();
	if (rayIntersection.weight.w < TRACE_SELF) return;

	CALCULATE_DATA(fragInput, viewDir);

	rayIntersection.t.yzw = fragInput.position;

#if _SUBSURFACE
	if (rayIntersection.weight.w == TRACE_SELF)
	{
		SurfaceInfo surface = GetSurfaceInfo(fragInput);

		float3 diffuse = surface.diffuse;

		rayIntersection.directColor = diffuse;
		// surface info is based on face front properties, so flip it back.
		rayIntersection.nextDir = fragInput.gN * (fragInput.isFrontFace ? 1 : -1);
		rayIntersection.normal = surface.normal * (fragInput.isFrontFace ? 1 : -1);
		return;
	}
#endif

	float3 new_position = rayIntersection.t.yzw;
	Shading(fragInput, viewDir,
		/*inout*/rayIntersection.sampleState, /*inout*/rayIntersection.weight, /*inout*/rayIntersection.t.yzw, /*inout*/rayIntersection.roughness,
		/*out*/rayIntersection.directColor, /*out*/rayIntersection.nextDir);
}

#if _OIT
#define INDEX_COMPARE 1
#else
#define INDEX_COMPARE surface.index == 1
#endif

// because 'IgnoreHit' can only be call at AnyHit func, so we have to use macros.
#if _SUBSURFACE
#define LitAnyHit(rayIntersection, attributeData) CALCULATE_DATA(fragInput, viewDir);\
	SurfaceInfo surface = GetSurfaceInfo(fragInput);\
	if (surface.discarded || rayIntersection.weight.w < TRACE_SHADOW) {\
		IgnoreHit();\
		return;\
	}\
	if (rayIntersection.weight.w == TRACE_SHADOW) {\
		if (rayIntersection.weight.x == InstanceID()) {\
			if (rayIntersection.weight.y == PrimitiveIndex() || abs(dot(surface.normal, WorldRayDirection())) < 0.14) {\
				IgnoreHit();\
				return;\
			}\
		}\
		if (INDEX_COMPARE) {\
			float3 diffuse = surface.diffuse;\
			rayIntersection.directColor *= surface.transparent * diffuse;\
			if (max(rayIntersection.directColor.x, max(rayIntersection.directColor.y, rayIntersection.directColor.z)) == 0) {\
				AcceptHitAndEndSearch();\
			}\
			else {\
				IgnoreHit();\
				return;\
			}\
		}\
		else {\
			rayIntersection.directColor *= 0;\
			AcceptHitAndEndSearch();\
			return;\
		}\
	}\
	else if (rayIntersection.weight.w == TRACE_SELF) {\
		if (rayIntersection.weight.x != InstanceID()) {\
			IgnoreHit();\
			return;\
		}\
	}
#else
#define LitAnyHit(rayIntersection, attributeData) CALCULATE_DATA(fragInput, viewDir);\
	SurfaceInfo surface = GetSurfaceInfo(fragInput);\
	if (surface.discarded || rayIntersection.weight.w < TRACE_SHADOW) {\
		IgnoreHit();\
		return;\
	}\
	if (rayIntersection.weight.w == TRACE_SHADOW) {\
		if (rayIntersection.weight.x == InstanceID()) {\
			if (rayIntersection.weight.y == PrimitiveIndex() || abs(dot(surface.normal, WorldRayDirection())) < 0.14) {\
				IgnoreHit();\
				return;\
			}\
		}\
		if (INDEX_COMPARE) {\
			float3 diffuse = surface.diffuse;\
			rayIntersection.directColor *= surface.transparent * diffuse;\
			if (max(rayIntersection.directColor.x, max(rayIntersection.directColor.y, rayIntersection.directColor.z)) == 0) {\
				AcceptHitAndEndSearch();\
				return;\
			}\
			else {\
				IgnoreHit();\
				return;\
			}\
		}\
		else {\
			rayIntersection.directColor *= 0;\
			AcceptHitAndEndSearch();\
			return;\
		}\
	}\
	else if (rayIntersection.weight.w == TRACE_SELF) {\
		IgnoreHit();\
		return;\
	}
#endif


#endif 