

namespace HypnosRenderPipeline
{
#if UNITY_2020_2_OR_NEWER
    [UnityEditor.AssetImporters.ScriptedImporter(1, ".ies")]
    public class IESImporter : UnityEditor.AssetImporters.ScriptedImporter
#else
    [UnityEditor.Experimental.AssetImporters.ScriptedImporter(1, ".ies")]
    public class IESImporter : UnityEditor.Experimental.AssetImporters.ScriptedImporter
#endif
    {
        public int resolution = 256;

        public string version;

        public int vertAngle;
        public int horizAngle;
        public float candela;


#if UNITY_2020_2_OR_NEWER
        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
#else
        public override void OnImportAsset(UnityEditor.Experimental.AssetImporters.AssetImportContext ctx)
#endif
        {
            var parser = new IESParser(ctx.assetPath);
            var cubemap = parser.RenderCubemap(resolution);

            vertAngle = parser.NumVertAngles;
            horizAngle = parser.NumHorizAngles;
            version = parser.IESversion;
            candela = parser.maxCandelas;

            ctx.AddObjectToAsset("Cubemap", cubemap); 

            ctx.SetMainObject(cubemap);
        }
    }
}
