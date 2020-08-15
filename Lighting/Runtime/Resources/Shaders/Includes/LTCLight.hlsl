#ifndef LTC_H_
#define LTC_H_


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

	float lodA = floor(lod);
	float lodB = ceil(lod);
	float t = lod - lodA;


	float3 a;
	float3 b;

	if (spec) {
		a = tex2Dlod(_LightSpecTex, float4(Puv, 0, lodA));
		b = tex2Dlod(_LightSpecTex, float4(Puv, 0, lodB));
	}
	else {
		Puv = Puv / 2 + 0.25;
		a = tex2Dlod(_LightDiffuseTex, float4(Puv, 0, lodA));
		b = tex2Dlod(_LightDiffuseTex, float4(Puv, 0, lodB));
	}

	return lerp(a, b, t);
}

// Baum's equation
// Expects non-normalized vertex positions
float3 PolygonRadiance(half4x3 L, bool spec)
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

	return max(0, sum.z * 0.15915) * FetchDiffuseFilteredTexture(LL[0], LL[1], LL[2], LL[3], fetchDir, spec);
}

half3 TransformedPolygonRadiance(half4x3 L, half2 uv, sampler2D transformInv, half amplitude, bool spec = false)
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

	return PolygonRadiance(LTransformed, spec) * amplitude;
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



float3 LTC_Evaluate(
	float3 N, float3 V, float3 P, float3x3 Minv, float3 points[4], bool spec = false)
{
	// construct orthonormal basis around N
	float3 T1, T2;
	T1 = normalize(V - N * dot(V, N));
	T2 = cross(N, T1);

	// rotate area light in (T1, T2, N) basis
	float3x3 R = (float3x3(T1, T2, N));

	// polygon (allocate 5 vertices for clipping)
	float3 L_[3];
	L_[0] = mul(R, points[0] - P);
	L_[1] = mul(R, points[1] - P);
	L_[2] = mul(R, points[2] - P);

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

	const float LUT_SIZE = 64.0;
	const float LUT_SCALE = (LUT_SIZE - 1.0) / LUT_SIZE;
	const float LUT_BIAS = 0.5 / LUT_SIZE;

	float2 uv = float2(avgDir.z * 0.5 + 0.5, formFactor);

	uv = uv * LUT_SCALE + LUT_BIAS;
	uv.y = 1 - uv.y;
	float scale = tex2D(_DiscClip, uv).r;
	return formFactor * scale;//*FetchDiffuseFilteredTexture(points[0] - P, points[1] - P, points[2] - P, points[3] - P, reflect(V, N), true);
}


#endif