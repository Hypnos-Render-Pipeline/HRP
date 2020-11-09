using UnityEditor;
using UnityEngine.UIElements;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class StyleLoader
    {
        public static string path = PathDefine.path + "RenderGraph/Editor/Style/";

        
        public static void Load<T>(T t) where T : VisualElement
        {
            var types = typeof(T).ToString().Split('.');
            t.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path + types[types.Length - 1] + ".uss"));
        }        
    }
}