using System.Linq;
using System.Threading;

namespace HypnosRenderPipeline
{
    public class PathDefine
    {
        static bool check_done = false;
        static bool inPackage;

        static string package_path = "Packages/com.hjk.hypnos/";
        static string asset_path = "Assets/HRP/";

        public static string path { get {
                if (!check_done)
                {
                    var query_task = UnityEditor.PackageManager.Client.List(true, false);
                    while (!query_task.IsCompleted) Thread.Sleep(1);
                    inPackage = false;
                    foreach (var pack in query_task.Result)
                    {
                        if (pack.name == "com.hjk.hypnos")
                            inPackage = true;
                    }
                    check_done = true;
                }
                return inPackage ? package_path : asset_path; 
            } }// = ;
    }
}