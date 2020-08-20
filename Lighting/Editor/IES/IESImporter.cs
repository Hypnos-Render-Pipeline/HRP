using UnityEditor.Experimental.AssetImporters;

namespace HypnosRenderPipeline
{

    [ScriptedImporter(1, ".ies")]
    public class IESImporter : ScriptedImporter
    {
        public int resolution = 256;

        public string version;

        public int vertAngle;
        public int horizAngle;
        public float candela;


        public override void OnImportAsset(AssetImportContext ctx)
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
