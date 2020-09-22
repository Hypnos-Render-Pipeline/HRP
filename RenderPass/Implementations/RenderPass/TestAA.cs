using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class TestAA : BaseRenderPass
    {
        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1));

        [Range(1, 20)]
        public int loop = 1;

        static MaterialWithName mat = new MaterialWithName("Hidden/AA");

        static Texture2D lut;

        public TestAA()
        {
            lut = Resources.Load<Texture2D>("Textures/AALut");
        }

        public override void Excute(RenderContext context)
        {
            var cam = context.camera;

            int tmpTex = Shader.PropertyToID("_Tex");

            context.commandBuffer.GetTemporaryRT(tmpTex, target.desc.basicDesc);
            context.commandBuffer.Blit(target, tmpTex, mat, 1);

            context.commandBuffer.SetGlobalTexture("_AALut", lut);

            context.commandBuffer.Blit(tmpTex, target);

            for (int i = 0; i < loop; i++)
            {
                context.commandBuffer.Blit(target, tmpTex, mat, 0);
                context.commandBuffer.Blit(tmpTex, target, mat, 0);
            }
        }
    }
}