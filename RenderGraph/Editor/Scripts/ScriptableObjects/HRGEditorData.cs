using UnityEditor;
using UnityEngine;

public class HRGEditorData : ScriptableObject
{
    public string lastOpenPath;


    [InitializeOnLoadMethod]
    static void InitializeOnLoadMethod()
    {
        EditorApplication.wantsToQuit -= Quit;
        EditorApplication.wantsToQuit += Quit;
    }
       
    static bool Quit()
    {
        var self = AssetDatabase.LoadAssetAtPath<HRGEditorData>(HypnosRenderPipeline.PathDefine.path + "RenderGraph/Editor/EditorData.asset");
        self.lastOpenPath = "unnamed";
        EditorUtility.SetDirty(self);
        AssetDatabase.Refresh();
        return true;
    }
}
