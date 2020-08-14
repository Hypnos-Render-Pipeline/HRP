Shader "Hidden/GenerateLTCTexture"
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            int _Level;
            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                uint w, h, l;
                _MainTex.GetDimensions(0, w, h, l);
                uint w_, h_;
                _MainTex.GetDimensions(_Level, w_, h_, l);
                float2 wh = float2(w_, h_);

                int blurRadius = max(1, max(w, h) / 256);

                float4 color = 0;
                float sum = 0;
                for (int j = -blurRadius; j <= blurRadius; j++)
                {
                    for (int k = -blurRadius; k <= blurRadius; k++)
                    {
                        float wei = max(blurRadius + 0.1 - sqrt(j * j + k * k), 0);
                        color += wei * _MainTex.SampleLevel(sampler_MainTex, i.uv + float2(j, k) / wh, _Level);
                        sum += wei;
                    }
                }
                return color / sum;
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            int _Level;
            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2Dlod(_MainTex, float4(i.uv, 0, _Level));
                return col;
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState linear_clamp_sampler;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 SampleTex(float2 uv) {
                if (any(uv < 0) || any(uv > 1)) return 0;
                return float4(_MainTex.SampleLevel(linear_clamp_sampler, uv, 0).xyz, 1);
            }

            float4 frag (v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                uv = (uv - 0.5) * 2 + 0.5;

                uint w, h, l;
                _MainTex.GetDimensions(0, w, h, l);
                float2 wh = float2(w, h);

                float2 px = 1.0f / wh;
                float dis = length(max(max(-uv, uv - 1), 0)) * 2 + 0.2;

                uv = saturate(uv);
                float4 res = 0;

                for (float i = -dis; i <= dis; i += 0.05)
                {
                    for (float j = -dis; j <= dis; j += 0.05)
                    {
                        float2 ij = float2(i, j);
                        float2 uv_ = uv + ij;
                        if (uv_.x < 0) i += abs(uv_.x);
                        if (uv_.y < 0) j += abs(uv_.y);
                        if (uv_.y > 1) break;
                        if (dot(ij, ij) > dis * dis) continue;
                        float w = (dis * dis) - dot(ij, ij) + 0.0001;
                        res += SampleTex(uv_) * w;
                    }
                }

                return float4(res.rgb / res.a, 1);
            }
            ENDCG
        }
    }
}
