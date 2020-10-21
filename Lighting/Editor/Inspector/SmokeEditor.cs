using System.Data.SqlClient;
using UnityEditor;
using UnityEngine;


namespace HypnosRenderPipeline
{
    public class SmokeEditor : ShaderGUI
    {
        MaterialEditor m_MaterialEditor;

        MaterialProperty tex = null;
        MaterialProperty res = null;

        MaterialProperty wc = null;

        MaterialProperty Scatter = null;
        MaterialProperty Absorb = null;
        MaterialProperty G = null;

        public void FindProperties(MaterialProperty[] props)
        {
            tex = FindProperty("_Volume", props);
            res = FindProperty("_SliceNum", props);
            wc = FindProperty("_AtlasWidthCount", props);

            Scatter = FindProperty("_Scatter", props);
            Absorb = FindProperty("_Absorb", props);
            G = FindProperty("_G", props);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperties(properties);
            m_MaterialEditor = materialEditor;
            Material material = materialEditor.target as Material;

            ShaderPropertiesGUI(material);
        }

        public void ShaderPropertiesGUI(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            // Detect any changes to the material
            EditorGUI.BeginChangeCheck();
            {
                // Primary properties

                EditorGUI.BeginChangeCheck();
                m_MaterialEditor.TexturePropertySingleLine(Styles.texText, tex, null);
                if (EditorGUI.EndChangeCheck())
                {
                    SmokeManager.Regenerate();
                }

                EditorGUI.BeginChangeCheck();
                res.floatValue = Mathf.Max(1, EditorGUILayout.IntField("Slice Number", (int)res.floatValue));
                if (EditorGUI.EndChangeCheck())
                {
                    var z = (int)res.floatValue;
                    var k = tex.textureValue.height / tex.textureValue.width;
                    z /= k;
                    wc.floatValue = Mathf.RoundToInt(Mathf.Sqrt(z));
                }

                wc.floatValue = Mathf.Max(1, EditorGUILayout.IntField("Atlas Width Count", (int)wc.floatValue));

                EditorGUILayout.Space(10);

                m_MaterialEditor.ShaderProperty(Scatter, "Scatter");
                m_MaterialEditor.ShaderProperty(Absorb, "Absorb");
                m_MaterialEditor.ShaderProperty(G, "G");

                EditorGUILayout.LabelField("MaterialID", material.GetInt("_MaterialID").ToString());
            }
            if (EditorGUI.EndChangeCheck())
            {
                RTRegister.SceneChanged();
            }
        }

        private static class Styles
        {
            public static GUIContent texText = EditorGUIUtility.TrTextContent("Volume Texture", "Volume Slice Atlas Texture");
        }
    }
}