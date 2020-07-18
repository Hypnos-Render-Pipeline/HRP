using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;


namespace HypnosRenderPipeline.RenderPass
{
    public abstract class BaseNodePin<Desc, Handle>
    {
        public Desc desc;
        public Handle handle { internal set; get; }

        public abstract void AllocateResourcces(RenderContext renderContext, int id);
        public abstract bool Compare<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin);
        public abstract void MoveFrom<T2, T3>(BaseNodePin<T2, T3> pin);
        public abstract bool CanCastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin);
        public abstract void CastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin);

        public static bool CompareType<T2, T3>()
        {
            if (typeof(T2) is Desc && typeof(T3) is Handle) return true;
            return false;
        }
    }


    public class TexturePin : BaseNodePin<TexturePin.TexturePinDesc, int>
    {
        public struct TexturePinDesc
        {
            public RenderTextureDescriptor basicDesc;
            public enum SizeCastMode { ResizeToInput = 0, Fixed };
            public SizeCastMode sizeMode;
            public enum ColorCastMode { FitToInput = 0, Fixed };
            public ColorCastMode colorMode;
            public enum SizeScale { Full = 1, Half = 2, Quater = 4, Eighth = 8, Custom = 0 };
            public SizeScale sizeScale;

            public TexturePinDesc(RenderTextureDescriptor descriptor, SizeCastMode sizeCastMode = SizeCastMode.ResizeToInput, ColorCastMode colorCastMode = ColorCastMode.FitToInput, SizeScale sizeScale = SizeScale.Full)
            {
                basicDesc = descriptor;
                sizeMode = sizeCastMode;
                colorMode = colorCastMode;
                this.sizeScale = sizeScale;
            }
        }

        public TexturePin(TexturePin.TexturePinDesc desc)
        {
            this.desc = desc;
        }

        public override void AllocateResourcces(RenderContext context, int id)
        {
            if (desc.sizeScale != TexturePinDesc.SizeScale.Custom)
            {
                desc.basicDesc.width = context.RenderCamera.pixelWidth / (int)desc.sizeScale;
                desc.basicDesc.height = context.RenderCamera.pixelHeight / (int)desc.sizeScale;
            }

            context.CmdBuffer.GetTemporaryRT(id, desc.basicDesc);
            handle = id;
        }

        public override bool Compare<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            var desc2 = (pin as TexturePin).desc;

            Vector2Int descSize;
            if (desc.sizeScale != TexturePinDesc.SizeScale.Custom)
            {
                descSize = new Vector2Int(renderContext.RenderCamera.pixelWidth, renderContext.RenderCamera.pixelHeight);
                descSize /= (int)desc.sizeScale;
            }
            else descSize = new Vector2Int(desc.basicDesc.width, desc.basicDesc.height);

            if (descSize != new Vector2Int(desc2.basicDesc.width, desc2.basicDesc.height)) return false;

            if (desc.basicDesc.dimension != desc2.basicDesc.dimension
                || desc.basicDesc.colorFormat != desc2.basicDesc.colorFormat
                || desc.basicDesc.enableRandomWrite != desc2.basicDesc.enableRandomWrite
                || desc.basicDesc.volumeDepth != desc2.basicDesc.volumeDepth)
                return false;

            return true;
        }
        public override void MoveFrom<T2, T3>(BaseNodePin<T2, T3> pin)
        {
            var src = pin as TexturePin;
            handle = src.handle;
            desc = src.desc;
        }
        public override bool CanCastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            var desc2 = (pin as TexturePin).desc;
            if (desc.basicDesc.dimension != desc2.basicDesc.dimension
                || desc.basicDesc.volumeDepth != desc2.basicDesc.volumeDepth)
                return false;

            return true;
        }
        public override void CastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            renderContext.CmdBuffer.Blit((pin as TexturePin).handle, handle);
        }
    }
}
