using UnityEngine;
using System;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Assertions;
#endif

namespace HypnosRenderPipeline
{
    [Serializable]
    public enum HRPLightType
    {
        Directional,
        Point,
        Spot,
        Sphere,
        Tube,
        Quad,
        Disc,
        Mesh,
    }

    [Serializable]
    public enum HRPLightShadowType
    {
        Off,
        Rasterization,
        Cached,
        RayTrace,
    }
    public struct LightStructGPU
    {
        public Vector4 position_range;
        public Vector4 radiance_type;
        public Vector4 mainDirection_id;
        public Vector4 geometry;// Spot:    cosineAngle(x)
                                // Sphere:  radius(x)
                                // Tube:    length(x), radius(y)
                                // Quad:    size(xy)
                                // Disc:    radius(x)
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class HRPLight : MonoBehaviour
    {
        #region Public Properties

        public HRPLightType lightType = HRPLightType.Point;

        /// <summary>
        /// Light temperature (K), you should always use this(HRPLight.temperature) instead of using "Light.colorTemperature".
        /// </summary>
        public float temperature { get { return m_temperature; } set { m_temperature = value; m_light.color = Mathf.CorrelatedColorTemperatureToRGB(value).gamma; } }

        [Min(0)]
        /// <summary>
        /// Light radiance, you should always use this(HRPLight.radiance) instead of using "Light.intensity".
        /// </summary>
        public float radiance = 1;

        /// <summary>
        /// Whether IES is supported with this light type.
        /// </summary>
        public bool supportIES { get { return lightType == HRPLightType.Point || lightType == HRPLightType.Spot; } }

        /// <summary>
        /// IES profile.
        /// </summary>
        public Cubemap IESProfile;

        /// <summary>
        /// shadow mode.
        /// </summary>
        public HRPLightShadowType shadow = HRPLightShadowType.Rasterization;

        /// <summary>
        /// Currently this only work with Quad Light (or Mesh Light).
        /// </summary>
        public Texture2D areaTexture;

        /// <summary>
        /// Should Area light mesh be drawed ?
        /// </summary>
        public bool drawLightMesh;

        /// <summary>
        /// Filtered light texture (diffuse) for LTC.
        /// </summary>
        public RenderTexture filteredDiffuseTexture { get { if (!areaTextureAlreadyFiltered) GeneratePrefilteredAreaTexture(); return m_filteredDiffuseTex; } }

        /// <summary>
        /// Filtered light texture (specular) for LTC.
        /// </summary>
        public RenderTexture filteredSpecularTexture { get { if (!areaTextureAlreadyFiltered) GeneratePrefilteredAreaTexture(); return m_filteredSpecTex; } }

        public Mesh lightMesh;

        /// <summary>
        /// Global ID of light, started with 0, and will change when light list of the scene changing.
        /// </summary>
        public int id { get; internal set; } = -1;

#if UNITY_EDITOR
        /// <summary>
        /// Editor Only. Whether light texture has been filtered.
        /// </summary>
        public bool areaTextureAlreadyFiltered { get { if (areaTexture == null) return true; if (m_lastTex == null || areaTexture != m_lastTex) return false; return true; } }

        /// <summary>
        /// Editor Only.
        /// </summary>
        public bool canHasTexture { get { if (lightType == HRPLightType.Quad || lightType == HRPLightType.Disc || lightType == HRPLightType.Mesh) return true; return false; } }
#endif

        public bool isArea { get { if (canHasTexture || lightType == HRPLightType.Tube || lightType == HRPLightType.Sphere || lightType == HRPLightType.Disc) return true; return false; } }

        /// <summary>
        /// Tube length(x), Tube radius(y)
        /// </summary>
        public Vector2 tubeLengthRadius { get { return m_areaSize; } set { m_areaSize = value; } }

        public float sphereRadius { get { return m_areaSize.x; } set { m_areaSize.x = value; } }

        public float discRadius { get { return m_areaSize.x; } set { m_areaSize.x = value; } }

        /// <summary>
        /// Quad width(x), Quad height(y)
        /// </summary>
        public Vector2 quadSize { get { return m_areaSize; } set { m_areaSize = value; } }

        /// <summary>
        /// Light range, this will only be used for light culling, won't influnce light attenuation in HRP
        /// </summary>
        public float range { get { return m_light.range; } }

        /// <summary>
        /// Light Color, you should always use this(HRPLight.color) instead of using "Light.color".
        /// </summary>
        public Color color { get { return Mathf.CorrelatedColorTemperatureToRGB(m_temperature); } set { m_light.color = value; m_temperature = m_light.colorTemperature; } }

        public float spotAngle { get { return m_light.spotAngle; } set { m_light.spotAngle = spotAngle; } }

        /// <summary>
        /// This will trigger regenrate of light struct, call this frequently may cause performance issue
        /// </summary>
        public LightStructGPU lightStructGPU { get {
                Vector4 pr = transform.position;
                pr.w = range;                
                Color color_ = color;
                Vector4 rt = new Vector4(color_.r, color_.g, color_.b, 0);
                rt *= radiance;
                rt.w = (int)lightType;
                Vector4 mi = transform.forward;
                mi.w = id;
                Vector4 geo = Vector4.zero;
                switch (lightType)
                {
                    case HRPLightType.Spot:
                        geo.x = Mathf.Cos(spotAngle);
                        break;
                    case HRPLightType.Sphere:
                        geo.x = sphereRadius;
                        break;
                    case HRPLightType.Tube:
                        geo = tubeLengthRadius;
                        break;
                    case HRPLightType.Quad:
                        geo = quadSize;
                        break;
                    case HRPLightType.Disc:
                        geo.x = discRadius;
                        break;
                    default:
                        break;
                }
                return new LightStructGPU { position_range = pr, radiance_type = rt, mainDirection_id = mi, geometry = geo };
            } }

        #endregion

        #region Private Properties

        [SerializeField]
        private float m_temperature = 6000;

        [NonSerialized]
        private RenderTexture m_filteredDiffuseTex, m_filteredSpecTex;


        [SerializeField]
        [Min(0)]
        private Vector2 m_areaSize = Vector2.one;

        Light m_light { get { if (__m_light__ == null) __m_light__ = GetComponent<Light>(); return __m_light__; } }
        [NonSerialized]
        Light __m_light__ = null;

        [NonSerialized]
        Texture2D m_lastTex;

        #endregion

        #region Public Methods

        /// <summary>
        /// Copy necessary data from Unity Light. Don't use it unless you are prety sure about what you are doing.
        /// </summary>
        /// <param name="light"></param>
        public void Copy(Light light)
        {
            switch (light.type)
            {
                case LightType.Spot:
                    lightType = HRPLightType.Spot;
                    break;
                case LightType.Directional:
                    lightType = HRPLightType.Directional;
                    break;
                case LightType.Point:
                    lightType = HRPLightType.Point;
                    break;
                case LightType.Rectangle:
                    lightType = HRPLightType.Quad;
                    break;
                case LightType.Disc:
                    lightType = HRPLightType.Disc;
                    break;
                default:
                    break;
            }
            radiance = light.intensity;
            m_temperature = light.colorTemperature;
        }

        /// <summary>
        /// Generate and append a HRPLight for an existing Unity Light.
        /// </summary>
        public static HRPLight GenerateHRPLight(Light light)
        {
#if UNITY_EDITOR
            var l = Undo.AddComponent(light.gameObject, typeof(HRPLight)) as HRPLight;
#else
            var l = light.gameObject.AddComponent<HRPLight>();
#endif
            l.Copy(light);
            light.Copy(l);
            return l;
        }


        /// <summary>
        /// Generate Filtered light texture for LTC. Usually you don't need to call this function, HRPLight will automatically filter texture when it needed.
        /// </summary>
        public void GeneratePrefilteredAreaTexture()
        {
            if (areaTexture == null) return;
            if (!areaTextureAlreadyFiltered)
            {
                // generate
                if (m_filteredDiffuseTex != null)
                {
                    m_filteredDiffuseTex.Release();
                    m_filteredDiffuseTex = null;
                    m_filteredSpecTex.Release();
                    m_filteredSpecTex = null;
                }

                m_filteredDiffuseTex = CalculateDiffuse(areaTexture);
                m_filteredSpecTex = CalculateSpec(areaTexture);

                m_lastTex = areaTexture;
            }
        }

        #endregion

        #region Private Methods

        void OnEnable()
        {
            LightManager.ReportCreate(this);
        }

        void OnDisable()
        {
            LightManager.ReportDestroy(this);
            if (m_filteredDiffuseTex != null)
            {
                m_filteredDiffuseTex.Release();
                m_filteredDiffuseTex = null;
            }
            if (m_filteredSpecTex != null)
            {
                m_filteredSpecTex.Release();
                m_filteredSpecTex = null;
            }
            m_lastTex = null;
        }

        RenderTexture CalculateSpec(Texture2D tex)
        {
            RenderTexture filterd_tex = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.DefaultHDR, tex.mipmapCount);
            filterd_tex.wrapMode = TextureWrapMode.Clamp;
            filterd_tex.useMipMap = true;
            filterd_tex.autoGenerateMips = false;
            filterd_tex.Create();
            RenderTexture swap_tex = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.DefaultHDR, tex.mipmapCount);
            swap_tex.useMipMap = true;
            swap_tex.autoGenerateMips = false;
            swap_tex.Create();


            var blurMat = new Material(Shader.Find("Hidden/GenerateLTCTexture"));

            CommandBuffer cb = new CommandBuffer();

            cb.Blit(tex, filterd_tex);
            cb.Blit(filterd_tex, swap_tex);
            for (int i = 1; i < tex.mipmapCount; i++)
            {
                cb.SetRenderTarget(filterd_tex, i);
                cb.SetGlobalInt("_Level", i - 1);
                cb.Blit(swap_tex, BuiltinRenderTextureType.CurrentActive, blurMat, 0);
                cb.SetRenderTarget(swap_tex, i);
                cb.Blit(filterd_tex, BuiltinRenderTextureType.CurrentActive, blurMat, 1);
            }
            Graphics.ExecuteCommandBuffer(cb);
            swap_tex.Release();
            return filterd_tex;
        }

        RenderTexture CalculateDiffuse(Texture2D tex)
        {
            RenderTexture filterd_tex = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.DefaultHDR, tex.mipmapCount);
            filterd_tex.wrapMode = TextureWrapMode.Clamp;
            filterd_tex.useMipMap = true;
            filterd_tex.autoGenerateMips = false;
            filterd_tex.Create();
            RenderTexture swap_tex = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.DefaultHDR, tex.mipmapCount);
            swap_tex.useMipMap = true;
            swap_tex.autoGenerateMips = false;
            swap_tex.Create();


            var blurMat = new Material(Shader.Find("Hidden/GenerateLTCTexture"));

            CommandBuffer cb = new CommandBuffer();

            cb.Blit(tex, filterd_tex, blurMat, 2);
            cb.Blit(filterd_tex, swap_tex);
            for (int i = 1; i < tex.mipmapCount; i++)
            {
                cb.SetRenderTarget(filterd_tex, i);
                cb.SetGlobalInt("_Level", i - 1);
                cb.Blit(swap_tex, BuiltinRenderTextureType.CurrentActive, blurMat, 0);
                cb.SetRenderTarget(swap_tex, i);
                cb.Blit(filterd_tex, BuiltinRenderTextureType.CurrentActive, blurMat, 1);
            }
            Graphics.ExecuteCommandBuffer(cb);
            swap_tex.Release();
            return filterd_tex;
        }

        #endregion

#if UNITY_EDITOR

        [MenuItem("CONTEXT/Light/Remove Component", false, 0)]
        static void RemoveLight(MenuCommand menuCommand)
        {
            GameObject go = ((Light)menuCommand.context).gameObject;

            Assert.IsNotNull(go);

            Undo.IncrementCurrentGroup();
            Undo.DestroyObjectImmediate(go.GetComponent<Light>());
            Undo.DestroyObjectImmediate(go.GetComponent<HRPLight>());
        }

        [MenuItem("CONTEXT/Light/Reset", false, 0)]
        static void ResetLight(MenuCommand menuCommand)
        {
            GameObject go = ((Light)menuCommand.context).gameObject;

            Assert.IsNotNull(go);

            Light light = go.GetComponent<Light>();
            HRPLight lightAdditionalData = go.GetComponent<HRPLight>();

            Assert.IsNotNull(light);
            Assert.IsNotNull(lightAdditionalData);

            Undo.RecordObjects(new UnityEngine.Object[] { light, lightAdditionalData }, "Reset HD Light");
            light.Reset();
            lightAdditionalData.Copy(light);
        }
#endif
    }

    public static class HRPLightUtil
    {
        /// <summary>
        /// Get HRPLight, if not exist, generate it.
        /// </summary>
        /// <param name="light"></param>
        public static HRPLight GetHRPLight(this Light light)
        {
            var l = light.GetComponent<HRPLight>();
            if (l == null)
            {
                return light.GenerateHRPLight();
            }
            return l;
        }

        /// <summary>
        /// Generate HRP light.
        /// </summary>
        /// <param name="light"></param>
        public static HRPLight GenerateHRPLight(this Light light)
        {
            return HRPLight.GenerateHRPLight(light);
        }

        public static void Copy(this Light light, HRPLight lightData)
        {
            if (lightData.isArea)
            {
                light.type = LightType.Point;
            }
            else switch (lightData.lightType)
                {
                    case HRPLightType.Point:
                        light.type = LightType.Point;
                        break;
                    case HRPLightType.Directional:
                        light.type = LightType.Directional;
                        break;
                    case HRPLightType.Spot:
                        light.type = LightType.Spot;
                        break;
                    default:
                        break;
                }

            light.color = Mathf.CorrelatedColorTemperatureToRGB(lightData.temperature).gamma;
            light.intensity = lightData.radiance;
        }
    }
}
