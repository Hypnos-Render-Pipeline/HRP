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

                    return L2(x, s) * Fms(x, s);
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
                float _Multiplier;

                int _RenderGround;

                fixed3 frag(v2f i) : SV_Target
                {
                    float3 s = normalize(_SunDir);
                    float3 x = float3(0, planet_radius + max(95, _WorldSpaceCameraPos.y), 0);

                    float phi = atan(s.z / s.x) + (s.x > 0 ? (s.z < 0 ? 2 * pi : 0) : pi);
                    phi = s.x == 0 ? (s.z > 0 ? pi / 2 : -pi / 2) : phi;
                    phi += i.uv.x * pi;

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
                            ro = lerp(horiz + 0.0001, ahoriz - 0.0001, ro * 2);
                        }
                        else
                            ro = lerp(horiz + 0.0001, 1, pow(ro * 2, 2));
                    }
                    else {
                        float horiz = length(x);
                        horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;
                        ro = lerp(-1, horiz - 0.0001, pow(ro * 2, 0.5));
                    }

                    float vy = ro / sqrt(max(0, 1 - ro * ro));
                    float3 v = normalize(float3(vx, vy, vz));

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

                    float3 res = Scatter(x, x_0, v, s, 32, _RenderGround);// *_SunLuminance;
                    return res * _Multiplier;
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
                    float3 x = float3(0, planet_radius + max(95, _WorldSpaceCameraPos.y), 0);
                    float depth = tex2Dlod(_DepthTex, float4(i.uv, 0, 0)).x;
                    bool sky_occ = depth != 0;
                    depth = LinearEyeDepth(depth);
                    float4 dispatch_dir = mul(_P_Inv, float4(i.uv * 2 - 1, 1, 1));
                    dispatch_dir /= dispatch_dir.w;
                    float dotCV = -normalize(dispatch_dir.xyz).z;
                    dispatch_dir = mul(_V_Inv, float4(dispatch_dir.xyz, 0));
                    float3 v = normalize(dispatch_dir.xyz);
                    float3 dir = normalize(mul(_V_Inv, float4(0, 0, -1, 0)));
                    
                    //float3 x_0;
                    //X_0(x, v, x_0);
                    //float skyDepth = distance(x, x_0);
                    //depth = lerp(depth, skyDepth, smoothstep(0.9, 1, depth / LinearEyeDepth(0)));

                    float4 sceneColor = tex2Dlod(_MainTex, float4(i.uv, 0, 0));

                    float4 output = float4(lerp(ScatterTable(x, v, s, _RenderGround) * _SunLuminance, Scatter(x, v, depth, s), sky_occ ? 1 - smoothstep(0.9, 1, depth / _MaxDepth) : 0)
                                    +(sky_occ ? sceneColor.xyz : 0) * T(x, x + depth * v), sceneColor.a);
//output = float4(Scatter(x, v, _MaxDepth, s),0);
//output = float4(ScatterTable(x, v, s, _RenderGround) * _SunLuminance, 0);
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
        
        Pass // Skybox
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #define T T_TAB
                #include "../Includes/Atmo/Atmo.hlsl"

                int _RenderGround;
                int _Slice;

                fixed3 frag(v2f i) : SV_Target
                {
                    float3 s = normalize(_SunDir);
                    float3 x = float3(0, planet_radius + max(95, _WorldSpaceCameraPos.y), 0);
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

                    float3 x_0;
                    X_0(x, v, x_0);

                    float3 res = SkyBox(x, v, s) * _SunLuminance;

                    uint2 id = i.vertex.xy;
                    int k[16] = { 15,7,13,5,3,11,1,9,12,4,14,6,0,8,2,10 };
                    int index = id.x % 4 + id.y % 4 * 4;
                    float noise = hash12(id);
                    if (noise * 16 > k[index]) {
                        res += 0.1 / 255.;
                    }
                    return res;
                }
            ENDCG
        }
    }
}
