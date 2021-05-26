Shader "Hidden/ACES"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 aces_tonemap(float3 color) {
                float3x3 m1 = float3x3(
                    0.59719, 0.35458, 0.04823,
                    0.07600, 0.90834, 0.01566,
                    0.02840, 0.13383, 0.83777
                    );
                float3x3 m2 = float3x3(
                    1.60475, -0.53108, -0.07367,
                    -0.10208, 1.10813, -0.00605,
                    -0.00327, -0.07276, 1.07602
                    );
                float3 v = mul(m1, color);
                float3 a = v * (v + 0.0245786) - 0.000090537;
                float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
                return clamp(mul(m2, (a / b)), 0.0, 1.0);
            }




            float3 Gamma(float3 color) {
                return pow(color, 1.0f / 2.2f);
            }
            float3 unity_to_ACES(float3 x)
            {
                float3x3 sRGB_2_AP0 = {
                    {0.4397010, 0.3829780, 0.1773350},
                    {0.0897923, 0.8134230, 0.0967616},
                    {0.0175440, 0.1115440, 0.8707040}
                };
                x = mul(sRGB_2_AP0, x);
                return x;
            };
            float3 ACES_to_ACEScg(float3 x)
            {
                float3x3 AP0_2_AP1_MAT = {
                    {1.4514393161, -0.2365107469, -0.2149285693},
                    {-0.0765537734,  1.1762296998, -0.0996759264},
                    {0.0083161484, -0.0060324498,  0.9977163014}
                };
                return mul(AP0_2_AP1_MAT, x);
            };
            float3 XYZ_2_xyY(float3 XYZ)
            {
                float divisor = max(dot(XYZ, 1), 1e-4);
                return float3(XYZ.x / divisor, XYZ.y / divisor, XYZ.y);
            };
            float3 xyY_2_XYZ(float3 xyY)
            {
                float m = xyY.z / max(xyY.y, 1e-4f);
                float3 XYZ = float3(xyY.x, xyY.z, (1.0f - xyY.x - xyY.y));
                XYZ.x *= m;
                XYZ.z *= m;
                return XYZ;
            };
            float3 darkSurround_to_dimSurround(float3 linearCV)
            {
                float3x3 AP1_2_XYZ_MAT = { {0.6624541811, 0.1340042065, 0.1561876870},
                    { 0.2722287168, 0.6740817658, 0.0536895174 },
                    { -0.0055746495, 0.0040607335, 1.0103391003 } };
                float3 XYZ = mul(AP1_2_XYZ_MAT, linearCV);

                float3 xyY = XYZ_2_xyY(XYZ);
                xyY.z = min(max(xyY.z, 0.0), 65504.0);
                xyY.z = pow(xyY.z, 0.9811f);
                XYZ = xyY_2_XYZ(xyY);

                float3x3 XYZ_2_AP1_MAT = {
                    {1.6410233797, -0.3248032942, -0.2364246952},
                    {-0.6636628587,  1.6153315917,  0.0167563477},
                    {0.0117218943, -0.0082844420,  0.9883948585}
                };
                return mul(XYZ_2_AP1_MAT, XYZ);
            };
            float3 ACES(float3 color) {

                float3x3 AP1_2_XYZ_MAT = { {0.6624541811, 0.1340042065, 0.1561876870},
                    { 0.2722287168, 0.6740817658, 0.0536895174 },
                    { -0.0055746495, 0.0040607335, 1.0103391003 } };

                float3 aces = unity_to_ACES(color);

                float3 AP1_RGB2Y = float3(0.272229, 0.674082, 0.0536895);

                float3 acescg = ACES_to_ACEScg(aces);
                float tmp = dot(acescg, AP1_RGB2Y);
                acescg = lerp(tmp, acescg, 0.96);
                const float a = 278.5085;
                const float b = 10.7772;
                const float c = 293.6045;
                const float d = 88.7122;
                const float e = 80.6889;
                float3 x = acescg;
                float3 rgbPost = (x * (x * a + b)) / (x * (x * c + d) + e);
                float3 linearCV = darkSurround_to_dimSurround(rgbPost);
                tmp = dot(linearCV, AP1_RGB2Y);
                linearCV = lerp(tmp, linearCV, 0.93);
                float3 XYZ = mul(AP1_2_XYZ_MAT, linearCV);
                float3x3 D60_2_D65_CAT = {
                    {0.98722400, -0.00611327, 0.0159533},
                    {-0.00759836,  1.00186000, 0.0053302},
                    {0.00307257, -0.00509595, 1.0816800}
                };
                XYZ = mul(D60_2_D65_CAT, XYZ);
                float3x3 XYZ_2_REC709_MAT = {
                    {3.2409699419, -1.5373831776, -0.4986107603},
                    {-0.9692436363,  1.8759675015,  0.0415550574},
                    {0.0556300797, -0.2039769589,  1.0569715142}
                };
                linearCV = mul(XYZ_2_REC709_MAT, XYZ);

                return Gamma(linearCV);
            }



            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
                return float4(aces_tonemap(col.xyz), col.a);
            }
            ENDCG
        }


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            sampler2D _MainTex, _History_Final_Result;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = lerp(tex2Dlod(_MainTex, float4(i.uv, 0, 0)), tex2Dlod(_History_Final_Result, float4(i.uv, 0, 0)), 0.9);
                return col;
            }
            ENDCG
        }
    }
}
