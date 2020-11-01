using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Runtime.InteropServices;
namespace HypnosRenderPipeline
{
    public class LightListGenerator
    {

        private struct LightStruct
        {
            public float4 a, b, c;
            public float3 d;
            public int mask;
        }

        public static int lightStructSize { get { return Marshal.SizeOf<LightStruct>(); } }

        static public int Generate(List<HRPLight> rtLights, ref ComputeBuffer buffer)
        {
            LightStruct[] rtllist = new LightStruct[rtLights.Count];

            int index = 0;
            foreach (var light in rtLights)
            {
                if (light.lightType == HRPLightType.Mesh || light.lightType == HRPLightType.Tube) continue;
                ref LightStruct s = ref rtllist[index];
                Transform trans = light.transform;
                s.a = float4(trans.position, (int)light.lightType);
                if (light.lightType == HRPLightType.Sphere) s.a.w = (int)(HRPLightType.Point);
                s.d = float3(light.color.r, light.color.g, light.color.b) * light.radiance;
                s.mask = light.sunLight ? 1 : -1;
                switch (light.lightType)
                {
                    case HRPLightType.Spot:
                        s.b = float4(float4(-light.transform.localToWorldMatrix.GetColumn(2)).xyz, cos(light.spotAngle / 2 * 0.0174533f));
                        s.c = light.range / 10;
                        break;
                    case HRPLightType.Directional:
                        s.b = -float4(float4(light.transform.localToWorldMatrix.GetColumn(2)).xyz, 0);
                        break;
                    case HRPLightType.Point:
                        s.b.x = light.range / 10;
                        s.b.y = 0.01f;
                        break;
                    case HRPLightType.Sphere:
                        s.b.x = light.range / 10;
                        s.b.y = math.max(0.01f, light.sphereRadius);
                        break;
                    case HRPLightType.Quad:
                        s.b = float4(float4(light.transform.localToWorldMatrix.GetColumn(0)).xyz * light.quadSize.x, light.range / 10);
                        s.c = float4(float4(light.transform.localToWorldMatrix.GetColumn(1)).xyz * light.quadSize.y, light.quadSize.x * light.quadSize.y / 3.14159265359f / (3.14159265359f / 2) / (3.14159265359f / 2));
                        //s.d.xyz *= 3.14159265359f;
                        break;
                    case HRPLightType.Disc:
                        s.b = float4(-float4(light.transform.localToWorldMatrix.GetColumn(2)).xyz, light.quadSize.x);
                        s.c = light.range / 10;
                        //s.d.xyz *= 3.14159265359f;
                        break;
                    default:
                        break;
                }
                index++;
            }
            buffer.SetData(rtllist, 0, 0, min(100, rtllist.Length));
            return min(100, rtllist.Length);
        }
    }
}