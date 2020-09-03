Shader "Hidden/ClearAlpha"
{
    Properties { }
    SubShader
    {
        Pass
        {
            ColorMask A
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float _Alpha;

            float4 frag (v2f i) : SV_Target
            {
                return float4(0, 0, 0, _Alpha);
            }
            ENDCG
        }
    }
}
