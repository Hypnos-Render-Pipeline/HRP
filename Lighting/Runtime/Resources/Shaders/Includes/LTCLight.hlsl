#ifndef LTC_H_
#define LTC_H_

#include "./PBS.hlsl"

float3 _LightPos;
float4 _LightX, _LightY;


float4 _LightColor;
sampler2D _LightDiffuseTex, _LightSpecTex;
sampler2D _TransformInv_Diffuse;
sampler2D _TransformInv_Specular;
sampler2D _AmpDiffAmpSpecFresnel;
sampler2D _DiscClip;

half IntegrateEdge(half3 v1, half3 v2)
{
	half d = dot(v1, v2);
	half theta = acos(max(-0.9999, dot(v1, v2)));
	half theta_sintheta = theta / sin(theta);
	return theta_sintheta * (v1.x * v2.y - v1.y * v2.x);
}


float3 IntegrateEdgeVec(float3 v1, float3 v2)
{
	float x = dot(v1, v2);
	float y = abs(x);

	float a = 0.8543985 + (0.4965155 + 0.0145206 * y) * y;
	float b = 3.4175940 + (4.1616724 + y) * y;
	float v = a / b;

	float theta_sintheta = (x > 0.0) ? v : 0.5 * rsqrt(max(1.0 - x * x, 1e-7)) - v;

	return cross(v1, v2) * theta_sintheta;
}

bool RayPlaneIntersect(float3 dir, float3 origin, float4 plane, out float t)
{
	t = -dot(plane, float4(origin, 1.0)) / dot(plane.xyz, dir);
	return t > 0.0;
}

float3 FetchDiffuseFilteredTexture(float3 p1, float3 p2, float3 p3, float3 p4, float3 dir, bool spec)
{
	// area light plane basis
	float3 V1 = p2 - p1;
	float3 V2 = p4 - p1;
	float3 planeOrtho = cross(V1, V2);
	float planeAreaSquared = dot(planeOrtho, planeOrtho);

	float4 plane = float4(planeOrtho, -dot(planeOrtho, p1));
	float planeDist;
	RayPlaneIntersect(dir, 0, plane, planeDist);

	float3 P = planeDist * dir - p1;

	// find tex coords of P
	float dot_V1_V2 = dot(V1, V2);
	float inv_dot_V1_V1 = 1.0 / dot(V1, V1);
	float3 V2_ = V2 - V1 * dot_V1_V2 * inv_dot_V1_V1;
	float2 Puv;
	Puv.y = dot(V2_, P) / dot(V2_, V2_);
	Puv.x = dot(V1, P) * inv_dot_V1_V1 - dot_V1_V2 * inv_dot_V1_V1 * Puv.y;

	// LOD
	float d = abs(planeDist) / pow(planeAreaSquared, 0.25);
	
	float lod = log(2048.0 * d) / log(3.0);
	lod = min(lod, 7.0);

	float3 a;
	float3 b;

	if (spec) {
		return tex2Dlod(_LightSpecTex, float4(Puv, 0, lod));
	}
	else {
		Puv = Puv / 2 + 0.25;
		return tex2Dlod(_LightDiffuseTex, float4(Puv, 0, lod));
	}
}

// Baum's equation
// Expects non-normalized vertex positions
float4 PolygonRadiance(half4x3 L, bool spec)
{
	float3 LL[4];
	LL[0] = L[0];
	LL[1] = L[1];
	LL[2] = L[2];
	LL[3] = L[3];

	uint config = 0;
	if (L[0].z > 0) config += 1;
	if (L[1].z > 0) config += 2;
	if (L[2].z > 0) config += 4;
	if (L[3].z > 0) config += 8;


	// The fifth vertex for cases when clipping cuts off one corner.
	// Due to a compiler bug, copying L into a vector array with 5 rows
	// messes something up, so we need to stick with the matrix + the L4 vertex.
	half3 L4 = L[3];

	// This switch is surprisingly fast. Tried replacing it with a lookup array of vertices.
	// Even though that replaced the switch with just some indexing and no branches, it became
	// way, way slower - mem fetch stalls?

	// clip
	uint n = 0;
	switch (config)
	{
	case 0: // clip all
		break;

	case 1: // V1 clip V2 V3 V4
		n = 3;
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		L[2] = -L[3].z * L[0] + L[0].z * L[3];
		break;

	case 2: // V2 clip V1 V3 V4
		n = 3;
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
		break;

	case 3: // V1 V2 clip V3 V4
		n = 4;
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
		L[3] = -L[3].z * L[0] + L[0].z * L[3];
		break;

	case 4: // V3 clip V1 V2 V4
		n = 3;
		L[0] = -L[3].z * L[2] + L[2].z * L[3];
		L[1] = -L[1].z * L[2] + L[2].z * L[1];
		break;

	case 5: // V1 V3 clip V2 V4: impossible
		break;

	case 6: // V2 V3 clip V1 V4
		n = 4;
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		L[3] = -L[3].z * L[2] + L[2].z * L[3];
		break;

	case 7: // V1 V2 V3 clip V4
		n = 5;
		L4 = -L[3].z * L[0] + L[0].z * L[3];
		L[3] = -L[3].z * L[2] + L[2].z * L[3];
		break;

	case 8: // V4 clip V1 V2 V3
		n = 3;
		L[0] = -L[0].z * L[3] + L[3].z * L[0];
		L[1] = -L[2].z * L[3] + L[3].z * L[2];
		L[2] = L[3];
		break;

	case 9: // V1 V4 clip V2 V3
		n = 4;
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		L[2] = -L[2].z * L[3] + L[3].z * L[2];
		break;

	case 10: // V2 V4 clip V1 V3: impossible
		break;

	case 11: // V1 V2 V4 clip V3
		n = 5;
		L[3] = -L[2].z * L[3] + L[3].z * L[2];
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
		break;

	case 12: // V3 V4 clip V1 V2
		n = 4;
		L[1] = -L[1].z * L[2] + L[2].z * L[1];
		L[0] = -L[0].z * L[3] + L[3].z * L[0];
		break;

	case 13: // V1 V3 V4 clip V2
		n = 5;
		L[3] = L[2];
		L[2] = -L[1].z * L[2] + L[2].z * L[1];
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		break;

	case 14: // V2 V3 V4 clip V1
		n = 5;
		L4 = -L[0].z * L[3] + L[3].z * L[0];
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		break;

	case 15: // V1 V2 V3 V4
		n = 4;
		break;
	}

	if (n == 0)
		return 0;

	// normalize
	L[0] = normalize(L[0]);
	L[1] = normalize(L[1]);
	L[2] = normalize(L[2]);
	if (n == 3)
		L[3] = L[0];
	else
	{
		L[3] = normalize(L[3]);
		if (n == 4)
			L4 = L[0];
		else
			L4 = normalize(L4);
	}

	// integrate
	float3 sum = 0;
	sum += IntegrateEdgeVec(L[0], L[1]);
	sum += IntegrateEdgeVec(L[1], L[2]);
	sum += IntegrateEdgeVec(L[2], L[3]);
	if (n >= 4)
		sum += IntegrateEdgeVec(L[3], L4);
	if (n == 5)
		sum += IntegrateEdgeVec(L4, L[0]);

	float3 fetchDir = normalize(sum);

	return float4(max(0, sum.z * 0.15915) * FetchDiffuseFilteredTexture(LL[0], LL[1], LL[2], LL[3], fetchDir, spec), fetchDir.z);
}

half4 TransformedPolygonRadiance(half4x3 L, half2 uv, sampler2D transformInv, half amplitude, bool spec = false)
{
	// Get the inverse LTC matrix M
	half3x3 Minv = 0;
	Minv._m22 = 1;
	Minv._m00_m02_m11_m20 = tex2D(transformInv, uv);

	half4x3 LTransformed = mul(L, Minv);

	//half3 lb = L._m00_m01_m02;
	//half3 rb = L._m10_m11_m12 - lb;
	//half3 lu = L._m30_m31_m32 - lb;

	//half3 dir = -normalize(cross(rb, lu));
	//half3 sample_point = dir * dot(lb, dir) * dir;

	//half2 sample_uv = half2(dot((sample_point - lb), rb) / dot(rb, rb), dot((sample_point - lb), lu) / dot(lu, lu));

	return PolygonRadiance(LTransformed, spec) * float4(amplitude.xxx, 1);
}

float3 SolveCubic(float4 Coefficient)
{
	// Normalize the polynomial
	Coefficient.xyz /= Coefficient.w;
	// Divide middle coefficients by three
	Coefficient.yz /= 3.0;

	float A = Coefficient.w;
	float B = Coefficient.z;
	float C = Coefficient.y;
	float D = Coefficient.x;

	// Compute the Hessian and the discriminant
	float3 Delta = float3(
		-Coefficient.z * Coefficient.z + Coefficient.y,
		-Coefficient.y * Coefficient.z + Coefficient.x,
		dot(float2(Coefficient.z, -Coefficient.y), Coefficient.xy)
		);

	float Discriminant = dot(float2(4.0 * Delta.x, -Delta.y), Delta.zy);

	float3 RootsA, RootsD;

	float2 xlc, xsc;

	// Algorithm A
	{
		float A_a = 1.0;
		float C_a = Delta.x;
		float D_a = -2.0 * B * Delta.x + Delta.y;

		// Take the cubic root of a normalized complex number
		float Theta = atan2(sqrt(Discriminant), -D_a) / 3.0;

		float x_1a = 2.0 * sqrt(-C_a) * cos(Theta);
		float x_3a = 2.0 * sqrt(-C_a) * cos(Theta + (2.0 / 3.0) * 3.14159265359);

		float xl;
		if ((x_1a + x_3a) > 2.0 * B)
			xl = x_1a;
		else
			xl = x_3a;

		xlc = float2(xl - B, A);
	}

	// Algorithm D
	{
		float A_d = D;
		float C_d = Delta.z;
		float D_d = -D * Delta.y + 2.0 * C * Delta.z;

		// Take the cubic root of a normalized complex number
		float Theta = atan2(D * sqrt(Discriminant), -D_d) / 3.0;

		float x_1d = 2.0 * sqrt(-C_d) * cos(Theta);
		float x_3d = 2.0 * sqrt(-C_d) * cos(Theta + (2.0 / 3.0) * 3.14159265359);

		float xs;
		if (x_1d + x_3d < 2.0 * C)
			xs = x_1d;
		else
			xs = x_3d;

		xsc = float2(-D, xs + C);
	}

	float E = xlc.y * xsc.y;
	float F = -xlc.x * xsc.y - xlc.y * xsc.x;
	float G = xlc.x * xsc.x;

	float2 xmc = float2(C * F - B * G, -B * F + C * E);

	float3 Root = float3(xsc.x / xsc.y, xmc.x / xmc.y, xlc.x / xlc.y);

	if (Root.x < Root.y && Root.x < Root.z)
		Root.xyz = Root.yxz;
	else if (Root.z < Root.x && Root.z < Root.y)
		Root.xyz = Root.xzy;

	return Root;
}



float4 LTC_Evaluate(
	float3 N, float3 V, float3 P, float3x3 Minv, float3 points[4], bool spec = false)
{
	// construct orthonormal basis around N
	float3 T1, T2;
	T1 = normalize(V - N * dot(V, N));
	T2 = cross(N, T1);

	// rotate area light in (T1, T2, N) basis
	float3x3 R = (float3x3(T1, T2, N));

	// polygon (allocate 5 vertices for clipping)
	float3 L_[4];
	L_[0] = mul(R, points[0] - P);
	L_[1] = mul(R, points[1] - P);
	L_[2] = mul(R, points[2] - P);
	L_[3] = mul(R, points[3] - P);

	// init ellipse
	float3 C = 0.5 * (L_[0] + L_[2]);
	float3 V1 = 0.5 * (L_[1] - L_[2]);
	float3 V2 = 0.5 * (L_[1] - L_[0]);

	C = mul(C, Minv);
	V1 = mul(V1, Minv);
	V2 = mul(V2, Minv);

	if (dot(cross(V1, V2), C) < 0.0)
		return 0;

	// compute eigenfloattors of ellipse
	float a, b;
	float d11 = dot(V1, V1);
	float d22 = dot(V2, V2);
	float d12 = dot(V1, V2);
	if (abs(d12) / sqrt(d11 * d22) > 0.0001)
	{
		float tr = d11 + d22;
		float det = -d12 * d12 + d11 * d22;

		// use sqrt matrix to solve for eigenvalues
		det = sqrt(det);
		float u = 0.5 * sqrt(tr - 2.0 * det);
		float v = 0.5 * sqrt(tr + 2.0 * det);
		float e_max = (u + v) * (u + v);
		float e_min = (u - v) * (u - v);

		float3 V1_, V2_;

		if (d11 > d22)
		{
			V1_ = d12 * V1 + (e_max - d11) * V2;
			V2_ = d12 * V1 + (e_min - d11) * V2;
		}
		else
		{
			V1_ = d12 * V2 + (e_max - d22) * V1;
			V2_ = d12 * V2 + (e_min - d22) * V1;
		}

		a = 1.0 / e_max;
		b = 1.0 / e_min;
		V1 = normalize(V1_);
		V2 = normalize(V2_);
	}
	else
	{
		a = 1.0 / dot(V1, V1);
		b = 1.0 / dot(V2, V2);
		V1 *= sqrt(a);
		V2 *= sqrt(b);
	}

	float3 V3 = cross(V1, V2);
	if (dot(C, V3) < 0.0)
		V3 *= -1.0;

	float L = dot(V3, C);
	float x0 = dot(V1, C) / L;
	float y0 = dot(V2, C) / L;

	float E1 = 1.0f / max(sqrt(a), 0.001);
	float E2 = 1.0f / max(sqrt(b), 0.001);

	a *= L * L;
	b *= L * L;

	float c0 = a * b;
	float c1 = a * b * (1.0 + x0 * x0 + y0 * y0) - a - b;
	float c2 = 1.0 - a * (1.0 + x0 * x0) - b * (1.0 + y0 * y0);
	float c3 = 1.0;

	float3 roots = SolveCubic(float4(c0, c1, c2, c3));
	float e1 = roots.x;
	float e2 = roots.y;
	float e3 = roots.z;

	float3 avgDir = float3(a * x0 / (a - e2), b * y0 / (b - e2), 1.0);

	float3x3 rotate = float3x3(V1, V2, V3);

	avgDir = mul(avgDir, rotate);
	avgDir = normalize(avgDir);

	float L1 = sqrt(-e2 / e3);
	float L2 = sqrt(-e2 / e1);

	float formFactor = L1 * L2 / sqrt((1.0 + L1 * L1) * (1.0 + L2 * L2));

	const float LUT_SIZE = 256.0;
	const float LUT_SCALE = (LUT_SIZE - 1.0) / LUT_SIZE;
	const float LUT_BIAS = 0.5 / LUT_SIZE;

	float2 uv = float2(avgDir.z * 0.5 + 0.5, formFactor);

	uv = uv * LUT_SCALE + LUT_BIAS;
	uv.y = 1 - uv.y;
	float scale = tex2D(_DiscClip, uv).r;
	formFactor *= scale;
	
	float3 LL[4];
	L_[0] = mul(L_[0], Minv);
	L_[1] = mul(L_[1], Minv);
	L_[2] = mul(L_[2], Minv);
	L_[3] = mul(L_[3], Minv);
	LL[0] = normalize(L_[0]);
	LL[1] = normalize(L_[1]);
	LL[2] = normalize(L_[2]);
	LL[3] = normalize(L_[3]);
	float3 sum = 0;
	sum += IntegrateEdgeVec(LL[0], LL[1]);
	sum += IntegrateEdgeVec(LL[1], LL[2]);
	sum += IntegrateEdgeVec(LL[2], LL[3]);
	sum += IntegrateEdgeVec(LL[3], LL[0]);

	float3 fetchDir = normalize(sum);
	return float4(formFactor * FetchDiffuseFilteredTexture(L_[0], L_[1], L_[2], L_[3], fetchDir, spec), fetchDir.z);
}



float3 QuadLight(SurfaceInfo surface, float3 lightColor, float3 lightPos, float4 lightX, float4 lightY, float3 pos, float3 view) {

	float3 baseColor = surface.baseColor;
	float roughness = 1 - surface.smoothness;
	float metallic = surface.metallic;
	float3 normal = surface.normal;
	float3 gnormal = surface.gnormal;
	float ao = surface.diffuseAO_specAO.x;

	float3 lp = lightPos;
	float3 lx = lightX.xyz;
	float3 ly = lightY.xyz;
	float2 size = float2(lightX.w, lightY.w) / 2;
	lx *= size.x;
	ly *= size.y;
	float3 ln = normalize(cross(lx, ly));

	half3x3 basis;
	basis[0] = normalize(view - normal * dot(view, normal));
	basis[1] = normalize(cross(normal, basis[0]));
	basis[2] = normal;

	half4x3 L = half4x3(half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly));

	L = L - half4x3(pos, pos, pos, pos);
	L = mul(L, transpose(basis));

	half theta = acos(dot(view, normal));
	half2 uv = half2(lerp(0.09, 0.64, roughness), theta / 1.57);

	half3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

	float3 result = 0;
	float3 specColor;
	baseColor = DiffuseAndSpecularFromMetallic(baseColor, metallic, /*out*/ specColor);
	baseColor *= 1 - surface.transparent;

	half4 diffuseTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Diffuse, AmpDiffAmpSpecFresnel.x);
	result = lerp(ao, 1, diffuseTerm.a * 0.8 + 0.2) * diffuseTerm.rgb * baseColor;

	half3 specularTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Specular, AmpDiffAmpSpecFresnel.y, true);
	half3 fresnelTerm = specColor + (1.0 - specColor) * AmpDiffAmpSpecFresnel.z;
	result += CalculateSpecAO(ao, roughness, view, gnormal) * specularTerm * fresnelTerm * M_PI;

	return result * lightColor;
}


float3 TubeLight(SurfaceInfo surface, float3 lightColor, float3 lightPos, float4 lightX, float4 lightY, float3 pos, float3 view) {

	float3 baseColor = surface.baseColor;
	float roughness = 1 - surface.smoothness;
	float metallic = surface.metallic;
	float3 normal = surface.normal;
	float3 gnormal = surface.gnormal;
	float ao = surface.diffuseAO_specAO.x;


	float3 lp = lightPos;
	float3 ly = normalize(cross(lightPos - pos, lightX.xyz));
	float3 lx = lightX.xyz;
	float2 size = float2(lightX.w, lightY.w) / 2;
	lx *= size.x;
	ly *= size.y;
	float3 ln = normalize(cross(lx, ly));

	half3x3 basis;
	basis[0] = normalize(view - normal * dot(view, normal));
	basis[1] = normalize(cross(normal, basis[0]));
	basis[2] = normal;

	half4x3 L = half4x3(half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly));

	L = L - half4x3(pos, pos, pos, pos);
	L = mul(L, transpose(basis));

	half theta = acos(dot(view, normal));
	half2 uv = half2(lerp(0.09, 0.64, roughness), theta / 1.57);

	half3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

	float3 result = 0;
	float3 specColor;
	baseColor = DiffuseAndSpecularFromMetallic(baseColor, metallic, /*out*/ specColor);
	baseColor *= 1 - surface.transparent;

	half4 diffuseTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Diffuse, AmpDiffAmpSpecFresnel.x);
	result = lerp(ao, 1, diffuseTerm.a * 0.8 + 0.2) * diffuseTerm.rgb * baseColor;

	half3 specularTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Specular, AmpDiffAmpSpecFresnel.y, true);
	half3 fresnelTerm = specColor + (1.0 - specColor) * AmpDiffAmpSpecFresnel.z;
	result += CalculateSpecAO(ao, roughness, view, gnormal) * specularTerm * fresnelTerm * M_PI;

	return result * lightColor;
}


float3 DiscLight(SurfaceInfo surface, float3 lightColor, float3 lightPos, float4 lightX, float4 lightY, float3 pos, float3 view) {

	float3 baseColor = surface.baseColor;
	float roughness = 1 - surface.smoothness;
	float metallic = surface.metallic;
	float3 normal = surface.normal;
	float3 gnormal = surface.gnormal;
	float ao = surface.diffuseAO_specAO.x;

	float3 lp = lightPos;
	float3 lx = lightX.xyz;
	float3 ly = lightY.xyz;
	float3 ln = cross(lx, ly);
	float2 size = float2(lightX.w, lightY.w) / 2;
	lx *= size.x;
	ly *= size.y;

	half3x3 basis;
	basis[0] = normalize(view - normal * dot(view, normal));
	basis[1] = normalize(cross(normal, basis[0]));
	basis[2] = normal;

	half4x3 L = half4x3(half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly));

	half theta = acos(dot(view, normal));
	half2 uv = half2(lerp(0.09, 0.64, roughness), theta / 1.57);

	float3 points[4] = { half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly) };

	half3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

	float3 result = 0;
	float3 specColor;
	baseColor = DiffuseAndSpecularFromMetallic(baseColor, metallic, /*out*/ specColor);
	baseColor *= 1 - surface.transparent;

	half3x3 Minv = 0;
	Minv._m22 = 1;
	{
		float3 l = normalize(_LightPos - pos);
		if (dot(l, ln) > 0.98 && dot(l, normal) < 0.3 && distance(_LightPos, pos) < size.x / 2.2)
			Minv._m00_m02_m11_m20 = tex2D(_TransformInv_Diffuse, uv);
		else
			Minv._m00_m02_m11_m20 = half4(1, 0, 1, 0);
		half4 diffuseTerm = LTC_Evaluate(normal, view, pos, Minv, points);
		result = lerp(ao, 1, diffuseTerm.a * 0.8 + 0.2) * diffuseTerm.rgb * baseColor;
		if (any(isnan(result))) result = baseColor;
	}
	{
		Minv._m00_m02_m11_m20 = tex2D(_TransformInv_Specular, uv);
		half3 specularTerm = LTC_Evaluate(normal, view, pos, Minv, points, true);
		specularTerm *= 1 - smoothstep(0, 0.1, dot(reflect(view, normal), ln)) * (1 - roughness); // prevent float precision lost
		half3 fresnelTerm = specColor + (1.0 - specColor) * AmpDiffAmpSpecFresnel.z;
		result += CalculateSpecAO(ao, roughness, view, gnormal) * specularTerm * fresnelTerm;
	}

	return result * lightColor;
}


#endif