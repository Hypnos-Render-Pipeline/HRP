#ifndef LTC_H_
#define LTC_H_


float3 _LightPos;
float4 _LightX, _LightY;


float4 _LightColor;
sampler2D _LightDiffuseTex, _LightSpecTex;
sampler2D _TransformInv_Diffuse;
sampler2D _TransformInv_Specular;
sampler2D _AmpDiffAmpSpecFresnel;


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

	// Flip texture to match OpenGL conventions
	Puv = Puv * float2(1, -1) + float2(0, 1);

	float lod = log(2048.0 * d) / log(3.0);
	lod = min(lod, 7.0);

	float lodA = floor(lod);
	float lodB = ceil(lod);
	float t = lod - lodA;


	float3 a;
	float3 b;

	if (spec) {
		Puv.y = 1 - Puv.y;
		a = tex2Dlod(_LightSpecTex, float4(Puv, 0, lodA));
		b = tex2Dlod(_LightSpecTex, float4(Puv, 0, lodB));
	}
	else {
		Puv.y = 1 - Puv.y;
		Puv = Puv / 2 + 0.25;
		a = tex2Dlod(_LightDiffuseTex, float4(Puv, 0, lodA));
		b = tex2Dlod(_LightDiffuseTex, float4(Puv, 0, lodB));
	}
	//if (!spec) return lerp(a, b, t); return 0;
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


#endif