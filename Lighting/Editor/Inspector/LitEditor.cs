using UnityEditor;
using UnityEngine;


namespace HypnosRenderPipeline
{
    public class LitEditor : ShaderGUI
    {
        MaterialEditor m_MaterialEditor;

        MaterialProperty albedoMap = null;
        MaterialProperty albedoColor = null;
        MaterialProperty cutoff = null;

        MaterialProperty metallic = null;
        MaterialProperty metallicglossyMap = null;

        MaterialProperty smoothness = null;
        MaterialProperty smoothnessScale = null;

        MaterialProperty bumpMap = null;
        MaterialProperty bumpScale = null;

        MaterialProperty aoScale = null;
        MaterialProperty aoMap = null;

        MaterialProperty emissionMap = null;
        MaterialProperty emission = null;

        MaterialProperty subsurface = null;
        MaterialProperty ld = null;
        MaterialProperty ssProfile = null;

        MaterialProperty clearCoat = null;

        MaterialProperty sheen = null;

        MaterialProperty index = null;

        MaterialProperty index_rate = null;

        public void FindProperties(MaterialProperty[] props)
        {
            albedoMap = FindProperty("_MainTex", props);
            albedoColor = FindProperty("_Color", props);

            cutoff = FindProperty("_Cutoff", props);

            metallicglossyMap = FindProperty("_MetallicGlossMap", props, false);
            metallic = FindProperty("_Metallic", props);

            smoothness = FindProperty("_Smoothness", props);
            smoothnessScale = FindProperty("_GlossMapScale", props, false);

            bumpScale = FindProperty("_BumpScale", props);
            bumpMap = FindProperty("_BumpMap", props);

            aoScale = FindProperty("_AOScale", props);
            aoMap = FindProperty("_AOMap", props);

            emission = FindProperty("_EmissionColor", props);
            emissionMap = FindProperty("_EmissionMap", props);

            subsurface = FindProperty("_Subsurface", props);
            ld = FindProperty("_Ld", props);
            ssProfile = FindProperty("_ScatterProfile", props);

            clearCoat = FindProperty("_ClearCoat", props);
            sheen = FindProperty("_Sheen", props);

            index = FindProperty("_Index", props);

            index_rate = FindProperty("_IndexRate", props);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperties(properties);
            m_MaterialEditor = materialEditor;
            Material material = materialEditor.target as Material;

            Event e = Event.current;
            if (EventType.KeyDown == e.type && KeyCode.L == e.keyCode)
            {
                material.SetInt("_EnableEdit", 1 - material.GetInt("_EnableEdit"));
            }

            ShaderPropertiesGUI(material);
        }

        public void ShaderPropertiesGUI(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;
            EditorGUI.BeginDisabledGroup(material.hideFlags == HideFlags.NotEditable);
            {
                // Detect any changes to the material
                EditorGUI.BeginChangeCheck();
                {
                    // Primary properties
                    GUILayout.Label(Styles.primaryMapsText, EditorStyles.boldLabel);
                    DoAlbedoArea(material);
                    m_MaterialEditor.ShaderProperty(cutoff, Styles.alphaCutoffText);

                    DoSpecularMetallicArea();
                    DoNormalArea();
                    DoEmissionArea(material);
                    EditorGUI.BeginChangeCheck();
                    m_MaterialEditor.TextureScaleOffsetProperty(albedoMap);
                    if (EditorGUI.EndChangeCheck())
                        emissionMap.textureScaleAndOffset = albedoMap.textureScaleAndOffset;

                    EditorGUILayout.Space();

                    EditorGUILayout.Space();


                    m_MaterialEditor.ShaderProperty(subsurface, Styles.subsurfaceText);
                    bool use_subsurface = subsurface.floatValue != 0;
                    if (use_subsurface)
                    {
                        m_MaterialEditor.ShaderProperty(ld, Styles.LdText);
                        m_MaterialEditor.TexturePropertySingleLine(Styles.scatterText, ssProfile, null);
                        EditorGUILayout.Space();
                    }

                    EditorGUILayout.Space();

                    m_MaterialEditor.ShaderProperty(clearCoat, Styles.clearCoatText);

                    EditorGUILayout.Space();

                    m_MaterialEditor.ShaderProperty(sheen, Styles.sheenText);


                    EditorGUILayout.Space();

                    m_MaterialEditor.ShaderProperty(index, Styles.indexText);
                    m_MaterialEditor.ShaderProperty(index_rate, Styles.indexRateText);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    SetMaterialKeywords(material);
                    HypnosRenderPipeline.RTRegister.SceneChanged();
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (oldShader.name == "Autodesk Interactive")
            {
                material.SetInt("_AutoDesk", 1);
                material.SetTexture("_MetallicMap", material.GetTexture("_MetallicGlossMap"));
                material.SetTexture("_RoughnessMap", material.GetTexture("_SpecGlossMap"));
                material.SetFloat("_Metallic", material.GetFloat("_Metallic"));
                material.SetFloat("_Roughness", material.GetFloat("_Glossiness"));
            }
            else
            {
                material.SetInt("_AutoDesk", 0);
            }
            // _Emission property is lost after assigning Standard shader to the material
            // thus transfer it before assigning the new shader
            if (material.HasProperty("_Emission"))
            {
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            SetMaterialKeywords(material);
        }



        void DoAlbedoArea(Material material)
        {
            m_MaterialEditor.TexturePropertySingleLine(Styles.albedoText, albedoMap, albedoColor);
        }

        void DoSpecularMetallicArea()
        {
            bool hasGlossMap = false;

            hasGlossMap = metallicglossyMap.textureValue != null;
            m_MaterialEditor.TexturePropertySingleLine(Styles.metallicsmothnessMapText, metallicglossyMap, hasGlossMap ? null : metallic);

            bool showSmoothnessScale = hasGlossMap;

            int indentation = 2; // align with labels of texture properties
            m_MaterialEditor.ShaderProperty(showSmoothnessScale ? smoothnessScale : smoothness, showSmoothnessScale ? Styles.smoothnessScaleText : Styles.smoothnessText, indentation);
        }

        void DoNormalArea()
        {
            m_MaterialEditor.TexturePropertySingleLine(Styles.normalMapText, bumpMap, bumpMap.textureValue != null ? bumpScale : null);
            m_MaterialEditor.TexturePropertySingleLine(Styles.aoMapText, aoMap, aoMap.textureValue != null ? aoScale : null);
        }

        void DoEmissionArea(Material material)
        {
            // Emission for GI?
            if (m_MaterialEditor.EmissionEnabledProperty())
            {
                bool hadEmissionTexture = emissionMap.textureValue != null;

                // Texture and HDR color controls
                m_MaterialEditor.TexturePropertyWithHDRColor(Styles.emissionText, emissionMap, emission, false);

                // If texture was assigned and color was black set color to white
                float brightness = emission.colorValue.maxColorComponent;
                if (emissionMap.textureValue != null && !hadEmissionTexture && brightness <= 0f)
                    emission.colorValue = Color.black;
            }
        }

        static void SetMaterialKeywords(Material material)
        {
            // Note: keywords must be based on Material value not on MaterialProperty due to multi-edit & material animation
            // (MaterialProperty value might come from renderer material property block)
            SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap"));
            SetKeyword(material, "_AOMAP", material.GetTexture("_AOMap"));

            SetKeyword(material, "_METALLICGLOSSMAP", material.GetTexture("_MetallicGlossMap"));

            SetKeyword(material, "_CLEARCOAT", material.GetFloat("_ClearCoat") != 0);

            SetKeyword(material, "_SUBSURFACE", material.GetInt("_Subsurface") != 0);
            // A material's GI flag internally keeps track of whether emission is enabled at all, it's enabled but has no effect
            // or is enabled and may be modified at runtime. This state depends on the values of the current flag and emissive color.
            // The fixup routine makes sure that the material is in the correct state if/when changes are made to the mode or color.
            MaterialEditor.FixupEmissiveFlag(material);
            bool shouldEmissionBeEnabled = (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
            SetKeyword(material, "_EMISSION", shouldEmissionBeEnabled);

            float alpha = material.GetColor("_Color").a;

            if (alpha == 1)
            {
                material.SetOverrideTag("Quque", "Geometry");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
            }
            else
            {
                material.SetOverrideTag("Quque", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        static void SetKeyword(Material m, string keyword, bool state)
        {
            if (state)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
        }

        private static class Styles
        {
            public static GUIContent uvSetLabel = EditorGUIUtility.TrTextContent("UV Set");

            public static GUIContent albedoText = EditorGUIUtility.TrTextContent("Albedo", "Albedo (RGB) and Transparency (A)");
            public static GUIContent alphaCutoffText = EditorGUIUtility.TrTextContent("Alpha Cutoff", "Threshold for alpha cutoff");
            public static GUIContent specularMapText = EditorGUIUtility.TrTextContent("Specular", "Specular (RGB) and Smoothness (A)");
            public static GUIContent metallicsmothnessMapText = EditorGUIUtility.TrTextContent("Metallic", "Metallic (R) and Smoothness (A)");
            public static GUIContent autodeskText = EditorGUIUtility.TrTextContent("Autodesk format", "Use Autodesk format.");
            public static GUIContent metallicMapText = EditorGUIUtility.TrTextContent("Metallic", "Metallic (R)");
            public static GUIContent roughnessMapText = EditorGUIUtility.TrTextContent("Roughness", "Roughness (R)");
            public static GUIContent smoothnessText = EditorGUIUtility.TrTextContent("Smoothness", "Smoothness value");
            public static GUIContent smoothnessScaleText = EditorGUIUtility.TrTextContent("Smoothness", "Smoothness scale factor");
            public static GUIContent smoothnessMapChannelText = EditorGUIUtility.TrTextContent("Source", "Smoothness texture and channel");
            public static GUIContent highlightsText = EditorGUIUtility.TrTextContent("Specular Highlights", "Specular Highlights");
            public static GUIContent reflectionsText = EditorGUIUtility.TrTextContent("Reflections", "Glossy Reflections");
            public static GUIContent normalMapText = EditorGUIUtility.TrTextContent("Normal Map", "Normal Map");
            public static GUIContent aoMapText = EditorGUIUtility.TrTextContent("AO Map", "AO Map(R)");
            public static GUIContent heightMapText = EditorGUIUtility.TrTextContent("Height Map", "Height Map (G)");
            public static GUIContent occlusionText = EditorGUIUtility.TrTextContent("Occlusion", "Occlusion (G)");
            public static GUIContent emissionText = EditorGUIUtility.TrTextContent("Color", "Emission (RGB)");
            public static GUIContent detailMaskText = EditorGUIUtility.TrTextContent("Detail Mask", "Mask for Secondary Maps (A)");
            public static GUIContent detailAlbedoText = EditorGUIUtility.TrTextContent("Detail Albedo x2", "Albedo (RGB) multiplied by 2");
            public static GUIContent detailNormalMapText = EditorGUIUtility.TrTextContent("Normal Map", "Normal Map");

            public static GUIContent subsurfaceText = EditorGUIUtility.TrTextContent("Subsurface", "Subsurface material");
            public static GUIContent LdText = EditorGUIUtility.TrTextContent("Ld", "Average scatter distance");
            public static GUIContent scatterText = EditorGUIUtility.TrTextContent("Scatter profile", "Scatter (RGB)");

            public static GUIContent clearCoatText = EditorGUIUtility.TrTextContent("Clear Coat", "Clear coat strength. \nOften used to simulate car paint.");

            public static GUIContent sheenText = EditorGUIUtility.TrTextContent("Sheen", "Sheen strength. \nOften used to simulate cloth.");

            public static GUIContent indexText = EditorGUIUtility.TrTextContent("IOR", "Material Index");
            public static GUIContent indexRateText = EditorGUIUtility.TrTextContent("Chromatic Dispersion", "Material Index variation rate among different wavelength");
            public static GUIContent mipScaleText = EditorGUIUtility.TrTextContent("Mip Scale", "Use this value to scale mip level. \nTurn this down will cause blur but reduce noise.");

            public static GUIContent tracIndirectText = EditorGUIUtility.TrTextContent("Trace Indirect Light", "Whether trace indirect light");

            public static string primaryMapsText = "Main Maps";
            public static string secondaryMapsText = "Secondary Maps";
            public static string forwardText = "Forward Rendering Options";
            public static string renderingMode = "Rendering Mode";
            public static string advancedText = "Advanced Options";
        }
    }
}