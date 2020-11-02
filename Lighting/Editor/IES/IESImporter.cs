

namespace HypnosRenderPipeline
{

    [UnityEditor.AssetImporters.ScriptedImporter(1, ".ies")]
    public class IESImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        public int resolution = 256;

        public string version;

        public int vertAngle;
        public int horizAngle;
        public float candela;


        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
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
