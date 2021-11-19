#ifndef FOGINCLUDE_H_
#define FOGINCLUDE_H_


#include "./LitInclude.hlsl"

sampler2D _ByPassUnityBug;
float4 _FogVolumeSize;
RWTexture3D<int> _Volume0 : register(u1);
RWTexture3D<int> _Volume1 : register(u2);
RWTexture3D<int> _Volume2 : register(u3);

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float3 wpos : TEXCOORD0;
    float4 vertex : SV_POSITION;
};

v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.wpos = mul(unity_ObjectToWorld, v.vertex);
    return o;
}

float2 RayBoxOffset(float3 p, float3 dir)
{
    dir = rcp(dir);
    float3 bmax = { 0.500001f, 0.500001f, 0.500001f };
    float3 to_axil_dis = -p * dir;
    float3 axil_to_face_dis = bmax * dir;

    float3 dis0 = to_axil_dis + axil_to_face_dis;
    float3 dis1 = to_axil_dis - axil_to_face_dis;

    float3 tmin = min(dis0, dis1);
    float3 tmax = max(dis0, dis1);

    float tmi = max(tmin.x, max(tmin.y, tmin.z));
    float tma = min(tmax.x, min(tmax.y, tmax.z));

    return tma >= tmi ? float2(max(tmi, 0.0f), tma) : -1;
}


float3 Fog(float3 wpos, float3 uv);

float frag(v2f i) : SV_Target
{
    float value = tex2Dlod(_ByPassUnityBug, float4(0.5,0.5,0,0));
    float3 camPos = _V_Inv._m03_m13_m23;
    float3 pos = i.wpos;
    float3 view = pos - camPos;
    float max_depth = length(view);
    view /= max_depth;

    float3 oview = mul(unity_WorldToObject, view);
    float3 ocamPos = mul(unity_WorldToObject, float4(camPos, 1));

    float2 dis = RayBoxOffset(ocamPos, oview);

    float z_stride = _FogVolumeSize.w / _FogVolumeSize.z;

    int2 range = trunc(dis / z_stride + 0.5);
    range = clamp(range, 0, _FogVolumeSize.z - 1);

    for (int z = range.x; z < range.y; z++)
    {
        float3 wpos = camPos + view * (z + 0.5) * z_stride;
        float3 grid_uv = mul(unity_WorldToObject, float4(wpos, 1)) + 0.5;

        float3 fog = Fog(wpos, grid_uv);
        fog.yz *= fog.x;
        int3 ifog = fog * 10000;
        InterlockedAdd(_Volume0[int3(i.vertex.xy, z)], ifog.x);
        InterlockedAdd(_Volume1[int3(i.vertex.xy, z)], ifog.y);
        InterlockedAdd(_Volume2[int3(i.vertex.xy, z)], ifog.z);
    }

    return value;
}

#endif