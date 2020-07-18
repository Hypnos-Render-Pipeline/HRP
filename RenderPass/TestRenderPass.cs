using HypnosRenderPipeline.RenderGraph;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public struct InputClass { }

    public class TestRenderPass : BaseRenderPass
    {
        [NodePin(PinType.InOut, true)]
        public TexturePin MustConnectedInOut = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1,1)));


        [Tooltip("AA")]
        public int k;

        public override void Excute(RenderContext context)
        {
            Debug.Log("TestRenderPass: Render " + k.ToString());
        }
    }

    [RenderNodePath("AAA")]
    public class TestRenderPass2 : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin EighthResInOut = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1,1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.Fixed,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Eighth));

        [Tooltip("AA")]
        [ColorUsage(true, true)]
        public Color k;

        public override void Excute(RenderContext context)
        {
            Debug.Log("TestRenderPass2: Render " + k.ToString());
        }
    }


    [RenderNodePath("AAA")]
    public class ClearWithColor : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin EighthResInOut = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1, 1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.Fixed,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Eighth));

        [Tooltip("AA")]
        [ColorUsage(true, true)]
        public Color k;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(EighthResInOut.handle);
            context.CmdBuffer.ClearRenderTarget(true, true, k);
        }
    }

    public class TestRenderNode : BaseToolNode
    {
        [NodePin(type: PinType.Out)]
        public TexturePin FullResOutput = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1,1)));

        public Texture2D tex;

        public override void Excute(RenderContext context)
        {
            Debug.Log("TestRenderNode: Generate output");
            if (tex != null)
            {
                context.CmdBuffer.Blit(tex, FullResOutput.handle);
            }
        }
    }

    public class TextureDebug : BaseToolNode
    {
        [NodePin(type: PinType.In, true)]
        public TexturePin tex = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1, 1)));


        public RenderTexture texture;

        public override void Excute(RenderContext context)
        {
            if (texture != null)
            {
                context.CmdBuffer.Blit(tex.handle, texture);
            }
        }
    }


    public class TestOutputNode : BaseOutputNode
    {
        //[NodePin(type: PinType.In)]
        //public TexturePin HalfResInput = new TexturePin(new TexturePin.TexturePinDesc(
        //                                                    new RenderTextureDescriptor(1,1, RenderTextureFormat.Default)
        //                                                ));

        public override void Excute(RenderContext context)
        {
            base.Excute(context);
            Debug.Log("TestOutputNode: Output to screen " + target.ToString());
        }
    }
}