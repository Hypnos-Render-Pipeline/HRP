#ifndef RT_COMMON_H_
#define RT_COMMON_H_

// Engine includes
#include "UnityRaytracingMeshUtils.cginc"

RaytracingAccelerationStructure         _RaytracingAccelerationStructure;
 
float4x4	_P_Inv, _V_Inv;
float4		_Pixel_WH;
int			_Frame_Index;

// Structure that defines the current state of the intersection
struct RayIntersection
{
	float3	directColor;
	float	roughness;
	float4  t;
	float4	weight;
	int4	sampleState;
	float3	nextDir;
	float3	normal;
};

struct RayIntersection_RTGI
{						// when not miss																// when miss
	float t;			// distance																			
	int data0;			// albedo rgb: 24bits, smoothness: 8bits
	int data1;			// normal: 24bits, metallic: 6bits, front: 1bit, miss flag: 1bit				// set miss flag to 1
	int data2;			// emission rgb: 24bits, emission multiplier: 8bits								// sky hdr
	int data3;			// transparent: 8bit, index 8bit, index_rate 8 bit
};

struct GBuffer_RTGI
{
	half3 albedo;
	half metallic;
	half3 emission;
	half dis;
	half3 normal;
	half smoothness;
	float transparent;
	float index;
	float index_rate;
	bool miss;
	bool front;
};

int EncodeHDR2Int(half3 hdr)
{
	half m = max(max(max(hdr.x, hdr.y), hdr.z), 1);
	half3 rgb_half = min(1, hdr / m);
	int3 rgb = int3(rgb_half * 255);
	return (rgb.x << 24) +
			(rgb.y << 16) +
			(rgb.z << 8) +
			int(min(1, log2(m) / 100) * 255);
}
half3 DecodeInt2HDR(int data)
{
	half3 rgb = (int3(data >> 24, data >> 16, data >> 8) & 0xFF) / 255.0f;
	half mul = (data & 0xFF) / 255.0f * 100;
	return rgb * exp2(mul);
}

uint EncodeNormal24(float3 n) {
	uint3 res;
	n.xy /= dot(1, abs(n));
	if (n.z < 0) n.xy = (1 - abs(n.yx)) * (n.xy >= 0 ? 1 : -1);
	float2 k = (n.xy + 1) / 2;
	uint2 kk = k * 4095;
	res.xy = (kk.xy & 255);
	res.z = dot(1, (kk.xy >> 8) << uint2(4, 0));
	return (res.x << 16) + (res.y << 8) + res.z;
}

float3 DecodeNormal24(uint k_) {
	uint3 a = uint3(k_ >> 16, k_ >> 8, k_) & 0xFF;
	float3 k;
	k.xy = (a.xy + (uint2(a.z >> 4, a.z & 15) << 8)) / 4095.0f;
	k.xy = k.xy * 2 - 1;
	float3 normal = float3(k.xy, 1 - dot(1, abs(k.xy)));
	if (normal.z < 0) normal.xy = (1 - abs(normal.yx)) * (normal.xy >= 0 ? 1 : -1);
	return normalize(normal);
}

RayIntersection_RTGI EncodeGBuffer2RIData(const GBuffer_RTGI gbuffer)
{
	RayIntersection_RTGI o;
	
	o.t = gbuffer.dis;
	
	int3 albedo_rgb = int3(saturate(gbuffer.albedo) * 0xFF);
	o.data0 = (albedo_rgb.x << 24) +
				(albedo_rgb.y << 16) +
				(albedo_rgb.z << 8) +
				int(gbuffer.smoothness * 0xFF);
	
	o.data1 = (EncodeNormal24(gbuffer.normal) << 8) +
				(int(gbuffer.metallic * 0x3F) << 2) +
				(gbuffer.front ? 2 : 0);
	
	o.data2 = EncodeHDR2Int(gbuffer.emission);

	o.data3 = (int(saturate(gbuffer.transparent) * 0xFF) << 24) +
				(int(((clamp(gbuffer.index, 1, 3) - 1) / 2) * 0xFF) << 16) + 
				(int(gbuffer.index_rate * 0xFF) << 8);

	return o;
}


GBuffer_RTGI DecodeIData2GBuffer(const RayIntersection_RTGI data)
{
	GBuffer_RTGI o = (GBuffer_RTGI)0;
										 
	o.dis = data.t;
	if ((data.data1 & 1) != 0) { // miss flag is set
		o.miss = true;
		return o;
	}
	else {	
		o.miss = false;		 					
		o.albedo = (int3(data.data0 >> 24, data.data0 >> 16, data.data0 >> 8) & 0xFF) / 255.0f;
		o.smoothness = (data.data0 & 0xFF) / 255.0f;

		o.metallic = ((data.data1 >> 2) & 0x3F) / 63.0f;
		o.normal = DecodeNormal24(data.data1 >> 8);
		o.front = data.data1 & 2;

		o.emission = DecodeInt2HDR(data.data2);

		o.transparent = ((data.data3 >> 24) & 0xFF) / 255.0f;
		o.index = 1 + 2 * (((data.data3 >> 16) & 0xFF) / 255.0f);
		o.index_rate = ((data.data3 >> 8) & 0xFF) / 255.0f;

	}
	return o;
}


struct AttributeData
{
	// Barycentric value of the intersection
	float2 barycentrics;
};

// Macro that interpolate any attribute using barycentric coordinates
#define INTERPOLATE_RAYTRACING_ATTRIBUTE(A0, A1, A2, BARYCENTRIC_COORDINATES) (A0 * BARYCENTRIC_COORDINATES.x + A1 * BARYCENTRIC_COORDINATES.y + A2 * BARYCENTRIC_COORDINATES.z)

// Structure to fill for intersections
struct IntersectionVertex
{
	// Object space position of the vertex
	float3 positionOS;
	// Object space normal of the vertex
	float3 normalOS;
	// Object space normal of the vertex
	float4 tangentOS;
	// UV coordinates
	float2 texCoord0;
	float2 texCoord1;
	float2 texCoord2;
	float2 texCoord3;
	// Vertex color
	float4 color;
	//geometry normal;
	float3 geomoralOS;
	// Value used for LOD sampling
	//float  triangleArea;
	//float  texCoord0Area;
	//float  texCoord1Area;
	//float  texCoord2Area;
	//float  texCoord3Area;
};

// Fetch the intersetion vertex data for the target vertex
void FetchIntersectionVertex(uint vertexIndex, out IntersectionVertex outVertex)
{
    outVertex.positionOS = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
    outVertex.normalOS   = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
    outVertex.tangentOS  = UnityRayTracingFetchVertexAttribute4(vertexIndex, kVertexAttributeTangent);
    outVertex.texCoord0  = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
    outVertex.texCoord1  = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord1);
    outVertex.texCoord2  = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord2);
    outVertex.texCoord3  = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord3);
    outVertex.color      = UnityRayTracingFetchVertexAttribute4(vertexIndex, kVertexAttributeColor);
}

void GetCurrentIntersectionVertex(AttributeData attributeData, out IntersectionVertex outVertex)
{
	// Fetch the indices of the currentr triangle
	uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

	// Fetch the 3 vertices
	IntersectionVertex v0, v1, v2;
	FetchIntersectionVertex(triangleIndices.x, v0);
	FetchIntersectionVertex(triangleIndices.y, v1);
	FetchIntersectionVertex(triangleIndices.z, v2);

	// Compute the full barycentric coordinates
	float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);

	// Interpolate all the data
    outVertex.positionOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.positionOS, v1.positionOS, v2.positionOS, barycentricCoordinates);
	outVertex.normalOS   = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
    outVertex.tangentOS  = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.tangentOS, v1.tangentOS, v2.tangentOS, barycentricCoordinates);
    outVertex.texCoord0  = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord0, v1.texCoord0, v2.texCoord0, barycentricCoordinates);
	outVertex.texCoord1  = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord1, v1.texCoord1, v2.texCoord1, barycentricCoordinates);
	outVertex.texCoord2  = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord2, v1.texCoord2, v2.texCoord2, barycentricCoordinates);
	outVertex.texCoord3  = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.texCoord3, v1.texCoord3, v2.texCoord3, barycentricCoordinates);
	outVertex.color      = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.color, v1.color, v2.color, barycentricCoordinates);
	outVertex.geomoralOS = cross(v0.positionOS - v1.positionOS, v0.positionOS - v2.positionOS);
    if (dot(outVertex.normalOS, outVertex.geomoralOS) < 0)
        outVertex.geomoralOS *= -1;
	//// Compute the lambda value (area computed in object space)
	//outVertex.triangleArea  = length(cross(v1.positionOS - v0.positionOS, v2.positionOS - v0.positionOS));
	//outVertex.texCoord0Area = abs((v1.texCoord0.x - v0.texCoord0.x) * (v2.texCoord0.y - v0.texCoord0.y) - (v2.texCoord0.x - v0.texCoord0.x) * (v1.texCoord0.y - v0.texCoord0.y));
	//outVertex.texCoord1Area = abs((v1.texCoord1.x - v0.texCoord1.x) * (v2.texCoord1.y - v0.texCoord1.y) - (v2.texCoord1.x - v0.texCoord1.x) * (v1.texCoord1.y - v0.texCoord1.y));
	//outVertex.texCoord2Area = abs((v1.texCoord2.x - v0.texCoord2.x) * (v2.texCoord2.y - v0.texCoord2.y) - (v2.texCoord2.x - v0.texCoord2.x) * (v1.texCoord2.y - v0.texCoord2.y));
	//outVertex.texCoord3Area = abs((v1.texCoord3.x - v0.texCoord3.x) * (v2.texCoord3.y - v0.texCoord3.y) - (v2.texCoord3.x - v0.texCoord3.x) * (v1.texCoord3.y - v0.texCoord3.y));
}

struct FragInputs
{
	float3 position;
	float4 uv0;
	float4 uv1;
	float4 uv2;
	float4 uv3;
	float4 color;
	float3x3 tangentToWorld;
	float3 gN;
	bool isFrontFace;
};

void BuildFragInputsFromIntersection(IntersectionVertex currentVertex, out FragInputs outFragInputs)
{
    outFragInputs.position = mul(ObjectToWorld3x4(), float4(currentVertex.positionOS, 1.0)).xyz;
	outFragInputs.uv0 = float4(currentVertex.texCoord0, 0.0, 0.0);
	outFragInputs.uv1 = float4(currentVertex.texCoord1, 0.0, 0.0);
	outFragInputs.uv2 = float4(currentVertex.texCoord2, 0.0, 0.0);
	outFragInputs.uv3 = float4(currentVertex.texCoord3, 0.0, 0.0);
	outFragInputs.color = currentVertex.color;
	// Let's compute the object space binormal
	float3x3 worldToObject = (float3x3)WorldToObject3x4();
  
    float3 normalWorld = normalize(mul(currentVertex.normalOS, worldToObject));
    float3 tangentWorld = normalize(mul(currentVertex.tangentOS.xyz, worldToObject));
    tangentWorld = normalize(tangentWorld - abs(dot(normalWorld, tangentWorld)) * normalWorld);
    half3 binormalWorld = normalize(cross(normalWorld, tangentWorld)) * currentVertex.tangentOS.w;
    outFragInputs.tangentToWorld = half3x3(-tangentWorld.xyz, -binormalWorld, normalWorld);
	outFragInputs.gN = normalize(mul(currentVertex.geomoralOS, worldToObject));
    outFragInputs.isFrontFace = dot(outFragInputs.gN, WorldRayDirection()) < 0.0f;
    outFragInputs.gN *= outFragInputs.isFrontFace ? 1 : -1;
}
 
float4 GetPositionWS(float4 positionVS) {
	float4 t = mul(_V_Inv, positionVS);
	return t;
}

#define CALCULATE_DATA(fragInput, viewDir)	IntersectionVertex currentvertex;\
											GetCurrentIntersectionVertex(attributeData, currentvertex);\
											FragInputs fragInput;\
											float3 viewDir = -WorldRayDirection();\
											BuildFragInputsFromIntersection(currentvertex, fragInput);\






#endif