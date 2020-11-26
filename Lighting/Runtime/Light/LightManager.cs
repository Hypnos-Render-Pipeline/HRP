using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace HypnosRenderPipeline
{
    public class LightList
    {
        /// <summary>
        /// SunLight, if exist. It will also be included in the directionals list.
        /// </summary>
        public HRPLight sunLight;

        /// <summary>
        /// directional lights
        /// </summary>
        public List<HRPLight> directionals;

        /// <summary>
        /// local lights, they are point/spot or sphere lights close to the camera
        /// </summary>
        public List<HRPLight> locals;

        /// <summary>
        /// area lights
        /// </summary>
        public List<HRPLight> areas;

        /// <summary>
        /// faraway lights, they are point/spot or sphere lights faraway from the camera
        /// </summary>
        public List<HRPLight> faraways;

        public LightList()
        {
            directionals = new List<HRPLight>();
            locals = new List<HRPLight>();
            areas = new List<HRPLight>();
            faraways = new List<HRPLight>();
        }

        public void Clear()
        {
            sunLight = null;
            directionals.Clear();
            locals.Clear();
            areas.Clear();
            faraways.Clear();
        }

        public void Copy(LightList lightList)
        {
            Clear();
            sunLight = lightList.sunLight;
            directionals.AddRange(lightList.directionals);
            locals.AddRange(lightList.locals);
            areas.AddRange(lightList.areas);
            faraways.AddRange(lightList.faraways);
        }
    }


    public sealed class LightManager
    {
        /// <summary>
        /// Sun light of the scene.
        /// </summary>
        static public HRPLight sunLight;

        private LightManager() { }

        private class Nested { static Nested() { } internal static readonly LightManager instance = new LightManager(); }
        private static LightManager instance { get { return Nested.instance; } }

        int m_totalCount = 0;
        HashSet<HRPLight> m_lightSet = new HashSet<HRPLight>();
        HRPLight[] m_lightList = new HRPLight[201];

        Dictionary<Camera, Dictionary<float, List<HRPLight>>> m_cullings = new Dictionary<Camera, Dictionary<float, List<HRPLight>>>();

        Plane[] m_planes;

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
        public static void GetVisibleLights(LightList list, Camera cam, float faraway = 200) { instance.__GetVisibleLights__(list, cam, faraway); }

        /// <summary>
        /// Return visible Lights with given camera and specific cullingDesc
        /// </summary>
        public static void GetVisibleLights(LightList list, Camera cam, float radius, float faraway = 200) { instance.__GetVisibleLights__(list, cam, radius, faraway); }

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

        private void __GetVisibleLights__(LightList list, Camera cam, float faraway)
        {
            list.Clear();
            list.sunLight = sunLight;
            m_planes = GeometryUtility.CalculateFrustumPlanes(cam);
            Bounds bounds = new Bounds();

            var camera_pos = cam.transform.position;

            for (int i = 0; i < m_totalCount; i++)
            {
                var light = instance.m_lightList[i];
                if (light.lightType == HRPLightType.Directional)
                {
                    list.directionals.Add(light);
                    continue;
                }
                bounds.center = light.transform.position;
                bounds.size = Vector3.one * light.range * 2;
                if (GeometryUtility.TestPlanesAABB(m_planes, bounds))
                {
                    float dis = Vector3.Distance(camera_pos, light.transform.position);
                    if (light.isArea && dis < faraway)
                    {
                        list.areas.Add(light);
                    }
                    else if (dis > faraway)
                    {
                        list.faraways.Add(light);
                    }
                    else
                    {
                        list.locals.Add(light);
                    }
                }
            }
        }

        private void __GetVisibleLights__(LightList list, Camera cam, float radius, float faraway)
        {
            Assert.IsTrue(radius > 0, "Culling radius must be greater than 0!");
            Assert.IsTrue(radius > faraway, "Culling radius must be greater than faraway distance!");

            list.Clear();
            list.sunLight = sunLight;

            var camera_pos = cam.transform.position;

            for (int i = 0; i < m_totalCount; i++)
            {
                var light = instance.m_lightList[i];
                if (light.lightType == HRPLightType.Directional)
                {
                    list.directionals.Add(light);
                    continue;
                }

                float dis = Vector3.Distance(camera_pos, light.transform.position);
                if (dis < radius + light.range)
                {
                    if (light.isArea && dis < faraway)
                    {
                        list.areas.Add(light);
                    }
                    else if (dis > faraway)
                    {
                        list.faraways.Add(light);
                    }
                    else
                    {
                        list.locals.Add(light);
                    }
                }
            }
        }

        #endregion
    }
}
