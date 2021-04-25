#ifndef SAMPLER_H_
#define SAMPLER_H_


#ifndef PI
#define PI 3.14159265359
#endif


Texture2D<int> _Sobol; //256x256
Texture3D<int> _ScramblingTile; //128*128*8
Texture3D<int> _RankingTile; //128*128*8


inline int sobol_256spp_256d(uint index) {
	return _Sobol[uint2(index & 0xFF, index >> 8)];
}

inline int scramblingTile(uint index) {
	return _ScramblingTile[uint3(index & 127, (index >> 7) & 127, index >> 14)];
}

inline int rankingTile(uint index) {
	return _RankingTile[uint3(index & 127, (index >> 7) & 127, index >> 14)];
}

inline float samplerBlueNoiseErrorDistribution_128x128_OptimizedFor_2d2d2d2d_8spp(uint pixel_i, uint pixel_j, uint sampleIndex, uint sampleDimension)
{
	// wrap arguments
	pixel_i = pixel_i & 127;
	pixel_j = pixel_j & 127;
	sampleIndex = sampleIndex & 255;
	sampleDimension = sampleDimension & 255;

	// xor index based on optimized ranking
	uint rankedSampleIndex = sampleIndex ^ rankingTile(sampleDimension + (pixel_i + pixel_j * 128) * 8);

	// fetch value in sequence
	uint value = sobol_256spp_256d(sampleDimension + rankedSampleIndex * 256);

	// If the dimension is optimized, xor sequence value based on optimized scrambling
	value = value ^ scramblingTile((sampleDimension % 8) + (pixel_i + pixel_j * 128) * 8);

	// convert to float and return
	float v = (0.5f + value) / 256.0f;
	return v;
}

float BNDSample(uint4 param)
{
	return samplerBlueNoiseErrorDistribution_128x128_OptimizedFor_2d2d2d2d_8spp(param.x, param.y, param.z, param.w);
}

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

#ifndef SAMPLE
#define SAMPLE (BNDSample(uint4(sampleState.xyz, sampleState.w++)))
#endif

float4 UniformSampleSphere(float2 E) {
	float Phi = 2 * PI * E.x;
	float CosTheta = 1 - 2 * E.y;
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;

	float PDF = 1 / (4 * PI);

	return float4(H, PDF);
}

float4 CosineSampleHemisphere(float2 E) {
	float Phi = 2 * PI * E.x;
	float CosTheta = sqrt(E.y);
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;

	float PDF = CosTheta / PI;
	return float4(H, PDF);
}

float3 CosineSampleHemisphere(float2 E, float3 N) {
	return normalize(UniformSampleSphere(E).xyz + N);
}

float3x3 GetMatrixFromNormal(float3 v1) {
	float3 v2, v3;
	v1 = normalize(v1);

	if (abs(v1.z) > sqrt(1 / 2)) {
		v2 = normalize(cross(v1, float3(0, 1, 0)));
	}
	else {
		v2 = normalize(cross(v1, float3(0, 0, 1)));
	}
	v3 = cross(v2, v1);

	//v2 = normalize(cross(v1, float3(1, 0, 0)));
	//v3 = normalize(cross(v2, v1));

	return float3x3(v2, v3, v1);
}

float3x3 GetMatrixFromNormal(float3 v1, float2 E) {
	float3 v2, v3;
	v1 = normalize(v1);
	v2 = normalize(cross(v1, UniformSampleSphere(E).xyz));
	v3 = cross(v2, v1);

	return float3x3(v2, v3, v1);
}

float3 ImportanceSampleGGX(float2 E, float Roughness) {
	float m = Roughness * Roughness;
	float m2 = m * m;

	float Phi = 2 * PI * E.x;
	float CosTheta = sqrt((1 - E.y) / (1 + (m2 - 1) * E.y));
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;
	return H;
}

float3 ImportanceSampleAnisoGGX(float2 E, float2 Roughness) {
	float2 m = Roughness * Roughness;

	float Phi = 2 * PI * E.x;
	float2 cs = float2(cos(Phi), sin(Phi));
	float2 xy = sqrt(E.y) * cs * m;
	float z = sqrt(1 - E.y);

	return normalize(float3(xy, z));
}

float3 ImportanceSampleAnisoVGGX(float2 E, float3 v, float2 Roughness) {
	float2 m = Roughness * Roughness;

	float3 Ve = normalize(v * float3(m, 1));

	float Phi = 2 * PI * E.x;
	float2 cs = float2(cos(Phi), sin(Phi));
	float2 xy = sqrt(E.y) * cs;

	float3x3 mat = GetMatrixFromNormal(Ve);

	float s = 0.5 * (1 + Ve.z);
	xy.y = (1 - s) * sqrt(1 - xy.x * xy.x) + s * xy.y;

	float3 N = mul(float3(xy, sqrt(max(0, 1 - xy.x * xy.x - xy.y * xy.y))), mat);
	return normalize(float3(N.xy * m, N.z));
}

float2 UniformSampleDisk(float2 Random) {
	const float Theta = 2.0f * (float)PI * Random.x;
	const float Radius = sqrt(Random.y);
	return float2(Radius * cos(Theta), Radius * sin(Theta));
}

float2 UniformSampleRegularPolygon(int sides, float2 Random) {
	float inv_sides = 1.0f / sides;
	float theta = 2 * PI * inv_sides;
	int triangle_index = floor(Random.x * sides);
	float2 a; sincos(theta * triangle_index + theta / 2, a.y, a.x);
	float2 b; sincos(theta * (triangle_index + 1) + theta / 2, b.y, b.x);
	Random.x = (Random.x - (float)triangle_index * inv_sides) * sides;
	Random.y = sqrt(Random.y);
	return (1 - Random.y) * a + Random.x * Random.y * b;
}

float3 dFromALd(float Ld, float3 A) {
	float3 k = (A - 0.33);
	k *= k;
	k *= k;
	return float3(Ld, Ld, Ld) / (3.5 + 100 * k);
}
 
Texture2D _RdLut; SamplerState sampler_RdLut;
float3 ImportanceSampleRd(float2 Random) {
	const float Theta = 2.0f * (float)PI * Random.x;
	const float Radius = _RdLut.SampleLevel(sampler_RdLut, float2(Random.y, 0.5), 0).x;
	return float3(Radius * cos(Theta), Radius * sin(Theta), Radius);
}

float3 PdfRd(float3 r, float d) {
	return (exp(- r/ d) + exp(- r / 3 / d)) / (8 * PI * d * r);
}

#endif