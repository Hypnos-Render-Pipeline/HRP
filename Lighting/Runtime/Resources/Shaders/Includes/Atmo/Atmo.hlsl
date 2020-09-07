#ifndef ATMO_INCLUDE_
#define ATMO_INCLUDE_

#include "./AtmoTextureSampler.hlsl"

#define T_CAL     0
#define T_TAB     1
#define L_0       0
#define L_        1
#define J_L_0     0
#define J_L_LOOP  1
#define J_L_TAB   2
#define Tu_L_0    0
#define Tu_L_     1
#define S_L_0     0
#define S_L_TAB   1
#define S_L_LOOP 2
#define S_L_SHADE 3

#ifndef T
#define T T_CAL
#endif
#ifndef L
#define L L_0
#endif
#ifndef J_L
#define J_L J_L_0
#endif
#ifndef Tu_L
#define Tu_L Tu_L_0
#endif
#ifndef S_L
#define S_L S_L_0
#endif

#if T == 0
#undef T
#undef T_CAL
#define T T_cal
#define T_CAL_
#elif T == 1
#undef T
#undef T_TAB
#define T T_tab
#define T_TAB_
#endif

#if L == 0
#undef L_0
#define L L_0
#define L_0_
#elif L == 1
#undef L_
#define L L_
#define L__
#endif

#if J_L == 0
#undef J_L_0
#define J_L J_L_0
#define J_L_0_
#elif J_L == 1
#undef J_L_
#undef J_L
#define J_L J_L_
#define J_L__
#elif J_L == 2
#undef J_L_TAB
#undef J_L
#define J_L J_L_tab
#define J_L_TAB_
#endif
 
#if Tu_L == 0
#undef Tu_L_0
#define Tu_L Tu_L_0
#define Tu_L_0_
#elif Tu_L == 1
#undef Tu_L_
#define Tu_L Tu_L_
#define Tu_L__
#endif

#if S_L == 0
#undef S_L
#undef S_L_0
#define S_L S_L_0
#define S_L_0_
#elif S_L == 1
#undef S_L
#undef S_L_
#define S_L S_L_
#define S_L__
#elif S_L == 2
#undef S_L
#undef S_L_LOOP
#define S_L S_L_loop
#define S_L_LOOP_
#elif S_L == 3
#undef S_L
#undef S_L_SHADE
#define S_L S_L_shade
#define S_L_SHADE_
#endif


#define T_resolution int2(64, 512)
#define E_resolution int2(16, 64)
#define S_resolution int4(32, 64, 16, 8)


#define planet_radius 6371e3
#define atmosphere_thickness 8e3
#define sun_angle (0.5 / 180.0 * pi)

uint sampleIndex1D = 0;
uint sampleIndex2D = 0;

float3 GroundAlbedo(const float3 x_0) {
	return 25e-2; // land
	return 95e-2; // snow
	return 8e-2;  // ocean
}


sampler2D T_table;
sampler3D S_table;
sampler3D J_table;


#define pi (3.14159265359)
#define float_max (1e100)

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
	return x < 0 ? float_max : x;
}
float remap(float v, float a, float b, float c, float d) {
	return (clamp(v, a, b) - a) / max(0.0001, b - a) * (d - c) + c;
}

#define atmosphere_radius (planet_radius + atmosphere_thickness)

#define H_R atmosphere_thickness
#define H_M (H_R / (8.0 / 1.2))
#define beta_M_S_0 float3(21e-6, 21e-6, 21e-6)
#define beta_R_S_0 float3(5.8e-6, 13.5e-6, 33.1e-6)
#define beta_OZO_0 float3(3.426e-7, 8.298e-7, 0.356e-7)*6


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
	const float a = 3.0 / (16.0 * pi);
	const float b = 1 + mu * mu;

	return a * b;
}

inline const float3 Beta_M_S(const float h) {
	return lerp(beta_M_S_0 * exp(-h / H_M), 0, saturate(h / atmosphere_thickness * 6 - 5));
}

inline float P_M(const float mu) {
	const float a = 3.0 / (8.0 * pi);
	const float b = 0.3758 * (1 + mu * mu);
	const float c = 2.6241 * pow(1.6241 - 1.58 * mu, 1.5);

	return a * b / c;
}

inline const float3 Beta_OZO_S(const float h) {
	return lerp(beta_OZO_0 * exp(-h / H_R), 0, saturate(h / atmosphere_thickness * 6 - 5));
}


bool X_0(const float3 x, const float3 v, out float3 x_0) {
	const float3 dir = normalize(v);
	const float dis_a = -dot(x, dir);
	const float3 proj_x = x + dir * dis_a;

	const float r = length(proj_x);

	const float h_g = planet_radius;
	const float h_t = atmosphere_radius;

	const float dis_b_0 = sqrt(valid(h_g * h_g - r * r));
	const float dis_b_1 = sqrt(valid(h_t * h_t - r * r));

	const float res_ground = min(valid(dis_a - dis_b_0), valid(dis_a + dis_b_0));
	const float res_top = min(valid(dis_a - dis_b_1), valid(dis_a + dis_b_1));

	x_0 = x + dir * min(res_ground, res_top);
	return dot(x,v) > 0 ? 0 : res_ground < res_top;
}

#ifdef T_CAL_
const float3 T_cal(const float3 x, const float3 y, const int sampleNum = 1e4) {
	float3 res = 0;

	float dis = distance(x, y);
	
	for (int i = 0; i < sampleNum; i++)
	{
		float altitude = Altitude(lerp(x, y, SAMPLE1D));
		res += Beta_R_S(altitude) + 1.11 * Beta_M_S(altitude) + Beta_OZO_S(altitude);
	}
	res *= dis / sampleNum;
	return exp(-res);
}
#endif

#define zip_order 4
float unzip(float x) {
	return x < 0.5 ? (x * pow(2 * x, zip_order - 1)) : ((1 - x) * pow(2 * x - 2, zip_order - 1) + 1);
}
float zip(float x) {
	return x < 0.5 ? pow(x / (1 << zip_order - 1), 1.0f / zip_order) : (1 - pow((1 - x) / (1 << zip_order - 1), 1.0f / zip_order));
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
	return tex2D(T_table, T_resolution, uv);
}
#ifdef T_TAB_
const float3 T_tab(const float3 x, const float3 y) {
	const float3 v = normalize(y - x);

	float3 a = T_tab_fetch(x, v);
	float3 b = T_tab_fetch(lerp(x, y, 0.97), v);

	return min(1.0, a / b);
}
#endif
//#define PROJ_PARM
#ifdef PROJ_PARM
float4 xvs_2_u(const float3 x, const float3 v, const float3 s) {
	float r = length(x);
	float mu = dot(normalize(x), normalize(v));
	mu = mu < 0 ? min(mu, -0.0005603) : mu;
	float mu_s = dot(normalize(x), normalize(s));
	float vo = dot(normalize(v), normalize(s));

	float phi = sqrt(r * r - planet_radius * planet_radius);
	float H = sqrt(atmosphere_radius * atmosphere_radius - planet_radius * planet_radius);
	float delta = r * r * mu * mu - phi * phi;

	float u_r = phi / H;
	float u_mu = (mu < 0 &&  delta > 0) ?
		(0.5 + (r*mu + sqrt(delta)) / (2 * phi)) :
		(0.5 - (r*mu - sqrt(delta + H * H)) / (2 * phi + 2 * H));
	float u_mu_s = max(0.0, (1 - exp(-3 * mu_s - 0.6)) / (1 - exp(-3.6)));
	float u_vo = (1 + vo) / 2;

	return float4(u_r, u_mu, u_mu_s, u_vo);
}

int u_2_xvs(const float4 u, out float3 x, out float3 v, out float3 s) {
	const float h_g = planet_radius + 1;
	const float h_t = atmosphere_radius - 1;

	float u_r = u.x;
	float u_mu = u.y;
	float u_mu_s = u.z;
	float u_vo = u.w;

	float H = sqrt(h_t * h_t - h_g * h_g);
	float phi = u_r * H;

	float r = sqrt(phi * phi + h_g * h_g);
	float mu;
	{
		if (u_mu >= 0.5) {
			float t = (u_mu - 0.5) * (2 * phi);
			// (t - r * mu) = sqrt(delta)
			// (t - r * mu) * (t - r * mu) = r * r * mu * mu - phi * phi
			// t * t - 2 * t * r * mu + r * r * mu * mu = r * r * mu * mu - phi * phi
			mu = min(1.0, (phi * phi + t * t) / (2 * t * r));
		}
		else {
			float t = -(0.5 - u_mu) * (2 * phi + 2 * H);
			// (r * mu + t) * (r * mu + t) - H * H = delta
			// r*r*mu*mu + 2 * r * t * mu + t *t - H * H = r * r * mu * mu - phi * phi
			// 2 * r * t * mu + t *t - H * H =  - phi * phi
			mu = min(0.0, max(-1.0, (H * H - phi * phi - t * t) / (2 * r * t)));
		}
	}
	float mu_s = (log(u_mu_s * (exp(-3.6) - 1) + 1) + 0.6) / -3;
	float vo = u_vo * 2 - 1;

	x = float3(0, r, 0);
	v = GetDir_11(mu);


	float a = mu_s;
	float b = v.x;
	float c = v.y;
	float d = vo;
	float b_ = (abs(b) < 0.00001 ? b + (sign(b) == 0 ? 1 : sign(b)) * 0.00001f : b);
	float unx = (d - a * c) / b_;

	s = 0;
	float t = sqrt(1 - a * a);
	if (unx > t) return -1;
	else if (unx < -t) return 1;
	s = float3(unx, a, sqrt(1 - a * a - unx * unx));
	return 0;
}
#else
#define MUL_ 4
float4 xvs_2_u(const float3 x, const float3 v, const float3 s) {
	float r = length(x);
	float mu = dot(normalize(x), normalize(v));// / 2 + 0.5;
	float mu_s = dot(normalize(x), normalize(s))/* / 2 + 0.5*/;
	float vo = dot(normalize(v), normalize(s));// / 2 + 0.5;

	float horiz = length(x);
	horiz = -saturate(sqrt(horiz * horiz - planet_radius * planet_radius) / horiz);

	mu = mu < horiz ? remap(mu, -1, horiz, -1, -0.5)  : mu < mu_s ? remap(mu, horiz, mu_s, -0.5, 0.25) : remap(mu, mu_s, 1, 0.25, 1);

	float H_g = planet_radius + 10;
	float H_t = atmosphere_radius - 10;
	float H = sqrt(H_t * H_t - H_g * H_g);
	float u_r = sqrt(max(0, r * r - H_g * H_g)) / H;
	float u_mu_s = saturate(max(0.0, (1 - exp(-3 * mu_s - 0.6)) / (1 - exp(-3.6))) * 1.4 - 0.4);

	vo = 1 - acos(vo) / pi;
	vo = pow(vo, MUL_);

	mu = mu / 2 + 0.5;
	//mu = 1 - acos(mu) / pi;
	//mu = pow(mu, MUL_);

	return float4(u_r, mu, vo, u_mu_s);
}

int u_2_xvs(const float4 u, out float3 x, out float3 v, out float3 s) {
	float H_g = planet_radius + 10;
	float H_t = atmosphere_radius - 10;
	float H = sqrt(H_t * H_t - H_g * H_g);
	float r = u.x  * H;
	r *= r;
	r = sqrt(r + H_g * H_g);

	x = float3(0, r, 0);

	float horiz = length(x);
	horiz = -saturate(sqrt(horiz * horiz - planet_radius * planet_radius) / horiz);

	float u_w = saturate((u.w + 0.4) / 1.4);
	float mu_s = (log(u_w * (exp(-3.6) - 1) + 1) + 0.6) / -3;


	float mu = u.y*2-1;//cos((1 - pow(u.y, 1.0/MUL_)) * pi);

	mu = mu < -0.5 ? remap(mu, -1, -0.5, -1, horiz) : mu < 0.25 ? remap(mu, -0.5, 0.25, horiz, mu_s) : remap(mu, 0.25, 1, mu_s, 1);

	v = GetDir01(mu / 2 + 0.5);
	//v = GetDir01(cos((1 - pow(u.y, 1.0/MUL_)) * pi) / 2 + 0.5);

	float vo = cos((1 - pow(u.z, 1.0/MUL_)) * pi);
	//float vo = u.z;

	if (u_w == 1) { // handle delta function.
		s = float3(0,1,0);
		return 0;
	}

	float a = mu_s;
	float b = v.x;
	float c = v.y;
	float d = vo * 2 - 1;
	float b_ = (abs(b) < 0.00001 ? b + (sign(b) == 0 ? 1 : sign(b)) * 0.00001f : b);
	float unx = (d - a * c) / b_;

	s = 0;
	float t = sqrt(1 - a * a);
	if (unx > t) return -1;
	else if (unx < -t) return 1;
	s = float3(unx, a, sqrt(1 - a * a - unx * unx));
	return 0;
}
#endif

#ifdef L_0_
const float3 L_0(const float3 x, const float3 v, const float3 s) {
	float3 x_0;
	bool isGround = X_0(x, v, x_0);
	if (isGround) return 0;

	// > sun angle (0.5 degree)
	if (dot(v, s) > cos(sun_angle)) {
		return T(x * 1.000001, x_0);
	}
	else {
		return 0;
	}
}
#endif

#ifdef L__
const float3 L_(const float3 x, const float3 v, const float3 s) {
	return 1;
}
#endif

#ifdef Tu_L_0_
const float3 Tu_L_0(const float3 x_0, const float3 s, const int sampleNum = 1) {

	const float3 albedo = GroundAlbedo(x_0);
	const float3 normal = normalize(x_0);


	const float approximate_int_0 = (1 - cos(sun_angle));

	const float3 approximate_int_1 = 1;// L(x_0, s, s);

	float cos = dot(normal, s);

	float3 res = approximate_int_0 * approximate_int_1 * cos * GroundAlbedo(x_0);

	return res * 2 * pi;
}
#endif

#ifdef Tu_L__
const float3 Tu_L_(const float3 x_0, const float3 s, const int sampleNum = 1e4) {

	const float3 albedo = GroundAlbedo(x_0);
	const float3 normal = normalize(x_0);

	float3 res = 0;

	for (int i = 0; i < sampleNum; i++)
	{
		float3 dir = CosineSampleHemisphere(SAMPLE2D, normal);
		res += L(x_0, dir, s);
	}
	return res / sampleNum;
}
#endif

#ifdef J_L_0_
const float3 J_L_0(const float3 y, const float3 v, const float3 s, const int sampleNum = 1) {
	float3 res = 0;
	
	const float altitude = Altitude(y);
	const float3 beta_R = Beta_R_S(altitude);
	const float3 beta_M = Beta_M_S(altitude);

	const float approximate_int_0 = (1 - cos(sun_angle)) / 2;

	const float3 approximate_int_1 = L(y, s, s);

	float mu = dot(s, v);
	float3 p_R = P_R(mu);
	float3 p_M = P_M(mu);

	res = approximate_int_0 * approximate_int_1 * (p_R * beta_R + p_M * beta_M);

	return res * 4 * pi;
}
#endif

#ifdef J_L_TAB_
const float3 J_L_tab(const float3 y, const float3 v, const float3 s, const int sampleNum = 1) {
	float4 uv = xvs_2_u(y, v, s);
	return tex4D(J_table, S_resolution, uv);
}
#endif

#ifdef S_L_0_
const float3 S_L_0(const float3 x, float3 v, const float3 s, const int dirSampleNum = 1e3, const int sampleNum = 1e3) {

	float3 x_0;
	bool isGround = X_0(x, v, x_0);

	const float dis = distance(x, x_0);

	float3 res = 0;

	for (int i = 0; i < dirSampleNum; i++)
	{
		const float3 y = lerp(x, x_0, SAMPLE1D);
		const float3 trans = T(x, y);
		res += trans * J_L(y, v, s, sampleNum);
	}
	res *= dis / dirSampleNum;

	if (isGround)
		res += T_tab_fetch(x, v) * Tu_L(x_0, s);

	return res;
}
#endif

#ifdef S_L__
const float3 S_L_(const float3 x, float3 v, const float3 s, const int dirSampleNum = 1, const int sampleNum = 1) {
	float4 uv = xvs_2_u(x, v, s);
	return tex4D(S_table, S_resolution, uv);
}
#endif

#ifdef J_L__
const float3 J_L_(const float3 y, const float3 v, const float3 s, const int sampleNum = 1e4) {
	const float altitude = Altitude(y);
	const float3 beta_R = Beta_R_S(altitude);
	const float3 beta_M = Beta_M_S(altitude);

	float3 direct;
	{
		const float approximate_int_0 = (1 - cos(sun_angle)) / 2;
		const float3 approximate_int_1 = L(y, s, s);
		float mu = dot(s, v);
		float3 p_R = P_R(mu);
		float3 p_M = P_M(mu);
		direct = approximate_int_0 * approximate_int_1 * (p_R * beta_R + p_M * beta_M)* 4 * pi;
	}

	float3 res = 0;
	
	for (int i = 0; i < sampleNum; i++)
	{
		float3 dir = UniformSampleSphere(SAMPLE2D);
		float mu = dot(dir, v);
		float3 p_R = P_R(mu);
		float3 p_M = P_M(mu);

		res += (p_R * beta_R + p_M * beta_M) * S_L(y, dir, s);
	}
	return res / sampleNum * 4 * pi;// + direct;
}
#endif

#ifdef S_L_LOOP_
const float3 S_L_loop(const float3 x, float3 v, const float3 s, const float dirSampleNum = 1, const int sampleNum = 1) {
	float3 x_0;
	bool isGround = X_0(x, v, x_0);

	const float dis = distance(x, x_0);

	float3 res = 0;

	for (int i = 0; i < dirSampleNum; i++)
	{
		const float3 y = lerp(x, x_0, SAMPLE1D);
		const float3 trans = T(x, y);
		res += trans * J_L(y, v, s, sampleNum);
	}
	res *= dis / dirSampleNum;

	return res;
}
#endif

#ifdef S_L_SHADE_
const float3 S_L_shade(const float3 x, const float3 x_0, const float3 s, const int dirSampleNum = 10) {

	const float3 v = normalize(x_0 - x);
	const float k = min(dot(normalize(x), v), 0);
	float fade = 1 - saturate((length(x) * sqrt(1 - k * k) - planet_radius) / atmosphere_thickness);
	fade = pow(fade, 2);


	const float dis = distance(x, x_0);

	float3 res = 0;

	for (int i = 0; i < dirSampleNum; i++)
	{
		const float layered_rnd = (i + SAMPLE1D) / dirSampleNum;
		const float3 y = lerp(x, x_0, layered_rnd);
		const float3 trans = T(x, y);
		float4 y_uv = xvs_2_u(y, v, s);
		res += trans * (J_L(y, v, s, 1) + max(0, tex4D(J_table, S_resolution, y_uv)));
	}
	res *= dis / dirSampleNum;

	return res * fade;
}


const float3 S_L_Night(const float3 x, const float3 x_0, const float3 s, const int dirSampleNum = 10) {
	const float dis = distance(x, x_0);

	const float3 v = normalize(x_0 - x);

	float3 res = 0;

	for (int i = 0; i < dirSampleNum; i++)
	{
		const float layered_rnd = (i + SAMPLE1D) / dirSampleNum;
		const float3 y = lerp(x, x_0, layered_rnd);
		const float3 trans = T(x, y);
		float4 y_uv = xvs_2_u(y, v, s);
		res += trans * max(0, tex4D(J_table, S_resolution, y_uv));
	}
	res *= dis / dirSampleNum;

	return res * 0.06;
}
#endif

#endif