//using UnityEditor;


//namespace HypnosRenderPipeline
//{

//    [CustomEditor(typeof(IESImporter))]
//#if UNITY_2020_2_OR_NEWER
//    class IESInspector : UnityEditor.AssetImporters.AssetImporterEditor
//#else
//    class IESInspector : UnityEditor.Experimental.AssetImporters.AssetImporterEditor
//#endif
//    {
//        protected override bool needsApplyRevert => true;
//        public override bool showImportedObject => false;

//        public override void OnInspectorGUI()
//        {
//            var ies = target as IESImporter;

//            EditorGUI.BeginDisabledGroup(true);

//            EditorGUILayout.LabelField("Version", ies.version);

//            EditorGUILayout.IntField("Horizontal angle count", ies.horizAngle);
//            EditorGUILayout.IntField("Vertical angle count", ies.vertAngle);

//            EditorGUILayout.FloatField("Candela", ies.candela);

//            EditorGUI.EndDisabledGroup();

//            EditorGUI.BeginChangeCheck();
//            ies.resolution = EditorGUILayout.IntPopup("Resolution", ies.resolution, new string[] { "16", "32", "64", "128", "256", "512" }, new int[] { 16, 32, 64, 128, 256, 512 });
//            if (EditorGUI.EndChangeCheck())
//            {
//                EditorUtility.SetDirty(ies);
//            }

//            ApplyRevertGUI();
//        }
//    }
//}
