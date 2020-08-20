using System.Collections.Generic;
using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public abstract class BaseNodePin<Desc, Handle> where Handle : new()
    {
        internal sealed class Pool
        {
            Stack<Handle> pool;
            private Pool() { pool = new Stack<Handle>(); }
            private class Nested { static Nested() { } internal static readonly Pool instance = new Pool(); }
            private static Pool instance { get { return Nested.instance; } }

            public static Handle Get()
            {
                var pool = instance.pool;
                if (pool.Count != 0) return pool.Pop();
                return new Handle();
            }
            public static void Release(Handle ll)
            {
                instance.pool.Push(ll);
            }
        }

        public string name;
        public Desc desc;
        public Handle handle { internal set; get; }

        public virtual void Move(BaseNodePin<Desc, Handle> pin) { desc = pin.desc; handle = pin.handle; name = pin.name; }

        public virtual void AllocateResourcces(RenderContext renderContext, int id) { handle = Pool.Get(); }
        public virtual void ReleaseResourcces(RenderContext renderContext) { Pool.Release(handle); }
        public virtual bool Compare(RenderContext renderContext, BaseNodePin<Desc, Handle> pin) { return true; }
        public virtual bool CanCastFrom(RenderContext renderContext, BaseNodePin<Desc, Handle> pin) { return true; }
        public virtual void CastFrom(RenderContext renderContext, BaseNodePin<Desc, Handle> pin) { desc = pin.desc; handle = pin.handle; }

        public static bool CompareType()
        {
            if (typeof(Desc) is Desc && typeof(Handle) is Handle) return true;
            return false;
        }
    }

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

    public class TexturePin : BaseNodePin<TexturePinDesc, int>
    {

        public TexturePin(TexturePinDesc desc)
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

        public override bool Compare(RenderContext renderContext, BaseNodePin<TexturePinDesc, int> pin)
        {
            var desc2 = pin.desc;

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
        public override void Move(BaseNodePin<TexturePinDesc, int> pin) { 
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
                renderContext.CmdBuffer.Blit(from, handle, MaterialWithName.depthBlit);
            }
            else
            {
                renderContext.CmdBuffer.Blit(from, handle);
            }
        }
    }

    //public class BufferPin<T> : BaseNodePin<BufferPin<T>.BufferPinDesc, FRGBufferHandle>
    //{
    //    public struct BufferPinDesc
    //    {
    //        public delegate int SizeDelegate();
    //        public SizeDelegate sizeDelegate;

    //        public enum SizeMode { FitToInput = 0, Fixed };

    //        public SizeMode sizeMode;

    //        public BufferPinDesc(SizeDelegate sizeDelegate, SizeMode sizeMode = SizeMode.FitToInput) { this.sizeDelegate = sizeDelegate; this.sizeMode = sizeMode; }
    //    }

    //    public FRGBufferDesc bufferDesc;

    //    public BufferPin(BufferPinDesc.SizeDelegate sizeDelegate)
    //    {
    //        this.desc = new BufferPinDesc(sizeDelegate);
    //    }

    //    public override void AllocateResourcces(RenderContext context, int id)
    //    {
    //        bufferDesc = new FRGBufferDesc(desc.sizeDelegate(), Marshal.SizeOf<T>());
    //        //context.ResourcePool.GetBuffer(res_desc, id);
    //        handle = new FRGBufferHandle(id);
    //    }

    //    public override void Move(BaseNodePin<BufferPinDesc, FRGBufferHandle> pin)
    //    {
    //        var from = (pin as BufferPin<T>);
    //        bufferDesc = from.bufferDesc;
    //        handle = from.handle;
    //    }

    //    public override void ReleaseResourcces(RenderContext context)
    //    {
    //        context.ResourcePool.ReleaseBuffer(handle);
    //    }

    //    public override bool Compare(RenderContext renderContext, BaseNodePin<BufferPin<T>.BufferPinDesc, FRGBufferHandle> pin)
    //    {
    //        var from = (pin as BufferPin<T>);

    //        if (desc.sizeMode != BufferPinDesc.SizeMode.FitToInput)
    //        {
    //            if (desc.sizeDelegate() > from.bufferDesc.count) return false;
    //        }
                        
    //        return true;
    //    }

    //    public override bool CanCastFrom(RenderContext renderContext, BaseNodePin<BufferPin<T>.BufferPinDesc, FRGBufferHandle> pin)
    //    {
    //        return false;
    //    }
    //    public override void CastFrom<T2, T3>(RenderContext renderContext, BaseNodePin<BufferPin<T>.BufferPinDesc, FRGBufferHandle> pin)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
