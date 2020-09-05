Shader "Atmo/S_table_0"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		T_table("T Table", 2D) = "white" {}
	}

	SubShader
	{
		Lighting Off
		Blend One Zero

		Pass
		{
			CGPROGRAM
			#include "UnityCustomRenderTexture.cginc"
			#pragma vertex CustomRenderTextureVertexShader
			#pragma fragment frag
			#pragma target 3.0


			float4      _Color;
			sampler2D   _Tex;
			
			#define T T_TAB
			#define CLAMP_COS
			#include "../../../../Includes/Atmo/Atmo.hlsl"

			float3 frag(v2f_customrendertexture IN) : COLOR
			{
				float2 uv_grid_size = 1 / float2(_CustomRenderTextureWidth, _CustomRenderTextureHeight);
				float4 corrected_uv;
				corrected_uv.xy = (IN.globalTexcoord.xy - uv_grid_size * 0.5) / (1 - uv_grid_size);
				corrected_uv.z = saturate(((uint)_CustomRenderTexture3DSlice % S_resolution.z) / float(S_resolution.z - 1));
				corrected_uv.w = saturate(((uint)_CustomRenderTexture3DSlice / S_resolution.z) / float(S_resolution.w - 1));
				corrected_uv = saturate(corrected_uv);

				float3 x, v, s;
				u_2_xvs(corrected_uv, x, v, s);

				float3 res = S_L(x, v, s, 100, 100);
				//if (any(isnan(res)))
				//	res = 0;
				return res;
			}
			ENDCG
		}
	}
}