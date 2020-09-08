using Unity.Mathematics;
using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class Atmo : BaseRenderPass
    {
        [NodePin(PinType.In)]
        public LightListPin sunLight = new LightListPin();

        RenderTexture t_table;
        RenderTexture j_table;
        //RenderTexture s_table;

        [NodePin(PinType.InOut, true)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        static MaterialWithName mat = new MaterialWithName("Hidden/Custom/Atmo");

        public Atmo()
        {
            t_table = Resources.Load<CustomRenderTexture>("Shaders/Atmo/Lut/T_Table/T_table");
            j_table = Resources.Load<CustomRenderTexture>("Shaders/Atmo/Lut/Loop/J_table_2");
            //s_table = Resources.Load<CustomRenderTexture>("Shaders/Atmo/Lut/Loop/S_table_0");
        }

        public override void Excute(RenderContext context)
        {
            context.commandBuffer.SetGlobalTexture("T_table", t_table);
            context.commandBuffer.SetGlobalTexture("J_table", j_table);
            //context.CmdBuffer.SetGlobalTexture("S_table", s_table);
            HRPLight sun = sunLight.handle.sunLight;
            if (sun == null)
            {
                context.commandBuffer.SetGlobalFloat("_SunRadiance", math.pow(10, 4.3f));
                context.commandBuffer.SetGlobalVector("_LightDir", Vector3.down);
            }
            else
            {
                context.commandBuffer.SetGlobalFloat("_SunRadiance", sun.radiance * math.pow(10, 4.3f));
                context.commandBuffer.SetGlobalVector("_LightDir", sun.direction);
            }

            context.commandBuffer.SetGlobalTexture("_Depth", depth);

            int nameId = 0;

            nameId = Shader.PropertyToID("TempSceneColor");
            context.commandBuffer.GetTemporaryRT(nameId, target.desc.basicDesc);
            context.commandBuffer.Blit(target, nameId);
            context.commandBuffer.Blit(nameId, target, mat);
            context.commandBuffer.ReleaseTemporaryRT(nameId);
        }
    }
}