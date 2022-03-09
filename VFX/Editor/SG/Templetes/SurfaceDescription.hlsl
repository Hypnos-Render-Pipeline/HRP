

struct SurfaceDescriptionInputs {
	float3 WorldSpacePosition;
	float3 WorldSpaceNormal;
	float3 WorldSpaceTangent;
	float4 ScreenPosition;
	float4 uv0;
	$SurfaceDescriptionInputs.TangentSpaceNormal: float3 TangentSpaceNormal;
	$SurfaceDescriptionInputs.TimeParameters:			float4 TimeParameters;
	$SurfaceDescriptionInputs.VertexColor:				float3 VertexColor;
};



SurfaceDescriptionInputs Convert2PixelGraph(float3 wpos, float3 normal, float4 tangent, float4 screenUV, float2 uv, float3 vColor) {

	SurfaceDescriptionInputs to_pix_graph = (SurfaceDescriptionInputs)0;
	$SurfaceDescriptionInputs.WorldSpacePosition:		to_pix_graph.WorldSpacePosition = wpos;
	$SurfaceDescriptionInputs.WorldSpaceNormal:			to_pix_graph.WorldSpaceNormal = normal;
	$SurfaceDescriptionInputs.WorldSpaceTangent:		to_pix_graph.WorldSpaceTangent = tangent;
	$SurfaceDescriptionInputs.ScreenPosition:			to_pix_graph.ScreenPosition = screenUV;
	$SurfaceDescriptionInputs.uv0:						to_pix_graph.uv0 = float4(uv, 0, 0);
	$SurfaceDescriptionInputs.TangentSpaceNormal:		to_pix_graph.TangentSpaceNormal = float3(0, 0, 1);
	$SurfaceDescriptionInputs.TimeParameters:			to_pix_graph.TimeParameters = float4(_Time.y, sin(_Time.y), cos(_Time.y), 0);
	$SurfaceDescriptionInputs.VertexColor:				to_pix_graph.VertexColor = vColor;
	
	return to_pix_graph;
}

$splice(GraphPixel)