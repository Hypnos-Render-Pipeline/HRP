#ifndef IRRADIANCE_CACHE_H_
#define IRRADIANCE_CACHE_H_
      
RWTexture3D<float4> _IrrVolume;
const static int3 volumeSize = int3(128, 64, 128);
float3 _IVScale;

int3 WPos2Index(float3 wpos)
{
    float3 d = (wpos - _V_Inv._m03_m13_m23) / _IVScale;
    int3 intd = floor(d + volumeSize / 2);
    
    return intd;
}

float4 GetIrr(float3 wpos)
{
    int3 index = WPos2Index(wpos);
    
    if (any((index < 0) + (index >= volumeSize)))
        return -1;
    
    return _IrrVolume[index];
}

void SetIrr(float3 wpos, float3 irr)
{
    int3 index = WPos2Index(wpos);
    
    if (any((index < 0) + (index >= volumeSize)))
        return;
    
    float4 c = _IrrVolume[index];
    float k = c.a;
    c.rgb = lerp(c.rgb, irr, 1.0f / (k + 1));
    c.a = min(256, k + 1);
    _IrrVolume[index] = c;
}

#endif