#ifndef ATMO_INCLUDE_
#define ATMO_INCLUDE_

#include "./TextureSampler.hlsl"

#define T_CAL     0
#define T_TAB     1

#ifndef T
#define T T_CAL
#endif

#if T == 0
#undef T_CAL
#undef T
#define T T_cal
#define T_CAL_
#elif T == 1
#undef T_TAB
#undef T
#define T T_tab
#define T_TAB_
#endif

float2 _TLutResolution;
float2 _SLutResolution;

float _PlanetRadius;
float _AtmosphereThickness;
float3 _GroundAlbedo;
float3 _RayleighScatter;
float _MeiScatter;
float _OZone;
float _SunAngle;
float3 _SunDir;
float3 _SunLuminance;
float3 _SunColor;

float _MultiScatterStrength;

float _MaxDepth;                

float _Multiplier;

#define pi (3.14159265359)
#define float_max (1e100f)

#define planet_radius _PlanetRadius
#define atmosphere_thickness _AtmosphereThickness
#define sun_angle _SunAngle

uint sampleIndex1D = 0;
uint sampleIndex2D = 0;

float3 GroundAlbedo(const float3 x_0) {
	return _GroundAlbedo;
}


sampler2D T_table;
sampler2D S_table;
sampler2D MS_table;
sampler3D Volume_table;


float Roberts1(uint n) {
	const float g = 1.6180339887498948482;
	const float a = 1.0 / g;
	return  frac(0.5 + a * n);
}
float2 Roberts2(uint n) {
	const float g = 1.32471795724474602596;
	const float2 a = float2(1.0 / g, 1.0 / (g * g));
	return  frac(0.5 + a * n);
}

#define SAMPLE1D (Roberts1(sampleIndex1D++))
#define SAMPLE2D (Roberts2(sampleIndex2D++))

inline const float valid(const float x) {
	return x < 0.0f ? float_max : x;
}

#define atmosphere_radius (planet_radius + atmosphere_thickness)

#define H_R atmosphere_thickness
#define H_M (H_R / (8.0 / 1.2))
#define beta_M_S_0 (float3(4e-6, 4e-6, 4e-6) * _MeiScatter)
#define beta_M_A_0 (float3(8.4e-6, 8.4e-6, 8.4e-6) * _MeiScatter)
#define beta_R_S_0 (_RayleighScatter * 33.1e-6)
#define beta_OZO_0 (float3(0.65e-6, 1.88e-6, 0.085e-6) * _OZone)


float3 UniformSampleSphere(const float2 e) {
	float Phi = 2 * pi * e.x;
	float CosTheta = 1 - 2 * e.y;
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;

	float PDF = 1 / (4 * pi);

	return H;
}
inline const float3 CosineSampleHemisphere(const float2 e, const float3 normal) {
	return normalize(UniformSampleSphere(e) + normal);
}

inline const float Altitude(const float3 x) {
	return length(x) - planet_radius;
}

inline const float3 GetPoint(const float altitude_01) {
	return float3(0, planet_radius + 10 + (atmosphere_thickness - 20) * altitude_01, 0);
}
inline const float3 GetDir01(const float cos_01) {
	float k = cos_01 * 2 - 1;
#ifdef CLAMP_COS
	k = k * 0.995;
#endif
	return float3(0, 1, 0) * k + sqrt(1 - k * k) * float3(1, 0, 0);
}
inline const float3 GetDir_11(const float cos) {
	float k = cos;
#ifdef CLAMP_COS
	k = k * 0.995;
#endif
	return float3(0, 1, 0) * k + sqrt(1 - k * k) * float3(1, 0, 0);
}
inline const float3 Beta_R_S(const float h) {
	return lerp(beta_R_S_0 * exp(-h / H_R), 0, saturate(h / atmosphere_thickness * 6 - 5));
}

inline const float P_R(const float mu) {
	//return 1.0 / 4 / pi;
	const float a = 3.0 / (16.0 * pi);
	const float b = 1 + mu * mu;

	return a * b;
}

inline const float3 Beta_M_S(const float h) {
	return lerp(beta_M_S_0 * exp(-h / H_M), 0, saturate(h / atmosphere_thickness * 6 - 5));
}

inline const float3 Beta_M_A(const float h) {
	return lerp(beta_M_A_0 * exp(-h / H_M), 0, saturate(h / atmosphere_thickness * 6 - 5));
}

inline float P_M(const float mu) {
	const float a = 3.0 / (8.0 * pi);
	const float b = 0.3758 * (1 + mu * mu);
	const float c = 2.6241 * pow(abs(1.6241 - 1.58 * mu), 1.5);

	return a * b / c;
}

inline const float3 Beta_OZO_A(const float h) {
	return lerp(beta_OZO_0 * exp(-h / H_R), 0, saturate(h / atmosphere_thickness * 6 - 5));
}


bool X_0(const float3 x, const float3 v, out float3 x_0) {
	const float3 dir = normalize(v);
	const float dis_a = -dot(x, dir);
	const float3 proj_x = x + dir * dis_a;

	const float r = dot(proj_x, proj_x);

	const float h_g = planet_radius;
	const float h_t = atmosphere_radius;

	const float dis_b_0 = sqrt(valid(h_g * h_g - r));
	const float dis_b_1 = sqrt(valid(h_t * h_t - r));

	const float res_ground = min(valid(dis_a - dis_b_0), valid(dis_a + dis_b_0));
	const float res_top = min(valid(dis_a - dis_b_1), valid(dis_a + dis_b_1));

	x_0 = x + dir * min(res_ground, res_top);
	return dot(x,v) > 0 ? 0 : res_ground < res_top;
}

bool X_Up(const float3 x, const float3 v, out float2 dis) {
	const float3 dir = normalize(v);
	const float dis_a = -dot(x, dir);
	const float3 proj_x = x + dir * dis_a;

	const float r = dot(proj_x, proj_x);

	const float h_g = planet_radius;
	const float h_t = atmosphere_radius;

	const float dis_b_0 = sqrt(valid(h_g * h_g - r));
	float k = h_t * h_t - r;
	if (v.y > 0 || k < 0) {
		dis = 0;
		return false;
	}
	const float dis_b_1 = sqrt(k);

	float res_ground = min(valid(dis_a - dis_b_0), valid(dis_a + dis_b_0));
	float2 res_top = float2(dis_a - dis_b_1, dis_a + dis_b_1);

	dis = float2(res_top.x, min(res_ground, res_top.y));
	return true;
}

#ifdef T_CAL_
const float3 T_cal(const float3 x, const float3 y, const int sampleNum = 1e4) {
	float3 res = 0;

	float dis = distance(x, y);
	
	for (int i = 0; i < sampleNum; i++)
	{
		float altitude = Altitude(lerp(x, y, (float(i/* + SAMPLE1D*/) / sampleNum)));
		res += Beta_R_S(altitude) + Beta_M_A(altitude) + Beta_OZO_A(altitude);
	}
	res *= dis / sampleNum;
	return exp(-res);
}
#endif

#define zip_order 4
float unzip(float x) {
	return x < 0.5 ? (x * pow(abs(2 * x), zip_order - 1)) : ((1 - x) * pow(2 * x - 2, zip_order - 1) + 1);
}
float zip(float x) {
	return x < 0.5 ? pow(abs(x / (1 << zip_order - 1)), 1.0f / zip_order) : (1 - pow(abs((1 - x) / (1 << zip_order - 1)), 1.0f / zip_order));
}
const float3 T_tab_fetch(const float3 x, const float3 v) {

	float horiz = length(x);
	horiz = -saturate(sqrt(horiz * horiz - planet_radius * planet_radius) / horiz);

	float cos = clamp(-1, 1, dot(v, normalize(x)));
	float2 uv;
	if (cos < horiz) {
		uv.x = 1 - saturate(Altitude(x) / atmosphere_thickness);
		uv.y = (cos + 1) / (horiz + 1) / 2 + 0.5;
	}
	else {
		uv.x = saturate(Altitude(x) / atmosphere_thickness);
		uv.y = (cos - horiz) / (1 - horiz) / 2;
	}
	uv.y = zip(uv.y);
	return tex2D(T_table, _TLutResolution, uv).xyz;
}
#ifdef T_TAB_
const float3 T_tab(const float3 x, const float3 y) {
	const float3 v = normalize(y - x);

	float3 a = T_tab_fetch(x, v);
	float3 b = T_tab_fetch(y, v);

	return min(1.0, a / b);
}
#endif

const float3 J(const float3 y, const float3 v, const float3 s, const int sampleNum = 1) {
	float3 res = 0;

	const float altitude = Altitude(y);
	const float3 beta_R = Beta_R_S(altitude);
	const float3 beta_M = Beta_M_S(altitude);

	const float approximate_int_0 = (1 - cos(sun_angle)) / 2;

	float3 approximate_int_1 = 0;
	float horiz = length(y);
	horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;
	if (dot(normalize(y), s) > horiz) {
		approximate_int_1 = T_tab_fetch(y, s);
	}

	float mu = dot(s, v);
	float3 p_R = P_R(mu);
	float3 p_M = P_M(mu);

	res = approximate_int_0 * approximate_int_1 * (p_R * beta_R + p_M * beta_M);

	return res * 4 * pi;
}

const float3 Tu_L(const float3 x_0, const float3 s) {

	const float3 albedo = GroundAlbedo(x_0);
	const float3 normal = normalize(x_0);


	const float approximate_int_0 = (1 - cos(sun_angle));

	float3 approximate_int_1 = 0;
	if (dot(normalize(x_0), s) > 0) {
		approximate_int_1 = T_tab_fetch(x_0, s);
	}

	float cos = dot(normal, s);

	float3 res = approximate_int_0 * approximate_int_1 * cos * GroundAlbedo(x_0);

	return res;
}

const float3 Scatter0(const float3 x, float3 x_0, const float3 s, const int dirSampleNum = 64) {
	x_0 = lerp(x, x_0, 0.99);
	const float dis = distance(x, x_0);

	const float3 v = normalize(x_0 - x);

	float3 res = 0;

	for (int i = 0; i < dirSampleNum; i++)
	{
		const float layered_rnd = float(i /*+ SAMPLE1D*/) / dirSampleNum;
		const float3 y = lerp(x, x_0, layered_rnd);
		const float3 trans = T(x, y);
		res += trans * J(y, v, s, 1);
	}
	res *= dis / dirSampleNum;

	return res;
}

const float3 Scatter1234_(const float3 x, const float3 s) {
	const float altitude = Altitude(x);
	const float3 beta_R = Beta_R_S(altitude);
	const float3 beta_M = Beta_M_S(altitude);

	float v = Altitude(x) / atmosphere_thickness;

	float u = (dot(s, normalize(x)) + 1) / 2;
	
	float3 scatter = tex2Dlod(MS_table, float4(u, v, 0, 0)).xyz;
	return (beta_R + beta_M) * scatter;
}

const float3 Scatter(const float3 x, float3 x_0, float3 v, const float3 s, const int dirSampleNum = 128, bool includeTu = true) {
	x_0 = lerp(x, x_0, 0.99);
	const float dis = distance(x, x_0);

	float3 res = 0;
	//return P_R(dot(s, v));
	for (int i = 0; i < dirSampleNum; i++)
	{
		const float layered_rnd = float(i + SAMPLE1D) / dirSampleNum;
		const float3 y = lerp(x, x_0, layered_rnd);
		const float3 trans = T(x, y);
		res += trans * (J(y, v, s, 1) + Scatter1234_(y, s) * _MultiScatterStrength);
	}
	res *= dis / dirSampleNum;

	float horiz = length(x);
	horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

	if (includeTu && dot(normalize(x), v) < horiz)
		res += Tu_L(x_0, s) / pi * T_tab_fetch(x_0, v);

	return res;
}

const float3 Lf(const float3 x, float3 x_0, const float3 s, const int dirSampleNum = 128) {
	x_0 = lerp(x, x_0, 0.99);
	const float dis = distance(x, x_0);
	const float3 v = normalize(x_0 - x);

	float3 res = 0;

	for (int i = 0; i < dirSampleNum; i++)
	{
		const float layered_rnd = float(i) / dirSampleNum;
		const float3 y = lerp(x, x_0, layered_rnd);
		const float3 trans = T(x, y);

		const float altitude = Altitude(y);
		const float3 beta_R = Beta_R_S(altitude);
		const float3 beta_M = Beta_M_S(altitude);

		res += trans * (beta_R + beta_M);
	}
	res *= dis / dirSampleNum;

	return res;
}

const float3 Fms(const float3 x, const float3 s, const int sampleNum = 128) {
	float3 res = 0;
	for (int i = 0; i < sampleNum; i++)
	{
		float3 dir = UniformSampleSphere(Roberts2(i));
		float3 x_0;
		bool ground = X_0(x, dir, x_0);
		res += Lf(x, x_0, s, 16);
	}
	res = res / sampleNum / (4 * pi);
	return 1 / (1 - res);
}

const float3 L2(const float3 x, const float3 s, const int sampleNum = 128) {
	float3 res = 0;
	for (int i = 0; i < sampleNum; i++)
	{
		float3 dir = UniformSampleSphere(Roberts2(i));
		float3 x_0;
		bool ground = X_0(x, dir, x_0);
		res += Scatter0(x, x_0, s, 16);
		if (ground) res += T_tab_fetch(x, dir) * Tu_L(x_0, s);
	}
	res = res / sampleNum;
	return res;
}

const float3 ScatterTable(float3 x, const float3 v, const float3 s, const bool includeTu = true) {

	float phi = acos(clamp(dot(normalize(v.xz), normalize(s.xz)), -1, 1)) / pi;

	float rho;
	float horiz = length(x);
	horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

	if (v.y < horiz) {
		rho = pow((v.y + 1) / (horiz + 1), 2) * 0.5;
	}
	else {
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
	float3 scatter = rho >= 0 ? tex2Dlod(S_table, float4(phi, rho, 0, 0)).xyz : 0;

	float coef = 1 - 4.0f / _SLutResolution.y;
	if (rho > coef) {
		float3 x_0;
		X_0(x, v, x_0);
		scatter = lerp(scatter, Scatter(x, x_0, v, s, 32, includeTu) * _Multiplier, (rho - coef) / (1 - coef));
	}
	else if (rho < 1 - coef) {
		float3 x_0;
		if (x.y > atmosphere_radius - 1) {
			float2 dis; 
			X_Up(x, v, dis);
			x_0 = x + dis.y * v;
			x = x + dis.x * v;
			scatter = Scatter(x, x_0, v, s, 128, includeTu) * _Multiplier;
		}
		// usualy we don't need this
		//else {
		//	X_0(x, v, x_0);
		//	scatter = Scatter(x, x_0, v, s, 128, includeTu) *_Multiplier;
		//}
	}
	// prevent error
	scatter = max(0, scatter);

	float3 sun = 1;
	if (x.y > atmosphere_radius - 1) {
		float2 dis;
		if (X_Up(x, v, dis)) {
			float3 x_0 = x + dis.y * v;
			x = x + dis.x * v;
			sun = T_tab_fetch(x, v);
		}
	}
	else {
		sun = T_tab_fetch(x, v);
	}
	sun *= smoothstep(cos(sun_angle + 0.005), cos(sun_angle), dot(v, s)) * (v.y > horiz);
	sun /= 0.2e4;

	return scatter + sun;
}


const float3 SkyBox(float3 x, const float3 v, const float3 s) {

	float phi = acos(clamp(dot(normalize(v.xz), normalize(s.xz)), -1, 1)) / pi;

	float rho;
	float horiz = length(x);
	horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

	if (v.y < horiz) {
		rho = pow((v.y + 1) / (horiz + 1), 2) * 0.5;
	}
	else {
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
	float3 scatter = rho >= 0 ? tex2Dlod(S_table, float4(phi, rho, 0, 0)).xyz : 0;

	float coef = 1 - 4.0f / _SLutResolution.y;
	if (rho > coef) {
		float3 x_0;
		X_0(x, v, x_0);
		scatter = lerp(scatter, Scatter(x, x_0, v, s, 32, false) * _Multiplier, (rho - coef) / (1 - coef));
	}
	else if (rho < 1 - coef) {
		float3 x_0;
		if (x.y > atmosphere_radius - 1) {
			float2 dis;
			X_Up(x, v, dis);
			x_0 = x + dis.y * v;
			x = x + dis.x * v;
			scatter = Scatter(x, x_0, v, s, 128, true) * _Multiplier;
		}
		//else {
		//	X_0(x, v, x_0);
		//	scatter = Scatter(x, x_0, v, s, 128, true) * _Multiplier;
		//}
	}
	// prevent error
	scatter = max(0, scatter);

	return scatter;
}

const float3 Scatter(const float3 x, const float3 v, const float depth, const float3 s) {

	float phi = pow(acos(dot(normalize(v.xz), normalize(s.xz))) / pi, 0.33333);

	float rho;
	float horiz = length(x);
	horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

	if (v.y < horiz) {
		rho = pow((v.y + 1) / (horiz + 1), 2) * 0.5;
	}
	else {
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
	float3 scatter = rho >= 0 ? tex3Dlod(Volume_table, float4(phi, rho, depth / _MaxDepth, 0)).xyz : 0;
	return scatter;
}

const float3 Sunlight(const float3 x, const float3 s) {

	float lx = length(x);
	float horiz = -saturate(sqrt(lx * lx - planet_radius * planet_radius) / lx);

	return _SunColor * T_tab_fetch(x, s) * (1 - cos(sun_angle)) * 39810 * smoothstep(horiz, horiz + 0.015, dot(x, s) / lx);
}

#endif