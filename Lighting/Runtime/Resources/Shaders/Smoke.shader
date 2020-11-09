Shader "HRP/Smoke"
{
    Properties
    {
        _Volume("Volume Texture", 2D) = "white" {}
        _SliceNum("Slice Num", int) = 256
        _AtlasWidthCount("Atlas Width Count", Int) = 16

        _Scatter("Scatter", Range(0,100)) = 1
        _Absorb("Absorb", Range(0, 100)) = 0
        _G("G", Range(-1, 1)) = 0

        _MaterialID("Material ID", float) = -1
    }
    SubShader
    {
        Pass
        {
            Name "RT"
            Tags{ "LightMode" = "RT" }

            CGPROGRAM

            #pragma exclude_renderers gles
            #pragma raytracing test

            #include "./Includes/RT/Include/RTLitInclude.hlsl" 

            float _MaterialID;

            [shader("closesthit")]
            void ClosestHit(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes) {

            }

            [shader("anyhit")]
            void AnyHit(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes) {

                if (rayIntersection.weight.w != TRACE_FOG_VOLUME) {
                    IgnoreHit();
                    return;
                }
                rayIntersection.t.x = RayTCurrent();
                rayIntersection.t.y = _MaterialID;
                rayIntersection.t.z = InstanceID();

                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
                float3 normalOS = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
                rayIntersection.t.w = dot(ObjectRayDirection().xyz, normalOS) > 0;
                float3x4 mat = WorldToObject3x4();
                rayIntersection.directColor = mat._m00_m10_m20;
                rayIntersection.weight.xyz = mat._m01_m11_m21;
                rayIntersection.nextDir = mat._m02_m12_m22;
                rayIntersection.normal = mat._m03_m13_m23;
                AcceptHitAndEndSearch();
            }
            
            ENDCG
        }
    }
    CustomEditor "HypnosRenderPipeline.SmokeEditor" 
}
