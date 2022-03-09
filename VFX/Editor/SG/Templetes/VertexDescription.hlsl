$VertexDescriptionInputs.VertexColor: #define VERTEX_COLOR
$SurfaceDescriptionInputs.VertexColor: #ifndef  VERTEX_COLOR
$SurfaceDescriptionInputs.VertexColor: #define VERTEX_COLOR
$SurfaceDescriptionInputs.VertexColor: #endif


struct VertexDescriptionInputs {
	$VertexDescriptionInputs.ObjectSpacePosition:       float3 ObjectSpacePosition;
	$VertexDescriptionInputs.WorldSpacePosition:		float3 WorldSpacePosition;
	$VertexDescriptionInputs.ObjectSpaceNormal:			float3 ObjectSpaceNormal;
	$VertexDescriptionInputs.WorldSpaceNormal:			float3 WorldSpaceNormal;
	$VertexDescriptionInputs.ObjectSpaceTangent:		float3 ObjectSpaceTangent;
	$VertexDescriptionInputs.WorldSpaceTangent:			float3 WorldSpaceTangent;
	$VertexDescriptionInputs.uv0:						float4 uv0;
	$VertexDescriptionInputs.uv1:						float4 uv1;
	$VertexDescriptionInputs.uv2:						float4 uv2;
	$VertexDescriptionInputs.uv3:						float4 uv3;
	#ifdef VERTEX_COLOR
		float3 VertexColor;
	#endif
	$VertexDescriptionInputs.TimeParameters:			float4 TimeParameters;
};



VertexDescriptionInputs Convert2VertexGraph(float2 uv, float4 vertex, float3 oNormal, float4 oTangent, float3 color) {

	VertexDescriptionInputs to_vert_graph = (VertexDescriptionInputs)0;
	$VertexDescriptionInputs.ObjectSpacePosition:	to_vert_graph.ObjectSpacePosition = vertex;
	$VertexDescriptionInputs.WorldSpacePosition:	to_vert_graph.WorldSpacePosition = mul(unity_ObjectToWorld, vertex);
	$VertexDescriptionInputs.ObjectSpaceNormal:		to_vert_graph.ObjectSpaceNormal = oNormal;
	$VertexDescriptionInputs.WorldSpaceNormal:		to_vert_graph.WorldSpaceNormal = UnityObjectToWorldNormal(oNormal);
	$VertexDescriptionInputs.ObjectSpaceTangent:	to_vert_graph.ObjectSpaceTangent = oTangent;
	$VertexDescriptionInputs.WorldSpaceTangent:		to_vert_graph.WorldSpaceTangent = UnityObjectToWorldDir(oTangent.xyz);
	$VertexDescriptionInputs.uv0:					to_vert_graph.uv0 = float4(uv, 0, 0);
	$VertexDescriptionInputs.uv1:					to_vert_graph.uv1 = float4(uv, 0, 0);
	$VertexDescriptionInputs.uv2:					to_vert_graph.uv2 = float4(uv, 0, 0);
	$VertexDescriptionInputs.uv3:					to_vert_graph.uv3 = float4(uv, 0, 0);
	#ifdef VERTEX_COLOR
		to_vert_graph.VertexColor = color;
	#endif
	$VertexDescriptionInputs.TimeParameters:		to_vert_graph.TimeParameters = float4(_Time.y, sin(_Time.y), cos(_Time.y), 0);

	return to_vert_graph;
}

$splice(GraphVertex)