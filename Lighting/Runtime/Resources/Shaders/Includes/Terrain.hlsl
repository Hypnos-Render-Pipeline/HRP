#ifndef TERRAIN_H_
#define TERRAIN_H_


float4x4 _TerrainShadowMatrix0, _TerrainShadowMatrix1, _TerrainShadowMatrix2, _TerrainShadowMatrix3;
float2 _TerrainHeightRange;

float2 _HeightRange;
sampler2D _TerrainHeight;
sampler2D _TerrainShadowMap;


float TerrainHeight(float2 xz) {
    return lerp(_HeightRange.x, _HeightRange.y, tex2Dlod(_TerrainHeight, float4(xz / 1024, 0, 0)).x);
}

float3 TerrainNormal(float3 wpos) {
    float of = 1.1;
    float3 px = wpos + float3(of, 0, 0);
    px.y = TerrainHeight(px.xz);
    float3 py = wpos + float3(0, 0, of);
    py.y = TerrainHeight(py.xz);

    float3 px_ = wpos - float3(of, 0, 0);
    px_.y = TerrainHeight(px_.xz);
    float3 py_ = wpos - float3(0, 0, of);
    py_.y = TerrainHeight(py_.xz);
    return normalize(cross(normalize(py_ - py), normalize(px_ - px)));
}


float TerrainShadow(float3 wpos) {

    float3 lpos = mul(_TerrainShadowMatrix0, float4(wpos, 1));

    if (all(lpos < 1 && lpos > 0)) {
        lpos.xy = lpos.xy * 0.5;
    }
    else {
        lpos = mul(_TerrainShadowMatrix1, float4(wpos, 1));

        if (all(lpos < 1 && lpos > 0)) {
            lpos.xy = lpos.xy * 0.5 + float2(0.5, 0);
        }
        else {
            lpos = mul(_TerrainShadowMatrix2, float4(wpos, 1));

            if (all(lpos < 1 && lpos > 0)) {
                lpos.xy = lpos.xy * 0.5 + float2(0, 0.5);
            }
            else {
                lpos = mul(_TerrainShadowMatrix3, float4(wpos, 1));
                lpos.xy = lpos.xy * 0.5 + 0.5;
            }
        }
    }

    return tex2Dlod(_TerrainShadowMap, float4(lpos.xy, 0, 0)).r > lpos.z ? 1 : 0;
}

float TerrainShadowDistance(float3 wpos) {

    float3 lpos = mul(_TerrainShadowMatrix0, float4(wpos, 1));

    if (all(lpos < 1 && lpos > 0)) {
        lpos.xy = lpos.xy * 0.5;
    }
    else {
        lpos = mul(_TerrainShadowMatrix1, float4(wpos, 1));

        if (all(lpos < 1 && lpos > 0)) {
            lpos.xy = lpos.xy * 0.5 + float2(0.5, 0);
        }
        else {
            lpos = mul(_TerrainShadowMatrix2, float4(wpos, 1));

            if (all(lpos < 1 && lpos > 0)) {
                lpos.xy = lpos.xy * 0.5 + float2(0, 0.5);
            }
            else {
                lpos = mul(_TerrainShadowMatrix3, float4(wpos, 1));
                lpos.xy = lpos.xy * 0.5 + 0.5;
            }
        }
    }

    return tex2Dlod(_TerrainShadowMap, float4(lpos.xy, 0, 0)).r - lpos.z;
}


#endif