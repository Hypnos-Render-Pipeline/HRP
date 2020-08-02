Shader "Hidden/Wireframe"
{
    Properties
    {
        _Color("Color", color) = (0.996,0.992,0.533, 0.5)
        _WireWidth("WireWidth", Range(0, 0.005)) = 0.0008
    }
        SubShader
        {
            Pass
            {
                Blend SrcAlpha OneMinusSrcAlpha
                //Cull Off
                CGPROGRAM

                #pragma vertex vert
                #pragma geometry geom
                #pragma fragment frag
                #include "UnityCG.cginc"

                struct g2f
                {
                    float4 vertex: SV_POSITION;
                    float3 dist: TEXCOORD1;
                };

                float4 vert(float4 vertex: POSITION) : SV_POSITION
                {
                    return UnityObjectToClipPos(vertex);
                }

                [maxvertexcount(3)]
                void geom(triangle float4 IN[3] : SV_POSITION, inout TriangleStream<g2f> triStream)
                {
                    float2 p0 = IN[0].xy / IN[0].w;
                    float2 p1 = IN[1].xy / IN[1].w;
                    float2 p2 = IN[2].xy / IN[2].w;

                    float2 v0 = p2 - p1;
                    float2 v1 = p2 - p0;
                    float2 v2 = p1 - p0;

                    float area = abs(v1.x * v2.y - v1.y * v2.x);

                    g2f OUT;
                    OUT.vertex = IN[0];
                    OUT.dist = float3(area / length(v0), 0, 0);
                    triStream.Append(OUT);

                    OUT.vertex = IN[1];
                    OUT.dist = float3(0, area / length(v1), 0);
                    triStream.Append(OUT);

                    OUT.vertex = IN[2];
                    OUT.dist = float3(0, 0, area / length(v2));
                    triStream.Append(OUT);
                }

                float _WireWidth;
                float4 _Color;

                float4 frag(g2f i) : SV_Target
                {
                    fixed4 col_Wire;
                    float d = min(i.dist.x, min(i.dist.y, i.dist.z));
                    if (d > _WireWidth) discard;
                    return _Color;
                }
                ENDCG

            }
        }
}