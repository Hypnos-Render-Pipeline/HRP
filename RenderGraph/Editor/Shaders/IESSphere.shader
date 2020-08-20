Shader "Hidden/IESSphere"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_
            #pragma hull hs
            #pragma domain ds
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            samplerCUBE _IESCube;

            appdata vert_(appdata v)
            {
                appdata o;
                o.normal = v.normal;
                return o;
            }


            v2f vert(appdata v)
            {
                v2f o;
                float3 offset = v.normal * (texCUBElod(_IESCube, float4(normalize(v.normal), 0)).rrr * 0.96 + 0.04);
                o.vertex = UnityObjectToClipPos(float4(offset, 1));
                o.normal = v.normal;
                return o;
            }

            struct OutputPatchConstant {
                float edge[3]         : SV_TessFactor;
                float inside : SV_InsideTessFactor;
                float3 vTangent[4]    : TANGENT;
                float2 vUV[4]         : TEXCOORD;
                float3 vTanUCorner[4] : TANUCORNER;
                float3 vTanVCorner[4] : TANVCORNER;
                float4 vCWts          : TANWEIGHTS;
            };

            OutputPatchConstant hsconst() {
                OutputPatchConstant o = (OutputPatchConstant)0;
                float tes = 5;
                o.edge[0] = tes;
                o.edge[1] = tes;
                o.edge[2] = tes;
                o.inside = tes;
                return o;
            }

            [UNITY_domain("tri")]
            [UNITY_partitioning("fractional_odd")]
            [UNITY_outputtopology("triangle_cw")]
            [UNITY_patchconstantfunc("hsconst")]
            [UNITY_outputcontrolpoints(3)]
            appdata hs(InputPatch<appdata, 3> v, uint id : SV_OutputControlPointID) {
                return v[id];
            }

            [UNITY_domain("tri")]
            v2f ds(OutputPatchConstant tessFactors, const OutputPatch<appdata, 3> vi, float3 bary : SV_DomainLocation) {
                appdata v;

                v.normal = vi[0].normal * bary.x + vi[1].normal * bary.y + vi[2].normal * bary.z;

                return vert(v);
            }


            fixed3 frag(v2f i) : SV_Target
            {
                return lerp(float3(0.1,0.1,0.8), float3(1,0,0), texCUBElod(_IESCube, float4(normalize(i.normal), 0)).r);
            }
            ENDCG
        }
    }
}
