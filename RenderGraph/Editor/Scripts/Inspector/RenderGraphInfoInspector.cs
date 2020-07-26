using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{

    [CustomEditor(typeof(RenderGraphInfo))]
    public class RenderGraphInfoInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            try
            {
                var path = AssetDatabase.GetAssetPath(instanceID);
                if (AssetDatabase.LoadAssetAtPath<RenderGraphInfo>(path) != null)
                {
                    RenderGraphViewWindow.Create().Load(path);
                    return true;
                }
                return false;
            }
            catch
            {
                Debug.LogError("Load faild");
                return false;
            }
        }
    }
}