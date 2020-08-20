using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace HypnosRenderPipeline
{
    public enum LightCullingType { Frustum, Shpere }

    public struct LightCullingDesc
    {
        public LightCullingType cullingType;
        public float radius;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type">Culling Type</param>
        /// <param name="radius">Radius if using Sphere culling</param>
        public LightCullingDesc(LightCullingType type = LightCullingType.Frustum, float radius = 50)
        {
            cullingType = type;
            this.radius = radius;
        }
    }



    public sealed class LightManager
    {
        private LightManager() { }

        private class Nested { static Nested() { } internal static readonly LightManager instance = new LightManager(); }
        private static LightManager instance { get { return Nested.instance; } }

        int m_totalCount = 0;
        HashSet<HRPLight> m_lightSet = new HashSet<HRPLight>();
        HRPLight[] m_lightList = new HRPLight[201];

        Dictionary<Camera, Dictionary<float, List<HRPLight>>> m_cullings = new Dictionary<Camera, Dictionary<float, List<HRPLight>>>();

        Vector3 m_camera_pos;
        Plane[] m_planes;

        unsafe struct CullingLight : IJobParallelFor
        {
            public float radius;
            [WriteOnly]
            public NativeArray<bool> visibilityArray;

            public void Execute(int id)
            {
                var light = instance.m_lightList[id];
                if (radius == -1)
                {
                    visibilityArray[id] = true;
                }
                else
                {
                    Vector3 cp = instance.m_camera_pos;
                    if (light.lightType != HRPLightType.Directional && Vector3.Distance(cp, light.transform.position) > radius + light.range)
                        visibilityArray[id] = false;
                    else
                        visibilityArray[id] = true;
                }
            }
        }
        CullingLight m_cullingLight = new CullingLight();


        #region Pubic Methods

        /// <summary>
        /// This should not be called manually.
        /// </summary>
        public static void ReportCreate(HRPLight light) { instance.__ReportCreate__(light); }

        /// <summary>
        /// This should not be called manually.
        /// </summary>
        public static void ReportDestroy(HRPLight light) { instance.__ReportDestroy__(light); }

        /// <summary>
        /// Return all the active Lights in scene.
        /// </summary>
        /// <param name="list"></param>
        public static void GetVisibleLights(List<HRPLight> list) { instance.__GetAllLights__(list); }

        /// <summary>
        /// Return visible Lights with given camera
        /// </summary>
        public static void GetVisibleLights(List<HRPLight> list, Camera cam) { instance.__GetVisibleLights__(list, cam); }

        /// <summary>
        /// Return visible Lights with given camera and specific cullingDesc
        /// </summary>
        public static void GetVisibleLights(List<HRPLight> list, Camera cam, LightCullingDesc lightCullingDesc) { if (lightCullingDesc.cullingType == LightCullingType.Frustum) instance.__GetVisibleLights__(list, cam); else instance.__GetVisibleLights__(list, cam, lightCullingDesc.radius); }

        #endregion

        #region Private Methods

        private void __ReportCreate__(HRPLight light)
        {
            if (m_lightSet.Contains(light))
            {
                Debug.LogWarning("Create already exist light");
                return;
            }
            if (m_totalCount >= 200)
            {
                Debug.LogWarning("Only supports the number of lights below 200.");
                return;
            }
            m_lightSet.Add(light);
            m_lightList[m_totalCount] = light;
            light.id = m_totalCount++;
        }

        private void __ReportDestroy__(HRPLight light)
        {
            if (!m_lightSet.Contains(light))
            {
                Debug.LogWarning("Destroy non-exist Light");
                return;
            }

            m_lightSet.Remove(light);
            m_totalCount--;
            (m_lightList[light.id] = m_lightList[m_totalCount]).id = light.id;
            light.id = -1;
        }

        private void __BeginCameraFrame__(Camera cam)
        {
            if (!m_cullings.ContainsKey(cam))
            {
                m_cullings.Add(cam, new Dictionary<float, List<HRPLight>>());
            }
            m_cullings[cam].Clear();
        }

        private void __EndCameraFrame__(Camera cam)
        {
            //m_cullings[cam].Clear();
        }


        private void __GetAllLights__(List<HRPLight> list)
        {
            list.Clear();
            list.Capacity = m_totalCount;
            for (int i = 0; i < m_totalCount; i++)
            {
                list.Add(m_lightList[i]);
            }
        }

        private void __GetVisibleLights__(List<HRPLight> list, Camera cam)
        {
            list.Clear();
            list.Capacity = m_totalCount;

            m_planes = GeometryUtility.CalculateFrustumPlanes(cam);
            m_cullingLight.radius = -1;
            m_cullingLight.visibilityArray = new NativeArray<bool>(m_totalCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            m_cullingLight.Schedule(m_totalCount, math.max(1, m_totalCount / SystemInfo.processorCount)).Complete();

            for (int i = 0; i < m_totalCount; i++)
            {
                if (m_cullingLight.visibilityArray[i])
                {
                    list.Add(m_lightList[i]);
                }
            }
            m_cullingLight.visibilityArray.Dispose();
        }

        private void __GetVisibleLights__(List<HRPLight> list, Camera cam, float radius)
        {
            Assert.IsTrue(radius > 0, "Culling radius must be greater than 0!");
            list.Clear();
            list.Capacity = m_totalCount;

            this.m_camera_pos = cam.transform.position;
            m_cullingLight.radius = radius;
            m_cullingLight.visibilityArray = new NativeArray<bool>(m_totalCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            m_cullingLight.Schedule(m_totalCount, math.max(1, m_totalCount / SystemInfo.processorCount)).Complete();

            for (int i = 0; i < m_totalCount; i++)
            {
                if (m_cullingLight.visibilityArray[i])
                {
                    list.Add(m_lightList[i]);
                }
            }
            m_cullingLight.visibilityArray.Dispose();
        }

        #endregion
    }
}
