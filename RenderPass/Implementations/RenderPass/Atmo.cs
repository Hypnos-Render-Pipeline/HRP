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
        RenderTexture s_table;

        [NodePin(PinType.InOut)]
        public TexturePin target = new TexturePin(new RenderTextureDescriptor(1, 1));

        [NodePin(PinType.In, true)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        static MaterialWithName mat = new MaterialWithName("Hidden/Custom/Atmo");

        public Atmo()
        {
            t_table = Resources.Load<CustomRenderTexture>("Shaders/Atmo/Lut/T_Table/T_table");
            j_table = Resources.Load<CustomRenderTexture>("Shaders/Atmo/Lut/Loop/J_table_2");
            s_table = Resources.Load<CustomRenderTexture>("Shaders/Atmo/Lut/Loop/S_table_0");
        }

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetGlobalTexture("T_table", t_table);
            context.CmdBuffer.SetGlobalTexture("J_table", j_table);
            context.CmdBuffer.SetGlobalTexture("S_table", s_table);
            HRPLight sun = sunLight.handle.sunLight;
            if (sun == null)
            {
                context.CmdBuffer.SetGlobalFloat("_SunRadiance", math.pow(10, 4.3f));
                context.CmdBuffer.SetGlobalVector("_LightDir", Vector3.down);
            }
            else
            {
                context.CmdBuffer.SetGlobalFloat("_SunRadiance", sun.radiance * math.pow(10, 4.3f));
                context.CmdBuffer.SetGlobalVector("_LightDir", sun.direction);
            }

            context.CmdBuffer.SetGlobalTexture("_Depth", depth);

            int nameId = 0;
            if (target.connected)
            {
                nameId = Shader.PropertyToID("TempSceneColor");
                context.CmdBuffer.GetTemporaryRT(nameId, target.desc.basicDesc);
                context.CmdBuffer.Blit(target, nameId);
                context.CmdBuffer.Blit(nameId, target, mat);
                context.CmdBuffer.ReleaseTemporaryRT(nameId);
            }
            else
            {
                context.CmdBuffer.Blit(Texture2D.blackTexture, target, mat);
            }
        }
    }
}