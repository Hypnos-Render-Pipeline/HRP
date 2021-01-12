Shader "Hidden/FilterSkybox"
{
    Properties
    {
        _MainTex ("Texture", CUBE) = "white" {}
    }
        SubShader
    {

        Pass // Filter
        {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                samplerCUBE _MainTex;

                int _Slice;

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
                float2 Roberts2(uint n) {
                    const float g = 1.32471795724474602596;
                    const float2 a = float2(1.0 / g, 1.0 / (g * g));
                    return  frac(0.5 + a * n);
                }
                float3 UV2Dir(int slice, float2 uv) {
                    float3 v = 0;
                    if (slice == 0) {
                        v = normalize(float3(1, lerp(1, -1, uv.y), lerp(1, -1, uv.x)));
                    }
                    else if (slice == 1) {
                        v = normalize(float3(-1, lerp(1, -1, uv.y), lerp(-1, 1, uv.x)));
                    }
                    else if (slice == 2) {
                        v = normalize(float3(lerp(-1, 1, uv.x), 1, lerp(-1, 1, uv.y)));
                    }
                    else if (slice == 3) {
                        v = normalize(float3(lerp(-1, 1, uv.x), -1, lerp(1, -1, uv.y)));
                    }
                    else if (slice == 4) {
                        v = normalize(float3(lerp(-1, 1, uv.x), lerp(1, -1, uv.y), 1));
                    }
                    else if (slice == 5) {
                        v = normalize(float3(lerp(1, -1, uv.x), lerp(1, -1, uv.y), -1));
                    }
                    return v;
                }

                float3 UV2Dir(float2 uv) {
                    return UV2Dir(_Slice, uv);
                }

                int _Clock;

                fixed3 frag(v2f i) : SV_Target
                {
                    float3 v = UV2Dir(i.uv);

                    float3 res = 0;
                    float w = 0;
                    for (int s = 0; s < 6; s++)
                    {
                        for (float x = 0.125; x < 1; x += 0.25)
                        {
                            for (float y = 0.125; y < 1; y += 0.25)
                            {
                                float3 dir = UV2Dir(s, float2(x, y));
                                float w_ = max(0, dot(v, dir));
                                w_ = pow(w_, 1.2);
                                w += w_;
                                res += texCUBElod(_MainTex, float4(dir, 5)) * w_;
                            }
                        }
                    }
                    return res / w;
                }
            ENDCG
        }
    }
}
