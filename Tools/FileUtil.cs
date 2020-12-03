namespace HypnosRenderPipeline
{
#if UNITY_EDITOR
    public class FileUtil {

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public class OpenFileName
        {
            public int structSize = 0;
            public System.IntPtr dlgOwner = System.IntPtr.Zero;
            public System.IntPtr instance = System.IntPtr.Zero;
            public System.String filter = null;
            public System.String customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public System.String file = null;
            public int maxFile = 0;
            public System.String fileTitle = null;
            public int maxFileTitle = 0;
            public System.String initialDir = null;
            public System.String title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public System.String defExt = null;
            public System.IntPtr custData = System.IntPtr.Zero;
            public System.IntPtr hook = System.IntPtr.Zero;
            public System.String templateName = null;
            public System.IntPtr reservedPtr = System.IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [System.Runtime.InteropServices.DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool GetOpenFileName([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn);

        [System.Runtime.InteropServices.DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool GetSaveFileName([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn);

        public static T SaveAssetInProject<T>() where T : UnityEngine.ScriptableObject
        {
            string filePath = UnityEditor.EditorUtility.SaveFilePanelInProject("", "New " + typeof(T).Name, "asset", "Create new " + typeof(T).Name);
            if (!string.IsNullOrEmpty(filePath))
            {
                var old_asset = UnityEditor.AssetDatabase.LoadAssetAtPath(filePath, typeof(T));
                if (old_asset != null)
                {
                    UnityEditor.AssetDatabase.DeleteAsset(filePath);
                }
                var obj = UnityEngine.ScriptableObject.CreateInstance<T>();
                UnityEditor.AssetDatabase.CreateAsset(obj, filePath);
                return obj;
            }
            return null;
        }
    }
#endif
}
