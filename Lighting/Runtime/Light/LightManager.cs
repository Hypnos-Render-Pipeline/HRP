using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HypnosRenderPipeline
{
    public enum LightCullingType { Frustum, Shpere }

    public struct LightCullingDesc
    {
        public LightCullingType cullingType;
        public float radius;
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
            __GetAllLights__(list);
            // todo: culling
        }

        private void __GetVisibleLights__(List<HRPLight> list, Camera cam, float radius)
        {
            list.Clear();
            list.Capacity = m_totalCount;
            Vector3 cp = cam.transform.position;
            for (int i = 0; i < m_totalCount; i++)
            {
                var light = m_lightList[i];
                if (light.lightType != HRPLightType.Directional && Vector3.Distance(cp, light.transform.position) > radius + light.range)
                    continue;
                list.Add(light);
            }
        }


        #endregion
    }
}
