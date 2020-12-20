Shader "Hidden/DeferredLighting"
{
    Properties { _MainTex("Texture", 2D) = "white" {} }
    SubShader
    {
        Pass // local light
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

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _EmissionTex, _AOTex;
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
                                        _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
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

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex;
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

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
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
                                                    _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    0,
                                                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 res = QuadLight(info, _LightColor, _LightPos, _LightX, _LightY, pos, view);

                uint2 id = i.vertex.xy;
                int k[16] = { 15,7,13,5,3,11,1,9,12,4,14,6,0,8,2,10 };
                int index = id.x % 4 + id.y % 4 * 4;
                float noise = hash12(id);

                if (noise * 16 > k[index]) {
                    res += .2 / 255.;
                }
                return res;
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

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex;
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

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
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
                                                    _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    0,
                                                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 res = TubeLight(info, _LightColor, _LightPos, _LightX, _LightY, pos, view);

                uint2 id = i.vertex.xy;
                int k[16] = { 15,7,13,5,3,11,1,9,12,4,14,6,0,8,2,10 };
                uint index = id.x % 4 + id.y % 4 * 4;
                float noise = hash12(id);

                if (noise * 16 > k[index]) {
                    res += .2 / 255.;
                }
                return res;
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

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex;
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

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
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

                float3 diffuse;
                float roughness;
                float metallic;
                float3 normal;
                float3 emission;
                float3 gnormal;
                float ao;

                SurfaceInfo info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                                    0,
                                                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 res = DiscLight(info, _LightColor, _LightPos, _LightX, _LightY, pos, view);

                uint2 id = i.vertex.xy;
                int k[16] = { 15,7,13,5,3,11,1,9,12,4,14,6,0,8,2,10 };
                uint index = id.x % 4 + id.y % 4 * 4;
                float noise = hash12(id);

                if (noise * 16 > k[index]) {
                    res += .2 / 255.;
                }
                return res;

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

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex;
            SamplerState sampler_point_clamp;
            sampler2D _MainTex;

            int _DebugTiledLight;

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            sampler2D _TerrainHeight;
            float2 _HeightRange;
            float H(float2 xz) {
                return lerp(_HeightRange.x, _HeightRange.y, tex2Dlod(_TerrainHeight, float4(xz / 1024, 0, 0)).x);
            }

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
                                        _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        0,
                                        _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 sunColor = Sunlight(float3(pos.x - camPos.x, planet_radius + max(pos.y, 95), pos.z - camPos.z), _SunDir);

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

                if (any(res > 0)) {
                    float3 p = pos;
                    p.y = max(p.y, H(p.xz));
                    for (int i = 0; i < 32; i++)
                    {
                        p += _SunDir * lerp(0.5, 1, (float)i / 32);
                        if (H(p.xz) > p.y + 1) {
                            res = 0;
                            break;
                        }
                    }
                }

                return float4(res, 0) + sceneColor;
            }
            ENDCG
        }

        
        Pass // ray traced local light
        {
            ZWrite off
            Cull off
            Blend One One

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

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex, _RayTracedLocalShadowMask;
            SamplerState sampler_point_clamp;

            cbuffer _TargetLocalLight {
                float4 position_range;
                float4 radiance_type;
                float4 mainDirection_id;
                float4 geometry;        // Spot:    cosineAngle(x)
                                        // Sphere:  radius(x)
                                        // Tube:    length(x), radius(y)
                                        // Quad:    size(xy)
                                        // Disc:    radius(x)
            }

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
                                        _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        0,
                                        _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 res = 0;
                float shadow = _RayTracedLocalShadowMask.SampleLevel(sampler_point_clamp, i.uv, 0);

                if (shadow == 0) return 0;

                Light light = {position_range, radiance_type, mainDirection_id, geometry};
                SolvedLocalLight l = SolveLight(light, pos);

                return float4(PBS(PBS_FULLY, info, l.dir, l.radiance, view) * shadow, 0);
            }
            ENDCG
        }
                
        Pass // ray traced local light volume
        {
            ZWrite off
            Cull back
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/Light.hlsl"
            #include "../Includes/PBS.hlsl"

            struct appdata 
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex, _RayTracedLocalShadowMask;
            SamplerState sampler_point_clamp;

            cbuffer _TargetLocalLight {
                float4 position_range;
                float4 radiance_type;
                float4 mainDirection_id;
                float4 geometry;        // Spot:    cosineAngle(x)
                                        // Sphere:  radius(x)
                                        // Tube:    length(x), radius(y)
                                        // Quad:    size(xy)
                                        // Disc:    radius(x)
            }

            int _DebugTiledLight;

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;
            float4 _Pixel_WH;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.vertex.xy + 0.5) * _Pixel_WH.zw;
                float d = _DepthTex.SampleLevel(sampler_point_clamp, uv, 0).x;
                if (d == 0 || i.vertex.z < d) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }
                float3 view = normalize(camPos - pos);

                SurfaceInfo info = (SurfaceInfo)0;
                info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, uv, 0),
                                        _SpecTex.SampleLevel(sampler_point_clamp, uv, 0),
                                        _NormalTex.SampleLevel(sampler_point_clamp, uv, 0),
                                        0,
                                        _AOTex.SampleLevel(sampler_point_clamp, uv, 0));

                float3 res = 0;
                float shadow = _RayTracedLocalShadowMask.SampleLevel(sampler_point_clamp, uv, 0);

                if (shadow == 0) return 0;

                Light light = {position_range, radiance_type, mainDirection_id, geometry};
                SolvedLocalLight l = SolveLight(light, pos);

                return float4(PBS(PBS_FULLY, info, l.dir, l.radiance, view) * shadow, 0);
            }
            ENDCG
        }
    }
}
