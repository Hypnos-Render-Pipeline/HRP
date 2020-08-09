Shader "Hidden/DebugBlit"
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


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D   _MainTex;
            float       _Multiplier;
            int         _Channel;
            float       _Aspect;

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);

                col.rgb *= _Multiplier;

                if (_Channel) {
                    return float4(col[_Channel - 1].xxx, 1);
                }

                float2 checkboard = i.uv * float2(_Aspect, 1);

                checkboard = int2(checkboard / 0.025f);

                float a = (checkboard.x % 2 + checkboard.y + 1) % 2 * 0.5 + 0.5;
                return float4(lerp(a, col.rgb, col.a), 1);
            }
            ENDCG
        }
    }
}
