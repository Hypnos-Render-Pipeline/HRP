Shader "Hidden/AtmoLut"
{
    Properties { _MainTex("Texture", 2D) = "white" {} }
    SubShader
    {
        CGINCLUDE
            #include "UnityCG.cginc"
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
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

        ENDCG

        Pass // T Lut
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "../Includes/Atmo/Atmo.hlsl"

                fixed4 frag(v2f i) : SV_Target
                {
                    float2 corrected_uv = float2(i.vertex.xy - 0.5) / (_TLutResolution - 1);
                    corrected_uv.y = unzip(corrected_uv.y);
                    float3 x;
                    if (corrected_uv.y < 0.5) {
                        x = GetPoint(corrected_uv.x);
                        float horiz = length(x);
                        horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;
                        corrected_uv.y = lerp(horiz + 0.0001, 1, corrected_uv.y * 2);
                    }
                    else {
                        corrected_uv.y = (corrected_uv.y - 0.5);
                        corrected_uv.x = 1 - corrected_uv.x;
                        x = GetPoint(corrected_uv.x);
                        float horiz = length(x);
                        horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;
                        corrected_uv.y = lerp(-1, horiz - 0.0001, corrected_uv.y * 2);
                    }
                    float3 dir = GetDir_11(corrected_uv.y);
                    float3 x_0;
                    bool isGround = X_0(x, dir, x_0);
                    float3 res = T(x, x_0);
                    return min(float4(res, isGround), 1);
                }
            ENDCG
        }
        
        Pass // MS Table
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #define T T_TAB
                #include "../Includes/Atmo/Atmo.hlsl"

                fixed3 frag(v2f i) : SV_Target
                {
                    float3 s = GetDir01(i.uv.x);
                    float3 x = float3(0, lerp(planet_radius, atmosphere_radius, i.uv.y), 0);

                    return L2(x, s) * Fms(x);
                }
            ENDCG
        }

        Pass // Sky Lut
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #define T T_TAB
                #include "../Includes/Atmo/Atmo.hlsl"

                int _RenderGround;

                fixed3 frag(v2f i) : SV_Target
                {
                    float3 s = normalize(_SunDir);
                    float3 x = _WorldSpaceCameraPos;
                    x.y += planet_radius;

                    float lx = length(x);
                    float3 x_ = x / lx;
                    float xdots = dot(x_, s);
                    s = float3(max(0,sqrt(1- xdots * xdots)), xdots, 0);
                    x = float3(0, lx, 0);

                    float phi = i.uv.x * pi;

                    float vx, vz;
                    sincos(phi, vz, vx);

                    float ro = (i.vertex.y - 0.499) / (_SLutResolution.y - 0.998);
                    if (ro > 0.5) {
                        ro = ro - 0.5;
                        float horiz = length(x);
                        horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

                        if (length(x) > atmosphere_radius) {
                            float ahoriz = length(x);
                            ahoriz = -sqrt(ahoriz * ahoriz - atmosphere_radius * atmosphere_radius) / ahoriz;
                            ro = lerp(horiz + 0.0001, ahoriz - 0.0001, pow(ro * 2, 2));
                        }
                        else
                            ro = lerp(horiz + 0.0001, 1, pow(ro * 2, 2));
                    }
                    else {
                        float horiz = length(x);
                        horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;
                        ro = lerp(-1, horiz - 0.0001, unzip(ro * 2));
                    }
                    
                    ro = asin(ro);
                    float rs, rc;
                    sincos(ro, rs, rc);
                    float3 v = float3(vx * rc, rs, vz * rc);

                    float3 x_0;

                    if (x.y > atmosphere_radius - 1) {
                        float2 dis;
                        if (!X_Up(x, v, dis)) return 0;
                        x_0 = x + dis.y * v;
                        x = x + dis.x * v;
                    }
                    else {
                        X_0(x, v, x_0);
                    }

                    float3 res = Scatter(x, x_0, v, s, 32, _RenderGround);// _SunLuminance;
                    return res * 1e5 * _Multiplier;
                }
            ENDCG
        }

        
        Pass // Shading
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #define T T_TAB
                #include "../Includes/Atmo/Atmo.hlsl"

                sampler2D _DepthTex;
                sampler2D _MainTex;

                int _RenderGround;
                int _ApplyAtmoFog;
                float4x4 _P_Inv, _V_Inv;

                float4 GetWorldPositionFromDepthValue(float2 uv, float linearDepth)
                {
                    float camPosZ = _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * linearDepth;
                    float height = 2 * camPosZ / unity_CameraProjection._m11;
                    float width = _ScreenParams.x / _ScreenParams.y * height;

                    float camPosX = width * uv.x - width / 2;
                    float camPosY = height * uv.y - height / 2;
                    float4 camPos = float4(camPosX, camPosY, camPosZ, 1.0);
                    return mul(unity_CameraToWorld, camPos);
                }

                float4 frag(v2f i) : SV_Target
                {
                    float3 s = normalize(_SunDir);
                    float3 x = _WorldSpaceCameraPos;
                    x.y += planet_radius;
                    float depth = tex2Dlod(_DepthTex, float4(i.uv, 0, 0)).x;
                    bool sky_occ = depth != 0;
                    depth = LinearEyeDepth(depth);
                    float4 dispatch_dir = mul(_P_Inv, float4(i.uv * 2 - 1, 1, 1));
                    dispatch_dir /= dispatch_dir.w;
                    float dotCV = -normalize(dispatch_dir.xyz).z;
                    dispatch_dir = mul(_V_Inv, float4(dispatch_dir.xyz, 0));
                    float3 v = normalize(dispatch_dir.xyz);
                    float3 dir = normalize(mul(_V_Inv, float4(0, 0, -1, 0)));
                    float dotvc = -dot(normalize(_V_Inv._m02_m12_m22), v);
                    depth /= dotvc;

                    float4 sceneColor = tex2Dlod(_MainTex, float4(i.uv, 0, 0));

                    float4 output;
                    if (_ApplyAtmoFog) {
                        output = float4(lerp(ScatterTable(x, v, s, _RenderGround) * _SunLuminance, Scatter(x, v, depth, s), sky_occ ? 1 - smoothstep(0.9, 1, depth / _MaxDepth) : 0)
                                + (sky_occ ? sceneColor.xyz : 0) * T(x, x + depth * v), sceneColor.a);
                    }
                    else {
                        output = float4(sky_occ ? sceneColor.xyz : ScatterTable(x, v, s, _RenderGround) * _SunLuminance, sceneColor.a);
                    }

                    uint2 id = i.vertex.xy;
                    int k[16] = { 15,7,13,5,3,11,1,9,12,4,14,6,0,8,2,10 };
                    int index = id.x % 4 + id.y % 4 * 4;
                    float noise = hash12(id);

                    if (noise * 16 > k[index]) {
                        output.xyz += 0.2/255. * output;
                    }

                    return output;
                }
            ENDCG
        }
        
        Pass // Skybox
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #define T T_TAB
                #include "../Includes/Atmo/Atmo.hlsl"
                #include "../Includes/Atmo/CloudMarching.hlsl"

                int _RenderCloudToSkybox;
                int _Slice;

                fixed3 frag(v2f i) : SV_Target
                {
                    float3 s = normalize(_SunDir);
                    float3 x = _WorldSpaceCameraPos;
                    x.y += planet_radius;
                    float3 v = 0;

                    if (_Slice == 0) {
                        v = normalize(float3(1, lerp(1, -1, i.uv.y), lerp(1, -1, i.uv.x)));
                    }
                    else if (_Slice == 1) {
                        v = normalize(float3(-1, lerp(1, -1, i.uv.y), lerp(-1, 1, i.uv.x)));
                    }
                    else if (_Slice == 2) {
                        v = normalize(float3(lerp(-1, 1, i.uv.x), 1, lerp(-1, 1, i.uv.y)));
                    }
                    else if (_Slice == 3) {
                        v = normalize(float3(lerp(-1, 1, i.uv.x), -1, lerp(1, -1, i.uv.y)));
                    }
                    else if (_Slice == 4) {
                        v = normalize(float3(lerp(-1, 1, i.uv.x), lerp(1, -1, i.uv.y), 1));
                    }
                    else if (_Slice == 5) {
                        v = normalize(float3(lerp(1, -1, i.uv.x), lerp(1, -1, i.uv.y), -1));
                    }

                    float3 res = SkyBox(x, v, s) * _SunLuminance;

                    if (_RenderCloudToSkybox) {

                        RandSeed(i.vertex.xy);

                        float cloud_dis;
                        float av_occ;
                        _Quality = float4(80, 8, 64, 0);
                        float4 marching_res = CloudRender(x, v, cloud_dis, av_occ);

                        float3 cloud = 0;

                        if (cloud_dis != 0) {
                            // cloud shading
                            float3 present_point = x + v * cloud_dis;
                            float3 sunLight = Sunlight(present_point, s);
                            float3 ambd = Tu_L(x, s) * _SunLuminance * 0.1;
                            float3 sky = SkyBox(present_point, normalize(present_point), s) * _SunLuminance;
                            float3 ambu = sky * (1 - marching_res.z) * 3;
                            float3 amb = (lerp(ambd, ambu, marching_res.z * 0.7 + 0.3) + sunLight * 0.5 * marching_res.z) * 4;
                            cloud = (marching_res.x * sunLight + marching_res.y * amb) * _Brightness;

                            // apply atmo fog to cloud
                            float3 fog = res * av_occ;
                            float3 trans = T(x, present_point);
                            trans = smoothstep(0.1, 0.97, trans);
                            cloud = cloud * trans + fog * (1 - trans) * (1 - marching_res.a);
                        }


                        bool hitGround = IntersectSphere(x, v, float3(0, 0, 0), planet_radius) > 0;

                        if (!hitGround) {
                            res.xyz += T_tab_fetch(x, v) * (1 - smoothstep(-0.05, 0, dot(normalize(x), s))) * Space(v) * _Brightness * 50;

                            float3 coef = (numericalMieFit(dot(s, v)) + 0.25) * _Brightness;

                            float4 highCloud = HighCloud(x, v);
                            res.xyz += coef * Sunlight(highCloud.yzw, s) * T(x, highCloud.yzw) * highCloud.x;

                            float4 flowCloud = FlowCloud(x, v);
                            res.xyz += 2 * coef * Sunlight(flowCloud.yzw, s) * T(x, flowCloud.yzw) * flowCloud.x;
                        }

                        res = lerp(cloud, res, marching_res.a);
                    }

                    return res;
                }
            ENDCG
        }

        
        Pass // Apply fog
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #define T T_TAB
                #include "../Includes/Atmo/Atmo.hlsl"
                #include "../Includes/Terrain.hlsl"

                sampler2D _DepthTex;
                sampler2D _MainTex;
                samplerCUBE _SkyTex;

                int _RenderGround;
                int _ApplyAtmoFog;
                float4x4 _P_Inv, _V_Inv;

                float4 GetWorldPositionFromDepthValue(float2 uv, float linearDepth)
                {
                    float camPosZ = _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * linearDepth;
                    float height = 2 * camPosZ / unity_CameraProjection._m11;
                    float width = _ScreenParams.x / _ScreenParams.y * height;

                    float camPosX = width * uv.x - width / 2;
                    float camPosY = height * uv.y - height / 2;
                    float4 camPos = float4(camPosX, camPosY, camPosZ, 1.0);
                    return mul(unity_CameraToWorld, camPos);
                }

                float Trans(float a, float b, float d, float den, float att) {
                    float k = (b - a) / d;
                    float ka = -att * k;
                    float kb = -att * a;

                    float inter = (exp(ka * d + kb) - exp(kb)) / ka;

                    return exp(-den * inter * sqrt(1 + k * k));
                }

                float HeightFog(float h) {
                    return 0.001 * exp(-0.04 * h);
                }

                int _Clock;
                uint sampleIndex = 0;
                float hash(float n)
                {
                    return frac(sin(n) * 43758.5453);
                }

                void RandSeed(uint2 seed) {
                    sampleIndex = (uint)_Clock % 1024 + hash((seed.x % 64 + seed.y % 64 * 64) / 64./64.) * 64*64;
                }

                float Rand()
                {
                    return Roberts1(sampleIndex++);
                }

                float Rand2()
                {
                    return Roberts2(sampleIndex++);
                }

                float4 frag(v2f i) : SV_Target
                {
                    RandSeed(i.vertex.xy);
                    float3 s = normalize(_SunDir);
                    float3 x = _WorldSpaceCameraPos;
                    x.y += planet_radius;
                    float depth = tex2Dlod(_DepthTex, float4(i.uv, 0, 0)).x;
                    bool sky_occ = depth != 0;
                    depth = LinearEyeDepth(depth);
                    float4 dispatch_dir = mul(_P_Inv, float4(i.uv * 2 - 1, 1, 1));
                    dispatch_dir /= dispatch_dir.w;
                    float dotCV = -normalize(dispatch_dir.xyz).z;
                    dispatch_dir = mul(_V_Inv, float4(dispatch_dir.xyz, 0));
                    float3 v = normalize(dispatch_dir.xyz);
                    float3 dir = normalize(mul(_V_Inv, float4(0, 0, -1, 0)));
                    float dotvc = -dot(normalize(_V_Inv._m02_m12_m22), v);
                    depth /= dotvc;
                    
                    float3 cpos = _WorldSpaceCameraPos;
                    float3 wpos = cpos + v * depth;
                    
                    int spp = 32;
                    float max_dis = min(depth, 4000);
                    float bias = Rand();
                    float fogTrans = 1;
                    float directLight = 0;
                    float ambientLight = 0;
                    float stepLength = max_dis / spp;

                    float3 s_t = normalize(cross(float3(0, -1, 0), s));
                    if (s_t.x < 0) s_t = -s_t;
                    float3 s_bt = cross(s, s_t);

                    for (int step = 0; step < spp; step++)
                    {
                        float nd = max_dis * (float)(step + bias) / spp;
                        float3 lp = cpos + v * nd;

                        float2 rnd = Rand2();
                        rnd.x *= (2 * 3.14159265359);
                        float2 offset; sincos(rnd.x, offset.x, offset.y);
                        offset *= rnd.y * 20;

                        float scatter = HeightFog(lp.y) * (1 - nd / 4000);

                        scatter = exp(-scatter * stepLength);
                        fogTrans *= scatter;
                        scatter = 1 - scatter;

                        float response = fogTrans * scatter;

                        lp += s_t * offset.x + s_bt * offset.y;

                        directLight += response * (TerrainShadow(lp) * 0.95 + 0.05);
                        ambientLight += response;
                    }

                    float4 sceneColor = tex2Dlod(_MainTex, float4(i.uv, 0, 0));

                    float phase = GetPhase(-0.5, dot(v, _SunDir));
                    float3 fogColor = directLight * Sunlight(x, _SunDir) * phase * 0.7 + ambientLight * texCUBElod(_SkyTex, float4(0, 1, 0, 0));

                    float3 in_scatter = fogColor + (sky_occ ? lerp(ScatterTable(x, v, s, _RenderGround) * _SunLuminance, Scatter(x, v, depth, s), 1 - smoothstep(0.9, 1, depth / _MaxDepth)) : 0);
                    float3 trans = fogTrans * (sky_occ ? T(x, x + depth * v) : 1);

                    float4 output = float4(in_scatter + trans * sceneColor.xyz, sceneColor.a);

                    uint2 id = i.vertex.xy;
                    int k[16] = { 15,7,13,5,3,11,1,9,12,4,14,6,0,8,2,10 };
                    int index = id.x % 4 + id.y % 4 * 4;
                    float noise = hash12(id);

                    if (noise * 16 > k[index]) {
                        output.xyz += 0.2/255.;
                    }
                    return output;
                }
            ENDCG
        }
    }
}
