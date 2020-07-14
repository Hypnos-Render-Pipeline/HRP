using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class StyleLoader
    {
        public static string path = "Assets/HRP/RenderGraph/Editor/Style/";

        
        public static void Load<T>(T t) where T : VisualElement
        {
            var types = typeof(T).ToString().Split('.');
            t.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path + types[types.Length - 1] + ".uss"));
        }        
    }
}