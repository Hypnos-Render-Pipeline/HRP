Shader "Hidden/LightMesh"
{
    Properties
    {
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

            float3 _LightColor;
            sampler2D _LightTex;
            int _Disc;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_Disc) if (distance(i.uv, 0.5) > 0.5) discard;
                return float4(_LightColor * tex2D(_LightTex, i.uv).rgb, 0);
            }
            ENDCG
        }
    }
}
