using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{

    public enum SizeCastMode { ResizeToInput = 0, Fixed };
    public enum ColorCastMode { FitToInput = 0, Fixed };
    public enum SizeScale { Full = 1, Half = 2, Quater = 4, Eighth = 8, Custom = 0 };
    public struct TexturePinDesc
    {
        public RenderTextureDescriptor basicDesc;
        public SizeCastMode sizeMode;
        public ColorCastMode colorMode;
        public SizeScale sizeScale;

        public TexturePinDesc(RenderTextureDescriptor descriptor, SizeCastMode sizeCastMode = SizeCastMode.ResizeToInput, ColorCastMode colorCastMode = ColorCastMode.FitToInput, SizeScale sizeScale = SizeScale.Full)
        {
            basicDesc = descriptor;
            sizeMode = sizeCastMode;
            colorMode = colorCastMode;
            this.sizeScale = sizeScale;
        }
    }

    public class TexturePin : BaseNodePin<TexturePinDesc, int>
    {
        public TexturePin(TexturePinDesc desc)
        {
            this.desc = desc;
        }
        public TexturePin(RenderTextureDescriptor descriptor, SizeCastMode sizeCastMode = SizeCastMode.ResizeToInput, ColorCastMode colorCastMode = ColorCastMode.FitToInput, SizeScale sizeScale = SizeScale.Full)
        {
            desc = new TexturePinDesc(descriptor, sizeCastMode, colorCastMode, sizeScale);
        }

        public static implicit operator RenderTargetIdentifier(TexturePin self)
        {
            return self.handle;
        }

        public override void AllocateResourcces(RenderContext context, int id)
        {
            if (desc.sizeScale != SizeScale.Custom)
            {
                desc.basicDesc.width = context.camera.pixelWidth / (int)desc.sizeScale;
                desc.basicDesc.height = context.camera.pixelHeight / (int)desc.sizeScale;
            }

            context.commandBuffer.GetTemporaryRT(id, desc.basicDesc);
            handle = id;
        }
        public override void ReleaseResourcces(RenderContext context)
        {
            context.commandBuffer.ReleaseTemporaryRT(handle);
        }

        public override bool Compare(RenderContext renderContext, BaseNodePin<TexturePinDesc, int> pin)
        {
            var desc2 = pin.desc;

            if (desc.sizeMode == SizeCastMode.Fixed)
            {
                Vector2Int descSize;
                if (desc.sizeScale != SizeScale.Custom)
                {
                    descSize = new Vector2Int(renderContext.camera.pixelWidth, renderContext.camera.pixelHeight);
                    descSize /= (int)desc.sizeScale;
                }
                else descSize = new Vector2Int(desc.basicDesc.width, desc.basicDesc.height);

                if (descSize != new Vector2Int(desc2.basicDesc.width, desc2.basicDesc.height)) return false;
            }

            if (desc.colorMode != ColorCastMode.FitToInput)
            {
                if (desc.basicDesc.colorFormat != desc2.basicDesc.colorFormat) return false;
            }

            if (desc.basicDesc.dimension != desc2.basicDesc.dimension
                || (desc.basicDesc.enableRandomWrite && !desc2.basicDesc.enableRandomWrite)
                || desc.basicDesc.volumeDepth != desc2.basicDesc.volumeDepth
                || desc.basicDesc.depthBufferBits > desc2.basicDesc.depthBufferBits)
                return false;

            return true;
        }
        public override void Move(BaseNodePin<TexturePinDesc, int> pin)
        {
            desc.basicDesc = pin.desc.basicDesc;
            handle = pin.handle;
            name = pin.name;
        }

        public override bool CanCastFrom(RenderContext renderContext, BaseNodePin<TexturePinDesc, int> pin)
        {
            var desc2 = pin.desc;
            if (desc.basicDesc.dimension != desc2.basicDesc.dimension
                || desc.basicDesc.volumeDepth != desc2.basicDesc.volumeDepth)
                return false;

            return true;
        }

        public override void CastFrom(RenderContext renderContext, BaseNodePin<TexturePinDesc, int> pin)
        {
            var from = pin.handle;

            if ((pin as TexturePin).desc.basicDesc.colorFormat == RenderTextureFormat.Depth)
            {
                renderContext.commandBuffer.Blit(from, handle, MaterialWithName.depthBlit);
            }
            else
            {
                renderContext.commandBuffer.Blit(from, handle);
            }
        }
    }
}