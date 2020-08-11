Shader "HRP/Terrain"
{
    Properties
    {
        _LodTex("Lod Texture", 2D) = "Black" {}
        _TileLB_tileSize("TileLB_tileSize", Vector) = (0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            //CBUFFER_START(UnityPerMaterial)
            //CBUFFER_END
            
            Texture2D _LodTex;
            float3 _TileLB_tileSize;
            Buffer<float2> _TilePos;
            float4 _TerrainCenter;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                float2 offset = _TilePos[instanceID];
                float3 wpos = v.vertex + float3(offset.x, 0, offset.y) + _TerrainCenter;
                int2 id = (wpos.xz - (v.uv - 0.5) * _TileLB_tileSize.z - _TileLB_tileSize.xy) / _TileLB_tileSize.z;

                float4 lods = _LodTex[id];

                float2 lod_ = lerp(lods.xz, lods.yw, v.uv.x);
                float lod = lerp(lod_.x, lod_.y, v.uv.y);

                o.vertex = UnityObjectToClipPos(float4(wpos, 1) + float3(v.uv2.x, 0, v.uv2.y) * lod);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 1;
            }
            ENDCG
        }
    }
}
