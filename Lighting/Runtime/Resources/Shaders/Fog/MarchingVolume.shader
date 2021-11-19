Shader "Hidden/MarchingVolume"
{
    Properties
    {
        _MainTex ("Texture", 3D) = "black" {}
    }
    SubShader
    {
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

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

            Texture3D _MainTex; SamplerState bilinear_clamp_sampler;
            float4 _FogVolumeSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 res = 0;
                for (int step = 0; step < _FogVolumeSize.z; step++)
                {
                    res += _MainTex.SampleLevel(bilinear_clamp_sampler, float3(i.uv, (step + 0.5) / _FogVolumeSize.z), 0).x;
                }                
                return res / 10;
            }
            ENDCG
        }
    }
}
