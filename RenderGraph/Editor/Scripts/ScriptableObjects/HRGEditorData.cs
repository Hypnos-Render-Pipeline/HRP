using UnityEditor;
using UnityEngine;
using HypnosRenderPipeline.Tools;

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
        var self = AssetDatabase.LoadAssetAtPath<HRGEditorData>(PathDefine.path + "RenderGraph/Editor/EditorData.asset");
        self.lastOpenPath = "unnamed";
        EditorUtility.SetDirty(self);
        AssetDatabase.Refresh();
        return true;
    }
}
