#ifndef PBS_H_
#define PBS_H_

//-----------------------------------------------------------------------------
//
//  Surface Info
//
//-----------------------------------------------------------------------------

struct SurfaceInfo {
	float3	baseColor;
	float	transparent;
	float	metallic;
	float	smoothness;
	float3	normal;
	float3	gnormal;
	float3	emission;
	float2  diffuseAO_specAO;
	float	clearCoat;	//清漆
	float	sheen;		//布料边缘光
	float	index;		//折射率
	float	Ld;			//次表面
	bool	discarded;	//discard surface
};

//-----------------------------------------------------------------------------
//
//  Helper funcs
//
//-----------------------------------------------------------------------------

#define M_E        2.71828182845904523536   // e
#define M_LOG2E    1.44269504088896340736   // log2(e)
#define M_LOG10E   0.434294481903251827651  // log10(e)
#define M_LN2      0.693147180559945309417  // ln(2)
#define M_LN10     2.30258509299404568402   // ln(10)
#define M_PI       3.14159265358979323846   // pi
#define M_PI_2     1.57079632679489661923   // pi/2
#define M_PI_4     0.785398163397448309616  // pi/4
#define M_1_PI     0.318309886183790671538  // 1/pi
#define M_2_PI     0.636619772367581343076  // 2/pi
#define M_2_SQRTPI 1.12837916709551257390   // 2/sqrt(pi)
#define M_SQRT2    1.41421356237309504880   // sqrt(2)
#define M_SQRT1_2  0.707106781186547524401  // 1/sqrt(2)

#ifndef UNITY_COMMON_INCLUDED

inline float Pow4(float x)
{
	return x * x* x* x;
}

inline float2 Pow4(float2 x)
{
	return x * x* x* x;
}

inline float3 Pow4(float3 x)
{
	return x * x* x* x;
}

inline float4 Pow4(float4 x)
{
	return x * x* x* x;
}

#endif

inline float Pow5(float x)
{
	return x * x * x * x * x;
}

inline float2 Pow5(float2 x)
{
	return x * x * x * x * x;
}

inline float3 Pow5(float3 x)
{
	return x * x * x * x * x;
}

inline float4 Pow5(float4 x)
{
	return x * x * x * x * x;
}

inline float OneMinusReflectivityFromMetallic(const float metallic)
{
	float oneMinusDielectricSpec = 1.0 - 0.04;
	return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline float3 DiffuseAndSpecularFromMetallic(const float3 albedo, const float metallic, out float3 specColor, out float oneMinusReflectivity)
{
	specColor = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
	oneMinusReflectivity = OneMinusReflectivityFromMetallic(metallic);
	return albedo * oneMinusReflectivity;
}

inline float SmoothnessToPerceptualRoughness(const float smoothness)
{
	return (1 - smoothness);
}

float DisneyDiffuse(const float NdotV, const float NdotL, const float LdotH, const float sheen, const float perceptualRoughness)
{
	float fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
	// Two schlick fresnel term
	float lightScatter = (1 + (fd90 - 1) * Pow5(1 - NdotL));
	float viewScatter = (1 + (fd90 - 1) * Pow5(1 - NdotV));

	return lightScatter * viewScatter + Pow5(1 - NdotV) * sheen;
}

inline float PerceptualRoughnessToRoughness(const float perceptualRoughness)
{
	return perceptualRoughness * perceptualRoughness;
}

inline float SmithJointGGXVisibilityTerm(const float NdotL, const float NdotV, const float roughness)
{
	float k = (roughness + 0.1) * (roughness + 0.1) / 1.1 / 1.1 / 3;
	return NdotL * NdotV / lerp(NdotL, 1, k) / lerp(NdotV, 1, k);
}

inline float SmithJointGGXVisibilityTerm2(const float NdotL, const float NdotV, const float roughness)
{
	float k = roughness + 1;
	k *= k / 8;
	float a = NdotV + 1e-5;
	float b = NdotL + 1e-5;
	float G_1 = 1 / (a*(1-k) + k);
	float G_2 = 1 / (b*(1-k) + k);
	return G_1 * G_2;
}

inline float GGXTerm(const float NdotH, const float roughness)
{
	float a2 = roughness * roughness;
	float d = (NdotH * a2 - NdotH) * NdotH + 1.0f; // 2 mad
	return M_1_PI * a2 / (d * d + 1e-7f); // This function is not intended to be running on Mobile,
										  // therefore epsilon is smaller than what can be represented by float
}

inline float3 FresnelTerm(const float3 F0, const float cosA)
{
	float t = Pow5(1 - cosA);   // ala Schlick interpoliation
	return F0 + (1 - F0) * t;
}

inline float3 FresnelLerp(const float3 F0, const float3 F90, const float cosA)
{
	float t = Pow5(1 - cosA);   // ala Schlick interpoliation
	return lerp(F0, F90, t);
}

float PhysicsFresnel(float IOR, float3 i, float3 n) {
	float cosi = abs(dot(i, n));
	float sini = sqrt(max(0, 1 - cosi * cosi));
	float sint = sini / IOR;
	float cost = sqrt(max(0, 1 - sint * sint));

	float r1 = (IOR * cosi - cost) / (IOR * cosi + cost);
	float r2 = (cosi - IOR * cost) / (cosi + IOR * cost);
	return (r1 * r1 + r2 * r2) / 2;
}

half4 BRDF1_Unity_PBS(half3 diffColor, half3 specColor, half smoothness,
	float3 normal, float3 viewDir,
	float3 lightDir, float3 lightColor)
{
	float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
	float3 halfDir = normalize(float3(lightDir) + viewDir);

	// The amount we shift the normal toward the view vector is defined by the dot product.
	half shiftAmount = dot(normal, viewDir);
	normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
	// A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
	//normal = normalize(normal);

	float nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here

	float nl = saturate(dot(normal, lightDir));
	float nh = saturate(dot(normal, halfDir));

	half lv = saturate(dot(lightDir, viewDir));
	half lh = saturate(dot(lightDir, halfDir));

	// Diffuse term
	half diffuseTerm = DisneyDiffuse(nv, nl, lh, 0, perceptualRoughness) * nl;

	// Specular term
	// HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
	// BUT 1) that will make shader look significantly darker than Legacy ones
	// and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
	float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

	// GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
	roughness = max(roughness, 0.002);
	float V = SmithJointGGXVisibilityTerm(nl, nv, roughness);
	float D = GGXTerm(nh, roughness);

	float specularTerm = V * D * M_PI; // Torrance-Sparrow model, Fresnel is applied later

	// specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
	specularTerm = max(0, specularTerm * nl);

	// To provide true Lambert lighting, we need to be able to kill specular completely.
	specularTerm *= any(specColor) ? 1.0 : 0.0;

	half3 color = diffColor * lightColor * diffuseTerm
					+ specularTerm * lightColor * FresnelTerm(specColor, lh);

	return half4(color, 1);
}


float3 BRDF(const int type, const float3 diffColor, const float3 specColor, const float smoothness, const float2 ao, const float clearCoat, const float sheen,
	float3 normal, const float3 viewDir, const float3 lightDir,
	const float3 lightSatu) {

	float perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
	float3 floatDir = normalize(lightDir + viewDir);

	float shiftAmount = dot(normal, viewDir);
	normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;

	float nv = saturate(dot(normal, viewDir));

	float nl = saturate(dot(normal, lightDir));
	float nh = saturate(dot(normal, floatDir));

	float lv = saturate(dot(lightDir, viewDir));
	float lh = saturate(dot(lightDir, floatDir));

	float3 diffuseTerm = DisneyDiffuse(nv, nl, lh, sheen, perceptualRoughness);

	float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

	roughness = max(roughness, 0.008);
	float G = SmithJointGGXVisibilityTerm(nl, nv, roughness) * M_PI;
	float D = GGXTerm(nh, roughness);
	float3 F = FresnelTerm(specColor, lh) * (any(specColor) ? 1.0 : 0.0);

	float coatDG = GGXTerm(nh, 0.02) * SmithJointGGXVisibilityTerm(nl, nv, 0.02);
	float3 DFG = D * G;
	DFG = F * lerp(DFG, DFG + coatDG, clearCoat);
	 
	if (type == 1) return (diffuseTerm * diffColor) * lightSatu * ao.x;
	else if (type == 2) return (G * M_1_PI * F) * lightSatu * ao.y;
	else if (type == 4) return nl * (diffuseTerm * diffColor * ao.x + DFG * ao.y) * lightSatu;
	else if (type == 8) return nl * DFG * ao.y * lightSatu;
	else if (type == 16) return nl * diffuseTerm * diffColor * ao.x * lightSatu;
	else return 0;
}
 

#define PBS_DIFFUSE (1)
#define PBS_SPECULAR (2)
#define PBS_FULLY (4)
#define PBS_SS_SPEC (8)
#define PBS_SS_DIFFUSE (16)

float CalculateDiffuseAO(float ao, float3 L, float3 gN) {
	float NdotL = max(0, dot(normalize(L), gN));
	return lerp(ao, 1, NdotL * 0.8 + 0.2);
}
float CalculateSpecAO(float ao, float rougness, float3 V, float3 gN) {
	return lerp(ao + (1 - ao) * rougness * 0.8, 1, saturate(dot(V, gN) * 1.5 - 0.5));
}

float3 PBS(const int type, SurfaceInfo IN, const float3 lightDir, const float3 lightSatu, const float3 viewDir) {
	
	IN.diffuseAO_specAO.x = CalculateDiffuseAO(IN.diffuseAO_specAO.x, lightDir, IN.gnormal);
	IN.diffuseAO_specAO.y = CalculateSpecAO(IN.diffuseAO_specAO.y, 1 - IN.smoothness, viewDir, IN.gnormal);

	float3 color;

	float oneMinusReflectivity;
	float3 baseColor, specColor;
	baseColor = DiffuseAndSpecularFromMetallic(IN.baseColor, IN.metallic, /*ref*/ specColor, /*ref*/ oneMinusReflectivity);

	float3 normal = IN.normal;

	baseColor *= 1 - IN.transparent;
	color = BRDF(type, baseColor, specColor, IN.smoothness, IN.diffuseAO_specAO, IN.clearCoat, IN.sheen, normal, viewDir, lightDir, lightSatu);

	return color;
}

inline float3 DiffuseAndSpecularFromMetallic(const float3 albedo, const float metallic, out float3 specColor)
{
	specColor = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
	float oneMinusReflectivity = OneMinusReflectivityFromMetallic(metallic);
	return albedo * oneMinusReflectivity;
}

#endif // !PBS_H_