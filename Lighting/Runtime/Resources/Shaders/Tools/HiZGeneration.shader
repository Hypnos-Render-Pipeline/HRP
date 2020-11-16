Shader "Hidden/HiZGeneration"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Texture2D<float> _HiZDepth;
            int _MipLevel;

            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }

            float frag(float4 vertex : SV_POSITION) : SV_Target
            {
                int2 id = int2(vertex.xy);
                int2 offset = int2(1, 0);
                int mip = _MipLevel;

                uint2 wh; int levels;
                _HiZDepth.GetDimensions(mip, wh.x, wh.y, levels);

                const float2 size = (float2)wh / (wh / 2);

                float2 o = id * size;
                int2 d = abs(frac(o) - 0.5) > 0.4999 ? o : floor(o);
                const float2 u = o + size;
                
                float maxD = 0;
                for (int i = d.x; i <= u.x; i++)
                {
                    for (int j = d.y; j <= u.y; j++)
                    {
                        maxD = max(maxD, _HiZDepth.mips[mip][int2(i, j)]);
                    }
                }

                return maxD;

                //int2 id = int2(vertex.xy) * 2;
                //int2 offset = int2(1, 0);
                //int mip = _MipLevel;

                //float4 ds = float4(_HiZDepth.mips[mip][id],
                //    _HiZDepth.mips[mip][id + offset],
                //    _HiZDepth.mips[mip][id + offset.yx],
                //    _HiZDepth.mips[mip][id + offset.xx]);

                //return max(max(ds.x, ds.y), max(ds.z, ds.w));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _HiZDepth;
            int _MipLevel;

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

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                return tex2Dlod(_HiZDepth, float4(i.uv, 0, _MipLevel + 1)) - tex2Dlod(_HiZDepth, float4(i.uv, 0, _MipLevel)) < 0 ? 1 : 0.5;
            }

            ENDCG
        }
    }
}
