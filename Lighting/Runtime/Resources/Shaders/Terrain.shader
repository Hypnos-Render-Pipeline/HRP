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
            #include "./Includes/Terrain.hlsl"

            Texture2D _LodTex;
            float3 _TileLB_tileSize;
            Buffer<float2> _TilePos;
            Buffer<uint> _TileIndex;
            float4 _TerrainCenter;
            float4x4 _V_Inv;

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

                float3 normal = TerrainNormal(wpos);

                float4 place_holder;
                Encode2GBuffer(0.05, 0, 1, 0, normal, 0, normal, 0, target0, target1, target2, place_holder, target3);
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
                o.wpos.y = TerrainHeight(o.wpos.xz);
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
                v.wpos.y = TerrainHeight(v.wpos.xz);
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
                o.wpos.y = TerrainHeight(o.wpos.xz);
                o.vertex = UnityWorldToClipPos(float4(o.wpos, 1));
                return o;
            }

            ENDCG
        }

        
        Pass
        {
            CGPROGRAM

            #pragma vertex shadow_vert
            #pragma fragment shadow_frag

            struct appdata_shadow
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct v2f_shadow
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f_shadow shadow_vert(appdata_shadow v)
            {
                v2f_shadow o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            int _Clock;
            uint sampleIndex = 0;
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }
            float Roberts1_(uint n) {
                const float g = 1.6180339887498948482;
                const float a = 1.0 / g;
                return frac(0.5 + a * n);
            }
            float2 Roberts2_(uint n) {
                const float g = 1.32471795724474602596;
                const float2 a = float2(1.0 / g, 1.0 / (g * g));
                return  frac(0.5 + a * n);
            }
            void RandSeed(uint2 seed) {
                sampleIndex = (uint)_Clock % 1024 + hash((seed.x % 64 + seed.y % 64 * 64) / 64. / 64.) * 64 * 64;
            }

            float Rand()
            {
                return Roberts1_(sampleIndex++);
            }

            float3 shadow_frag(v2f_shadow i) : SV_TARGET{
                RandSeed(i.vertex.xy);

                float2 uv;
                float4x4 mat;
                int spp = 32;
                if (all(i.uv < 0.5)) {
                    spp = 128;
                    uv = i.uv * 2;
                    mat = _TerrainShadowMatrix0;
                }
                else if (i.uv.y < 0.5) {
                    spp = 64;
                    uv = (i.uv - float2(0.5, 0)) * 2;
                    mat = _TerrainShadowMatrix1;
                }
                else if (i.uv.x < 0.5) {
                    spp = 32;
                    uv = (i.uv - float2(0, 0.5)) * 2;
                    mat = _TerrainShadowMatrix2;
                }
                else {
                    spp = 16;
                    uv = (i.uv - 0.5) * 2;
                    mat = _TerrainShadowMatrix3;
                }

                float3 st = mul(mat, float4(uv, 0, 1));
                float3 dir = mat._m02_m12_m22;
                float dis = length(dir);
                dir /= dis;

                for (int i = 0; i < spp; i++)
                {
                    float z = float(i + Rand()) / spp;
                    float d = dis * z;
                    float3 p = st + dir * d;
                    if (TerrainHeight(p.xz) > p.y + 1) {
                        return z;
                    }
                }

                return 1.1;
            }

            ENDCG
        }
    }
}
