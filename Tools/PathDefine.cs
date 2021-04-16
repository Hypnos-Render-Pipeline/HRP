using UnityEngine;

namespace HypnosRenderPipeline.Tools
{
    public class PathDefine
    {

        static string package_path = "Packages/com.hjk.hypnos/";
        static string asset_path = "Assets/HRP/";

        static bool check_done = false;
        static bool inPackage;

        public static bool release
        {
            get
            {
#if UNITY_EDITOR
                return path != asset_path;
#else
                return true;
#endif
            }
        }

        public static string path
        {
            get
            {
                if (!check_done)
                {
#if UNITY_EDITOR
                    try
                    {
                        var query_task = UnityEditor.PackageManager.Client.List(true, false);
                        while (!query_task.IsCompleted) System.Threading.Thread.Sleep(1);
                        inPackage = false;
                        foreach (var pack in query_task.Result)
                        {
                            if (pack.name == "com.hjk.hypnos")
                                inPackage = true;
                        }
                        var obj = Resources.Load<PathObject>("path");
                        obj.inPackage = inPackage;
                        UnityEditor.EditorUtility.SetDirty(obj);
                    }
                    catch (System.Exception e)
                    {
                        System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);
                        inPackage = !st.GetFrame(0).GetFileName().Contains("Assets");
                    }
#else
                    inPackage = Resources.Load<PathObject>("path").inPackage; 
#endif
                    check_done = true;
                }
                return inPackage ? package_path : asset_path;
            }
        }
    }
}