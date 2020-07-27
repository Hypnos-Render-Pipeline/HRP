using HypnosRenderPipeline.RenderGraph;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public abstract class BaseNodePin<Desc, Handle>
    {
        public string name;
        public Desc desc;
        public Handle handle { internal set; get; }

        public virtual void Move(BaseNodePin<Desc, Handle> pin) { desc = pin.desc; handle = pin.handle; name = pin.name; }

        public abstract void AllocateResourcces(RenderContext renderContext, int id);
        public abstract void ReleaseResourcces(RenderContext renderContext);
        public abstract bool Compare<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin);
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
        public override void ReleaseResourcces(RenderContext context)
        {
            context.CmdBuffer.ReleaseTemporaryRT(handle);
        }

        public override bool Compare<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            var desc2 = (pin as TexturePin).desc;

            if (desc.sizeMode == TexturePinDesc.SizeCastMode.Fixed)
            {
                Vector2Int descSize;
                if (desc.sizeScale != TexturePinDesc.SizeScale.Custom)
                {
                    descSize = new Vector2Int(renderContext.RenderCamera.pixelWidth, renderContext.RenderCamera.pixelHeight);
                    descSize /= (int)desc.sizeScale;
                }
                else descSize = new Vector2Int(desc.basicDesc.width, desc.basicDesc.height);

                if (descSize != new Vector2Int(desc2.basicDesc.width, desc2.basicDesc.height)) return false;
            }

            if (desc.colorMode != TexturePinDesc.ColorCastMode.FitToInput)
            {
                if (desc.basicDesc.colorFormat != desc2.basicDesc.colorFormat) return false;
            }

            if (desc.basicDesc.dimension != desc2.basicDesc.dimension
                || desc.basicDesc.enableRandomWrite != desc2.basicDesc.enableRandomWrite
                || desc.basicDesc.volumeDepth != desc2.basicDesc.volumeDepth
                || desc.basicDesc.depthBufferBits > desc2.basicDesc.depthBufferBits)
                return false;

            return true;
        }

        public override bool CanCastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            var desc2 = (pin as TexturePin).desc;
            if (desc.basicDesc.dimension != desc2.basicDesc.dimension
                || desc.basicDesc.volumeDepth != desc2.basicDesc.volumeDepth
                || desc.basicDesc.depthBufferBits != desc2.basicDesc.depthBufferBits) //todo: blit depth
                return false;

            return true;
        }
        public override void CastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            var from = (pin as TexturePin).handle;
            renderContext.CmdBuffer.Blit(from, handle);

            //todo: blit depth
            //if ((pin as TexturePin).desc.basicDesc.depthBufferBits != 0)
            //{
            //    renderContext.CmdBuffer.SetShadowSamplingMode(from, ShadowSamplingMode.RawDepth);
            //    renderContext.CmdBuffer.Blit(from, handle);
            //    renderContext.CmdBuffer.SetShadowSamplingMode(from, ShadowSamplingMode.None);
            //}
        }
    }


    public class BufferPin<T> : BaseNodePin<BufferPin<T>.BufferPinDesc, FRGBufferHandle>
    {
        public struct BufferPinDesc
        {
            public delegate int SizeDelegate();
            public SizeDelegate sizeDelegate;

            public enum SizeMode { FitToInput = 0, Fixed };

            public SizeMode sizeMode;

            public BufferPinDesc(SizeDelegate sizeDelegate, SizeMode sizeMode = SizeMode.FitToInput) { this.sizeDelegate = sizeDelegate; this.sizeMode = sizeMode; }
        }

        public FRGBufferDesc bufferDesc;

        public BufferPin(BufferPinDesc.SizeDelegate sizeDelegate)
        {
            this.desc = new BufferPinDesc(sizeDelegate);
        }

        public override void AllocateResourcces(RenderContext context, int id)
        {
            bufferDesc = new FRGBufferDesc(desc.sizeDelegate(), Marshal.SizeOf<T>());
            //context.ResourcePool.GetBuffer(res_desc, id);
            handle = new FRGBufferHandle(id);
        }

        public override void Move(BaseNodePin<BufferPinDesc, FRGBufferHandle> pin)
        {
            var from = (pin as BufferPin<T>);
            bufferDesc = from.bufferDesc;
            handle = from.handle;
        }

        public override void ReleaseResourcces(RenderContext context)
        {
            context.ResourcePool.ReleaseBuffer(handle);
        }

        public override bool Compare<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            var from = (pin as BufferPin<T>);

            if (desc.sizeMode != BufferPinDesc.SizeMode.FitToInput)
            {
                if (desc.sizeDelegate() > from.bufferDesc.count) return false;
            }
                        
            return true;
        }

        public override bool CanCastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            return false;
        }
        public override void CastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<T2, T3> pin)
        {
            throw new NotImplementedException();
        }
    }
}
