Shader "HRP/Smoke"
{
    Properties
    {
        _VolumeTex("Volume Texture", 2D) = "white" {}
        _SliceNum("Slice Num", int) = 256
        _AtlasWidthCount("Atlas Width Count", Int) = 16

        _Density("Density", Range(0,100)) = 1
        _Scatter("Scatter", Range(0, 1)) = 0.5
        _G("Phase G", Range(-1, 1)) = 0

        _MaterialID("Material ID", float) = -1
    }
    
    SubShader
    {
        ColorMask 0 Cull Front ZWrite Off ZTest Always
        
        Tags { "Queue" = "Transparent+1" }
        Pass
        {
            Name "Fog"
            Tags { "LightMode" = "Fog" }
            HLSLPROGRAM
            #pragma vertex      vert
            #pragma fragment    frag
            #pragma target		5.0

            #include "../Includes/FogInclude.hlsl"

            //CBUFFER_START(UnityPerMaterial)
                float _Density, _Scatter, _G;
                uint _AtlasWidthCount, _SliceNum;
                Texture2D _VolumeTex;
                SamplerState linear_clamp_sampler;
            //CBUFFER_END
                  
            float3 Fog(float3 wpos, float3 uv) {

                float3 volume_uv = uv - 0.5;
                if (any(volume_uv > 0.495)) return 0;
                volume_uv += 0.5;
                float zslice = volume_uv.z * _SliceNum;
                int width = _AtlasWidthCount;
                uint z = floor(zslice);
                uint h = _SliceNum / width + (_SliceNum % width != 0 ? 1 : 0);
                float2 scale = 1 / float2(width, h);
                float2 offset = volume_uv.xy * scale;
                float2 uv1 = float2(z % width, z / width) * scale; uv1.y = 1 - uv1.y - scale.y;
                uv1 += offset;
                z += 1;
                float2 uv2 = float2(z % width, z / width) * scale; uv2.y = 1 - uv2.y - scale.y;
                uv2 += offset;
                float density1 = _VolumeTex.SampleLevel(linear_clamp_sampler, uv1, 0);
                float density2 = _VolumeTex.SampleLevel(linear_clamp_sampler, uv2, 0);
                float density = max(0, lerp(density1, density2, frac(zslice)));

                return float3(density * _Density, _Scatter, _G);
            }

            ENDHLSL
        }
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

            #include "../Includes/RT/Include/RTLitInclude.hlsl" 

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