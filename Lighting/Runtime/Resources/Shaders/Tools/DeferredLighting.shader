Shader "Hidden/DeferredLighting"
{
    Properties { }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Includes/GBuffer.hlsl"

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

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _OtherTex;
            SamplerState sampler_point_clamp;

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos; 
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }
                float3 view = normalize(camPos - pos);

                float3 baseColor;
                float roughness; 
                float metallic;
                float3 normal;
                float3 emission;

                DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                baseColor, roughness, metallic, normal, emission);

                float3 res = lerp(pow(max(0, dot(normalize(view + float3(0, 1, 0)), normal)), lerp(2000, 10, roughness)) * lerp(10, 0.2, roughness), max(normal.y, 0) / 2, 0.96 - metallic) * baseColor;

                return float4(res + emission, 1);
            }
            ENDCG
        }
    }
}
