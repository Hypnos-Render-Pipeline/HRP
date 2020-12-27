Shader "HRP/Terrain"
{
    Properties
    {
        _LodTex("Lod Texture", 2D) = "Black" {}
    }
        SubShader
        {
            CGINCLUDE

                #include "UnityCG.cginc"
                #include "./Includes/PBS.hlsl"
                #include "./Includes/GBuffer.hlsl"

                Texture2D _LodTex;
                float3 _TileLB_tileSize;
                Buffer<float2> _TilePos;
                Buffer<uint> _TileIndex;
                float4 _TerrainCenter;
                float4x4 _V_Inv;
                float2 _HeightRange;

                sampler2D _TerrainHeight;

                float H(float2 xz) {
                    return lerp(_HeightRange.x, _HeightRange.y, tex2Dlod(_TerrainHeight, float4(xz / 1024, 0, 0)).x);
                }

                float3 Normal(float3 wpos) {
                    float of = 1.1;
                    float3 px = wpos + float3(of, 0, 0);
                    px.y = H(px.xz);
                    float3 py = wpos + float3(0, 0, of);
                    py.y = H(py.xz);

                    float3 px_ = wpos - float3(of, 0, 0);
                    px_.y = H(px_.xz);
                    float3 py_ = wpos - float3(0, 0, of);
                    py_.y = H(py_.xz);
                    return normalize(cross(normalize(py_ - py), normalize(px_ - px)));
                }

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float2 uv2 : TEXCOORD1;
                    uint instanceID : SV_InstanceID;
                };
                struct v2t
                {
                    float3 wpos : TEXCOORD0;
                };
                struct t2f
                {
                    float4 vertex : SV_POSITION;
                    float3 wpos : TEXCOORD0;
                };
                struct Falcor {
                    float edge[3] : SV_TessFactor;
                    float inside : SV_InsideTessFactor;
                };

                void frag(t2f i, out fixed4 target0 : SV_Target0, out fixed4 target1 : SV_Target1, out fixed4 target2 : SV_Target2, out fixed4 target3 : SV_Target3)
                {
                    float3 wpos = i.wpos;

                    float3 normal = Normal(wpos);

                    float4 place_holder;
                    Encode2GBuffer(0.25, 0.6, 0, normal, 0, normal, 0, target0, target1, target2, place_holder, target3);
                }

            ENDCG



            Pass
            {
                CGPROGRAM

                #pragma vertex vert
                #pragma fragment frag
                #pragma hull hs
                #pragma domain ds

                v2t vert(appdata v)
                {
                    v2t o;

                    float2 offset = _TilePos[_TileIndex[v.instanceID]];
                    float3 wpos = v.vertex + float3(offset.x, 0, offset.y) + _TerrainCenter;
                    int2 id = (wpos.xz - (v.uv - 0.5) * _TileLB_tileSize.z * 0.1 - _TileLB_tileSize.xy) / _TileLB_tileSize.z;

                    float4 lods = _LodTex[id];

                    float2 lod_ = lerp(lods.xz, lods.yw, v.uv.x);
                    float lod = lerp(lod_.x, lod_.y, v.uv.y);

                    o.wpos = wpos + float3(v.uv2.x, 0, v.uv2.y) * lod;
                    o.wpos.y = H(o.wpos.xz);
                    return o;
                }

                Falcor hsconst(InputPatch<v2t, 3> v) {

                    float3 camPos = _V_Inv._m03_m13_m23;

                    float3 dis = float3(distance((v[0].wpos + v[1].wpos) / 2, camPos), distance((v[1].wpos + v[2].wpos) / 2, camPos), distance((v[2].wpos + v[0].wpos) / 2, camPos));

                    float3 tf = clamp(200 / dis, 1, 16);
                    tf = dis == 0 ? 0 : tf;
                    //tf = 1;
                    Falcor o;
                    o.edge[2] = tf.x;
                    o.edge[0] = tf.y;
                    o.edge[1] = tf.z;
                    o.inside = max(tf.x, max(tf.y, tf.z));
                    return o;
                }

                [UNITY_domain("tri")]
                [UNITY_partitioning("fractional_odd")]
                [UNITY_outputtopology("triangle_cw")]
                [UNITY_patchconstantfunc("hsconst")]
                [UNITY_outputcontrolpoints(3)]
                v2t hs(InputPatch<v2t, 3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }

                [UNITY_domain("tri")]
                t2f ds(Falcor tessFactors, const OutputPatch<v2t, 3> vi, float3 bary : SV_DomainLocation) {
                    t2f v;
                    v.wpos = vi[0].wpos * bary.x + vi[1].wpos * bary.y + vi[2].wpos * bary.z;
                    v.wpos.y = H(v.wpos.xz);
                    v.vertex = UnityWorldToClipPos(float4(v.wpos, 1));
                    return v;
                }

                ENDCG
            }

            Pass
            {
                CGPROGRAM

                #pragma vertex vert
                #pragma fragment frag

                t2f vert(appdata v)
                {
                    t2f o;

                    float2 offset = _TilePos[_TileIndex[v.instanceID]];
                    float3 wpos = v.vertex + float3(offset.x, 0, offset.y) + _TerrainCenter;
                    int2 id = (wpos.xz - (v.uv - 0.5) * _TileLB_tileSize.z * 0.1 - _TileLB_tileSize.xy) / _TileLB_tileSize.z;

                    float4 lods = _LodTex[id];

                    float2 lod_ = lerp(lods.xz, lods.yw, v.uv.x);
                    float lod = lerp(lod_.x, lod_.y, v.uv.y);

                    o.wpos = wpos + float3(v.uv2.x, 0, v.uv2.y) * lod;
                    o.wpos.y = H(o.wpos.xz);
                    o.vertex = UnityWorldToClipPos(float4(o.wpos, 1));
                    return o;
                }

                ENDCG
            }
        }
}
