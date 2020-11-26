#ifndef _LIT_
#define _LIT_

float3 ObjectToWorldDir(float3 dir) {
	return normalize(mul(dir, (float3x3)unity_WorldToObject));
}

float4 ObjectToClipPos(float4 p) {
	return mul(unity_MatrixVP, mul(unity_ObjectToWorld, p));
}

float4 ObjectToClipPos(float3 p) {
	return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(p, 1)));
}

void Time(out float3 t) {
	t.x = _Time.y;
	sincos(_Time.y, t.y, t.z);
}

// ------------------------------
// ------------ PreZ ------------
// ------------------------------
#ifdef PreZ

#ifdef FEATURES_GRAPH_VERTEX
struct a2v {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;
};

float4 vert(a2v i) : SV_POSITION {
	VertexDescriptionInputs IN = (VertexDescriptionInputs)0;
	IN.ObjectSpacePosition = i.vertex;
	IN.ObjectSpaceNormal = i.normal;
	IN.ObjectSpaceTangent = i.tangent;
	IN.WorldSpacePosition = mul(unity_ObjectToWorld, i.vertex);
	IN.WorldSpaceNormal = ObjectToWorldDir(i.normal);
	IN.uv0 = float4(i.uv, 0, 0);
	Time(IN.TimeParameters);
	VertexDescription o = VertexDescriptionFunction(IN);
	return ObjectToClipPos(o.VertexPosition);
}
#else
float4 vert(float4 vertex : POSITION) : SV_POSITION { return mul(unity_MatrixVP, mul(unity_ObjectToWorld, vertex)); }
#endif
void frag() {}

#endif
// ------------------------------




// ------------------------------
// -------- GBuffer_Equal -------
// ------------------------------
#ifdef GBuffer_Equal

struct a2v {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;
};

struct v2f {
	float4 vertex : SV_POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;
	float3 wpos : TEXCOORD1;
};


#ifdef FEATURES_GRAPH_VERTEX
v2f vert(a2v i) {
	VertexDescriptionInputs IN = (VertexDescriptionInputs)0;
	IN.ObjectSpacePosition = i.vertex;
	IN.ObjectSpaceNormal = i.normal;
	IN.ObjectSpaceTangent = i.tangent;
	IN.WorldSpacePosition = mul(unity_ObjectToWorld, i.vertex);
	IN.WorldSpaceNormal = ObjectToWorldDir(i.normal);
	IN.uv0 = float4(i.uv, 0, 0);
	Time(IN.TimeParameters);

	VertexDescription res = VertexDescriptionFunction(IN);

	v2f o;
	o.vertex = ObjectToClipPos(res.VertexPosition);
	o.normal = ObjectToWorldDir(res.VertexNormal);
	o.tangent = float4(ObjectToWorldDir(i.tangent.xyz), i.tangent.w);
	float3 bt = normalize(cross(o.normal, o.tangent.xyz));
	o.tangent = float4(cross(bt, o.normal), i.tangent.w);
	o.uv = res.VertexUV;
	o.wpos = mul(unity_ObjectToWorld, res.VertexPosition);
	return o;
}
#else
v2f vert(a2v i) {
	v2f o;
	o.vertex = ObjectToClipPos(i.vertex);
	o.normal = ObjectToWorldDir(i.normal);
	o.tangent = float4(ObjectToWorldDir(i.tangent.xyz), i.tangent.w);
	o.uv = i.uv;
	o.wpos = mul(unity_ObjectToWorld, i.vertex).xyz;
	return o;
}
#endif

void frag(v2f i, out float4 target0: SV_Target0, out float4 target1 : SV_Target1, out float4 target2 : SV_Target2, out float4 target3 : SV_Target3) {

	SurfaceDescriptionInputs IN = (SurfaceDescriptionInputs)0;
	IN.TangentSpaceNormal = float3(0, 0, 1);
	IN.WorldSpacePosition = i.wpos;
	IN.WorldSpaceNormal = i.normal;
	IN.uv0 = float4(i.uv, 0, 0);
	Time(IN.TimeParameters);

	SurfaceDescription res = SurfaceDescriptionFunction(IN);

	float3 diffuse = res.Albedo;
	float3 normal = res.Normal;
	float metallic = res.Metallic;
	float smoothness = res.Smoothness;

	float3 n = normalize(i.normal), t = normalize(i.tangent.xyz);
	float3 binormal = cross(n, t) * i.tangent.w;
	float3x3 rotation = float3x3(t, binormal, n);
	normal = mul(normal, rotation);

	float3 emission = res.Emission;

	float ao = res.Occlusion;

	Encode2GBuffer(diffuse, 1 - smoothness, metallic, normal, emission, i.normal, ao, target0, target1, target2, target3);
}

#endif
// ------------------------------

#endif