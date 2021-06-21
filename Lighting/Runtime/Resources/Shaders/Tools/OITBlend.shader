Shader "Hidden/OITBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            int _ScreenWidth;
            struct OITOutput
            {
                float3 srcColor;
                uint alpha;
            };

            struct OITOutputList {
                uint4 zs;
                OITOutput datas[4];
            };

            StructuredBuffer<OITOutputList> _OITOutputList;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2Dlod(_MainTex, float4(i.uv, 0, 0));

                uint2 pixel_coord = i.vertex.xy;

                OITOutputList list = _OITOutputList[pixel_coord.x + _ScreenWidth * pixel_coord.y];


                float3 res = col.xyz;

                for (int id = 3; id >= 0; --id)
                {
                    uint ptr = list.zs[id];
                    OITOutput o = list.datas[ptr & 3];
                    float z = asfloat(ptr & 0xFFFFFFFC);

                    if (z == 0) continue;

                    uint ualpha = o.alpha;
                    float3 alpha = (uint3(ualpha >> 16, ualpha >> 8, ualpha) & 0xFF) / 255.0f;

                    res = lerp(res, o.srcColor, alpha);
                }

                //return col;
                return float4(res, col.a);
            }
            ENDCG
        }
    }
}
