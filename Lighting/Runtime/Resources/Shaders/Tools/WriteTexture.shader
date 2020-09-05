Shader "Hidden/WriteTexture" {
    Properties { }
    SubShader {
		CGINCLUDE
			float4 vert(float4 vertex : POSITION) : SV_POSITION {
				return UnityObjectToClipPos(vertex);
			}
		ENDCG

		Pass{
			name "Write 2D"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment write2D

				Buffer<int> _Data;

				int write2D(float4 vertex : SV_POSITION) : SV_Target 
				{
					int2 id = vertex.xy;
					int data = _Data[id.x + id.y * 256];
					return data;
				}
			ENDCG
		}
		Pass{
		name "Write 3D"
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment write3D

				Buffer<int> _Data;
				int _Slice;

				int write3D(float4 vertex : SV_POSITION) : SV_Target
				{
					int2 id = vertex.xy;
					int data = _Data[id.x + id.y * 128 + 128 * 128 * _Slice];
					return data;
				}
			ENDCG
		}
    }
}
