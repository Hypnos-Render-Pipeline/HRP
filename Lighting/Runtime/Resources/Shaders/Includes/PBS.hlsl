#ifndef PBS_H_
#define PBS_H_

//-----------------------------------------------------------------------------
//
//  Surface Info
//
//-----------------------------------------------------------------------------

struct SurfaceInfo {
	float3	diffuse;
	float3	specular;
	float	transparent;
	float	smoothness;
	float   aniso;
	float3	normal;
	float3	tangent;
	float3	gnormal;
	float3	emission;
	float2  diffuseAO_specAO;
	bool	iridescence;
	bool	specF;
	float	index2;
	float	dinc;
	float	clearCoat;	
	float	sheen;		
	float	index;		
	float	Ld;			
	bool	discarded;
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

float2 CalculateAnisoRoughness(float roughness, float aniso) {

	float k = sqrt(-0.9 * abs(aniso) + 1);
	k = aniso > 0 ? k : 1 / k;
	return min(1, max(0.008, float2(roughness * k, roughness / k)));
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
	float k = (roughness + 0.01) * (roughness + 0.01) / 1.01 / 1.01 / 2;
	return (1e-5f + NdotL * NdotV) / (1e-5f + lerp(NdotL, 1, k) * lerp(NdotV, 1, k));
}

inline float GGXTerm(const float NdotH, const float roughness)
{
	float a2 = roughness * roughness;
	float d = (NdotH * a2 - NdotH) * NdotH + 1.0f; // 2 mad
	return M_1_PI * a2 / (d * d + 1e-7f);
}

inline float AnisoGGXTerm(const float NoH, const float2 roughness, float3 H, float3 X, float3 Y)
{
	float ax = roughness.x;
	float ay = roughness.y;
	float XoH = dot(X, H);
	float YoH = dot(Y, H);
	float d = XoH * XoH / (ax * ax) + YoH * YoH / (ay * ay) + NoH * NoH;
	return M_1_PI / (ax * ay * d * d);
}

float SmithJointAniso(const float2 roughness, float NoV, float NoL, float3 V, float3 L, float3 X, float3 Y)
{
	float ax = roughness.x;
	float ay = roughness.y;
	float XoV = dot(X, V);
	float YoV = dot(Y, V);
	float XoL = dot(X, L);
	float YoL = dot(Y, L);
	float Vis_SmithV = NoL * length(float3(ax * XoV, ay * YoV, NoV));
	float Vis_SmithL = NoV * length(float3(ax * XoL, ay * YoL, NoL));
	return 0.5 * rcp(Vis_SmithV + Vis_SmithL);
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

//------------Iridescence Fresnel------------
float sqr(float x) { return x * x; }
float2 sqr(float2 x) { return x * x; }
float depol(float2 polV) { return 0.5 * (polV.x + polV.y); }
float3 depolColor(float3 colS, float3 colP) { return 0.5 * (colS + colP); }
void fresnelDielectric(in float ct1, in float n1, in float n2,
	out float2 R, out float2 phi) {
	n1 = max(1, n1);
	n2 = max(1, n2);
	float st1 = (1 - ct1 * ct1); // Sinus theta1 'squared'
	float nr = n1 / n2;

	if (sqr(nr) * st1 > 1) { // Total reflection
		R = 1;
		phi = 2.0 * atan(float2(-sqr(nr) * sqrt(st1 - 1.0 / sqr(nr)) / ct1,
			-sqrt(st1 - 1.0 / sqr(nr)) / ct1));
	}
	else {   // Transmission & Reflection

		float ct2 = sqrt(1 - sqr(nr) * st1);
		float2 r = float2((n2 * ct1 - n1 * ct2) / (n2 * ct1 + n1 * ct2),
			(n1 * ct1 - n2 * ct2) / (n1 * ct1 + n2 * ct2));
		phi.x = (r.x < 0.0) ? M_PI : 0.0;
		phi.y = (r.y < 0.0) ? M_PI : 0.0;
		R = sqr(r);
	}
}

void fresnelConductor(in float ct1, in float n1, in float n2, in float k,
	out float2 R, out float2 phi) {

	if (k == 0) { // use dielectric formula to avoid numerical issues
		fresnelDielectric(ct1, n1, n2, R, phi);
	}
	else {
		float A = sqr(n2) * (1 - sqr(k)) - sqr(n1) * (1 - sqr(ct1));
		float B = sqrt(sqr(A) + sqr(2 * sqr(n2) * k));
		float U = sqrt((A + B) / 2.0);
		float V = sqrt((B - A) / 2.0);

		R.y = (sqr(n1 * ct1 - U) + sqr(V)) / (sqr(n1 * ct1 + U) + sqr(V));
		phi.y = atan2(2 * n1 * V * ct1, sqr(U) + sqr(V) - sqr(n1 * ct1)) + M_PI;

		R.x = (sqr(sqr(n2) * (1 - sqr(k)) * ct1 - n1 * U) + sqr(2 * sqr(n2) * k * ct1 - n1 * V))
			/ (sqr(sqr(n2) * (1 - sqr(k)) * ct1 + n1 * U) + sqr(2 * sqr(n2) * k * ct1 + n1 * V));
		phi.x = atan2(2 * n1 * sqr(n2) * ct1 * (2 * k * U - (1 - sqr(k)) * V), sqr(sqr(n2) * (1 + sqr(k)) * ct1) - sqr(n1) * (sqr(U) + sqr(V)));
	}
}

// Evaluation XYZ sensitivity curves in Fourier space
float3 evalSensitivity(float opd, float shift) {

	// Use Gaussian fits, given by 3 parameters: val, pos and var
	float phase = 2 * M_PI * opd * 1.0e-6;
	float3 val = float3(5.4856e-13, 4.4201e-13, 5.2481e-13);
	float3 pos = float3(1.6810e+06, 1.7953e+06, 2.2084e+06);
	float3 var = float3(4.3278e+09, 9.3046e+09, 6.6121e+09);
	float3 xyz = val * sqrt(2 * M_PI * var) * cos(pos * phase + shift) * exp(-var * phase * phase);
	xyz.x += 9.7470e-14 * sqrt(2 * M_PI * 4.5282e+09) * cos(2.2399e+06 * phase + shift) * exp(-4.5282e+09 * phase * phase);
	return xyz / 1.0685e-7;
}

float3 IridescenceFresnel(float cosTheta1, float cosTheta2, float eta_2, float eta_3, float kappa_3, float d) {
	// First interface
	float2 R12, phi12;
	fresnelDielectric(cosTheta1, 1.0, eta_2, R12, phi12);
	float2 R21 = R12;
	float2 T121 = 1.0 - R12;
	float2 phi21 = M_PI - phi12;

	// Second interface
	float2 R23, phi23;
	fresnelConductor(cosTheta2, eta_2, eta_3, kappa_3, R23, phi23);

	// Phase shift
	float OPD = d * cosTheta2;
	float2 phi2 = phi21 + phi23;

	// Compound terms
	float3 I = 0;
	float2 R123 = R12 * R23;
	float2 r123 = sqrt(R123);
	float2 Rs = sqr(T121) * R23 / (1 - R123);

	// Reflectance term for m=0 (DC term amplitude)
	float2 C0 = R12 + Rs;
	float3 S0 = evalSensitivity(0.0, 0.0);
	I += depol(C0) * S0;

	// Reflectance term for m>0 (pairs of diracs)
	float2 Cm = Rs - T121;
	for (int m = 1; m <= 3; ++m) {
		Cm *= r123;
		float3 SmS = 2.0 * evalSensitivity(m * OPD, m * phi2.x);
		float3 SmP = 2.0 * evalSensitivity(m * OPD, m * phi2.y);
		I += depolColor(Cm.x * SmS, Cm.y * SmP);
	}

	// Convert back to RGB reflectance
	float3x3 XYZ_TO_RGB = float3x3(3.0799327, -1.537150, -0.542782, -0.921235, 1.875992, 0.0452442, 0.0528909, -0.204043, 1.1511515);
	I = clamp(mul(XYZ_TO_RGB, I), 0.0, 1.0);
	return I;
}
//#undef sqr


float3 BRDF(const int type, const float3 diffColor, const float3 specColor, const float smoothness, const float aniso, const float2 ao, const float clearCoat, const float sheen,
	const bool iridescence, const float eta_2, const float eta_3, const float kappa_3, const float dinc, const bool specF,
	float3 normal, float3 tangent, const float3 viewDir, const float3 lightDir,
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

	float G;
	float D;

	if (aniso != 0) {
		float3 X = tangent;
		float3 Y = normalize(cross(X, normal));
		X = cross(normal, Y);
		float2 r2 = CalculateAnisoRoughness(roughness, aniso);
		D = AnisoGGXTerm(nh, r2, floatDir, X, Y);
		G = SmithJointAniso(r2, nv, nl, viewDir, lightDir, X, Y) * M_PI;
	}
	else{
		roughness = max(roughness, 0.008);
		D = GGXTerm(nh, roughness);
		G = SmithJointGGXVisibilityTerm(nl, nv, roughness) * M_PI;
	}

	float3 F;
	if (iridescence) {
		float cosTheta2 = sqrt(1.0 - (1 - nv * nv) / max(0.0000001, eta_2 * eta_2));
		F = IridescenceFresnel(nv, cosTheta2, eta_2, eta_3, kappa_3, dinc);
	}
	else
		F = specF ? specColor : FresnelTerm(specColor, lh) * (any(specColor) ? 1.0 : 0.0);

	float coatDG = GGXTerm(nh, 0.02) * SmithJointGGXVisibilityTerm(nl, nv, 0.02);
	float3 DFG = D * G;
	DFG = F * lerp(DFG, DFG + coatDG, clearCoat);
	
	if (type == 1) return (diffuseTerm * diffColor) * lightSatu * ao.x;
	else if (type == 2) return G * M_1_PI * F * lightSatu * ao.y;
	else if (type == 4) return (nl * diffuseTerm * diffColor * ao.x + nh * DFG * ao.y) * lightSatu;
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

	float3 diffuse;
	diffuse = IN.diffuse;

	diffuse *= 1 - IN.transparent;
	color = BRDF(type, diffuse, IN.specular, IN.smoothness, IN.aniso, IN.diffuseAO_specAO, IN.clearCoat, IN.sheen,
		IN.iridescence, IN.index, IN.index2, max(IN.specular.x, max(IN.specular.y, IN.specular.z)), IN.dinc, IN.specF,
		IN.normal, IN.tangent, viewDir, lightDir, lightSatu);

	return color;
}

inline float3 DiffuseAndSpecularFromMetallic(const float3 albedo, const float metallic, out float3 specColor)
{
	specColor = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
	float oneMinusReflectivity = OneMinusReflectivityFromMetallic(metallic);
	return albedo * oneMinusReflectivity;
}

#endif // !PBS_H_