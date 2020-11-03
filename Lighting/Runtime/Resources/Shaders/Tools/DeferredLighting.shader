Shader "Hidden/DeferredLighting"
{
    Properties { _MainTex("Texture", 2D) = "white" {} }
    SubShader
    {
        Pass // normal light
        {
            ZWrite off
            Cull off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/Light.hlsl"
            #include "../Includes/PBS.hlsl"

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;

            int _DebugTiledLight;

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos; 
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }
                float3 view = normalize(camPos - pos);

                SurfaceInfo info = (SurfaceInfo)0;
                info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 res = 0;

                BegineLocalLightsLoop(i.uv, pos, _VP_Inv);
                {
                    res += PBS(PBS_FULLY, info, light.dir, light.radiance, view);

                    if (_DebugTiledLight) {
                        res += float3(0.05, 0, 0.05);
                    }
                }
                EndLocalLightsLoop;

                return float4(res + info.emission, 1);
            }
            ENDCG
        }


        Pass // quad lighjt
        {
            ZWrite off
            Cull off
            Blend DstAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/LTCLight.hlsl"
            #include "../Includes/PBS.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;


            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }

                float3 view = normalize(camPos - pos);

                SurfaceInfo info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                return QuadLight(info, _LightColor, _LightPos, _LightX, _LightY, pos, view);
            }
            ENDCG
        }

        Pass // tube light
        {
            ZWrite off
            Cull off
            Blend DstAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/LTCLight.hlsl"
            #include "../Includes/PBS.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;


            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }

                float3 view = normalize(camPos - pos);

                SurfaceInfo info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                return QuadLight(info, _LightColor, _LightPos, _LightX, _LightY, pos, view);
            }
            ENDCG
        }

        Pass // disc light
        {
            ZWrite off
            Cull off
            Blend DstAlpha One
            //Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/LTCLight.hlsl"
            #include "../Includes/PBS.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;


            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }

                float3 view = normalize(camPos - pos);

                float3 baseColor;
                float roughness;
                float metallic;
                float3 normal;
                float3 emission;
                float3 gnormal;
                float ao;

                SurfaceInfo info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                return DiscLight(info, _LightColor, _LightPos, _LightX, _LightY, pos, view);

            }
            ENDCG
        }

        Pass // sun light
        {
            ZWrite off
            Cull off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/Light.hlsl"
            #include "../Includes/PBS.hlsl"

            #define T T_TAB
            #include "../Includes/Atmo/Atmo.hlsl"

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;
            sampler2D _MainTex;

            int _DebugTiledLight;

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            float3 _SunColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;
                float4 sceneColor = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
                if (sceneColor.a == 0) return sceneColor;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos; 
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }
                float dis = distance(camPos, pos);
                float3 view = (camPos - pos) / dis;

                SurfaceInfo info = (SurfaceInfo)0;
                info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 sunColor = _SunColor * Sunlight(float3(0, planet_radius + max(pos.y, 95), 0), _SunDir);
                
#if 1           // trick for the sun disk size
                float3 halfDir = normalize(view + _SunDir);
                float3 no = info.normal;
                info.normal = normalize(lerp(halfDir, info.normal, 1 - dot(halfDir, info.normal) * 0.3 * (pow(sun_angle, 0.2) / pow(0.008726647, 0.2))));
                float3 res = PBS(PBS_SS_SPEC, info, _SunDir, sunColor, view);
                info.normal = no;
                res += PBS(PBS_SS_DIFFUSE, info, _SunDir, sunColor, view);
#else
                float3 res = PBS(PBS_FULLY, info, _SunDir, sunColor, view);
#endif
                return float4(res, 1) + sceneColor;
            }
            ENDCG
        }

    }
}
