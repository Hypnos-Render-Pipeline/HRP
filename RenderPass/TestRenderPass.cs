using HypnosRenderPipeline.RenderGraph;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public struct InputClass { }

    public class EighthScale : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin EighthResInOut = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1,1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.Fixed,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Eighth));

        public override void Excute(RenderContext context)
        {
        }
    }

    public class ClearWithColor : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public TexturePin target = new TexturePin(new TexturePin.TexturePinDesc(
                                                            new RenderTextureDescriptor(1, 1),
                                                            TexturePin.TexturePinDesc.SizeCastMode.Fixed,
                                                            TexturePin.TexturePinDesc.ColorCastMode.FitToInput,
                                                            TexturePin.TexturePinDesc.SizeScale.Eighth));

        [Tooltip("AA")]
        [ColorUsage(true, true)]
        public Color k;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.SetRenderTarget(target.handle);
            context.CmdBuffer.ClearRenderTarget(true, true, k);
        }
    }

    public class LoadTexture : BaseToolNode
    {
        [NodePin(type: PinType.Out)]
        public TexturePin FullResOutput = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1,1)));

        public Texture2D tex;

        public override void Excute(RenderContext context)
        {
            if (tex != null)
            {
                context.CmdBuffer.Blit(tex, FullResOutput.handle);
            }
        }
    }
}