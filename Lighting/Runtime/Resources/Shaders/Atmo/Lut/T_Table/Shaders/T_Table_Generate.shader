Shader "Atmo/T_Generate"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#include "UnityCustomRenderTexture.cginc"
            #include "../../../../Includes/Atmo/Atmo.hlsl"


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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
			{ 
				float2 corrected_uv = float2(i.vertex.xy - 0.5) / float2(_CustomRenderTextureWidth-1, _CustomRenderTextureHeight-1);
				corrected_uv.y = unzip(corrected_uv.y);
				if (corrected_uv.y < 0.5) {
					
					float3 x = GetPoint(corrected_uv.x);

					float horiz = length(x);
					horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

					corrected_uv.y = lerp(horiz + 0.0001, 1, corrected_uv.y * 2);


					float3 dir = GetDir_11(corrected_uv.y);

					float3 x_0;
					bool isGround = X_0(x, dir, x_0);

					float3 res = T(x, x_0, 1000);

					return min(float4(res, isGround), 1);
				}
				else {
					corrected_uv.y = (corrected_uv.y - 0.5);
					corrected_uv.x = 1 - corrected_uv.x;

					float3 x = GetPoint(corrected_uv.x);

					float horiz = length(x);
					horiz = -sqrt(horiz * horiz - planet_radius * planet_radius) / horiz;

					corrected_uv.y = lerp(-1, horiz - 0.0001, corrected_uv.y * 2);


					float3 dir = GetDir_11(corrected_uv.y);

					float3 x_0;
					bool isGround = X_0(x, dir, x_0);

					float3 res = T(x, x_0, 1000);

					return min(float4(res, isGround), 1);
				}
            }
            ENDCG
        }
    }
}
