Shader "Hidden/IBL"
{
    Properties
    {
        _MainTex ("Texture", Cube) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Blend DstAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/Light.hlsl"
            #include "../Includes/PBS.hlsl"

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

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            TextureCube _MainTex; SamplerState sampler_linear_clamp;

            Texture2D _DepthTex, _BaseColorTex, _SpecTex, _NormalTex, _AOTex, _RayTracedLocalShadowMask;
            SamplerState sampler_point_clamp;

            float4 GetWorldPositionFromDepthValue(float2 uv, float linearDepth)
            {
                float camPosZ = _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * linearDepth;
                float height = 2 * camPosZ / unity_CameraProjection._m11;
                float width = _ScreenParams.x / _ScreenParams.y * height;

                float camPosX = width * uv.x - width / 2;
                float camPosY = height * uv.y - height / 2;
                float4 camPos = float4(camPosX, camPosY, camPosZ, 1.0);
                return mul(unity_CameraToWorld, camPos);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
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

                SurfaceInfo info = (SurfaceInfo)0;
                info = DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _SpecTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                                        0,
                                        _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0));

                float3 skyIrradiance = _MainTex.SampleLevel(sampler_linear_clamp, info.normal, 0);

                float3 res = PBS(PBS_SS_DIFFUSE, info, info.normal, skyIrradiance, view);

                return float4(res * 2, 1);
            }
            ENDCG
        }
    }
}
