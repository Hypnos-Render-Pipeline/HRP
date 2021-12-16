Shader "Hidden/DebugBlit"
{
    Properties { }
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


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            Texture2D   _DebugTex; SamplerState linear_clamp_sampler;
            float       _Multiplier;
            int         _Channel;
            int         _Checkboard;
            float       _Aspect;
            int _Lod;

            float4 frag (v2f i) : SV_Target
            {
                float4 col = _DebugTex.SampleLevel(linear_clamp_sampler, i.uv, _Lod);
                col.rgb *= _Multiplier;

                if (_Channel) {
                    if (_Channel == 5)
                        return float4(col.xyz, 1);
                    return float4(col[_Channel - 1].xxx * _Multiplier, 1);
                }

                float2 checkboard = i.uv * float2(_Aspect, 1);

                checkboard = int2(checkboard / 0.025f);

                float a = (checkboard.x % 2 + checkboard.y + 1) % 2 * 0.5 + 0.5;
                return float4(lerp(_Checkboard ? a : 0, col.rgb, saturate(col.a)), 1);
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


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            TextureCube _DebugTex; SamplerState linear_clamp_sampler;
            float       _Multiplier;
            int         _Channel;
            int         _Checkboard;
            float       _Aspect;
            int _Lod;

            float4 frag(v2f i) : SV_Target
            {
                float pi = 3.14159265359;
                float rho = 1.5 * pi - i.uv.x * pi * 2;
                float phi = (i.uv.y * 2 - 1) * pi / 2;

                float3 v;
                sincos(rho, v.x, v.z);
                float ps, pc;
                sincos(phi, ps, pc);
                v.xz *= pc;
                v.y = ps;
                //return float4(v, 1);

                float4 col = _DebugTex.SampleLevel(linear_clamp_sampler, v, _Lod);
                col.rgb *= _Multiplier;

                if (_Channel) {
                    if (_Channel == 5)
                        return float4(col.xyz, 1);
                    return float4(col[_Channel - 1].xxx * _Multiplier, 1);
                }

                float2 checkboard = i.uv * float2(_Aspect, 1);

                checkboard = int2(checkboard / 0.025f);

                float a = (checkboard.x % 2 + checkboard.y + 1) % 2 * 0.5 + 0.5;
                return float4(lerp(_Checkboard ? a : 0, col.rgb, saturate(col.a)), 1);
            }
            ENDCG
        }
    }
}
