Shader "Hidden/CalculateMotion"
{
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            Texture2D _DepthTex;
            SamplerState sampler_point_clamp;

            float4x4 _VP_Inv_NoJitter;
            float4x4 _Last_VP_NoJitter;

            float2 frag (v2f i) : SV_Target
            {
                float depth = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0);
                float3 speed = 0;

                float4 vpoint = float4(i.uv * 2 - 1, depth, 1);

                float4 wpoint;
                wpoint = mul(_VP_Inv_NoJitter, vpoint); wpoint /= wpoint.w;
                wpoint.xyz -= speed;

                float4 lvpoint = mul(_Last_VP_NoJitter, wpoint);

                lvpoint /= lvpoint.w;
                lvpoint = (lvpoint + 1) * 0.5;

                return i.uv - lvpoint.xy;
            }
            ENDCG
        }
    }
}
