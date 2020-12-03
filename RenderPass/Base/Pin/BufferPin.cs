using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{
    public class Buffer
    {
        public ComputeBuffer buffer;
    }

    public class BufferPin<T> : BaseNodePin<int, Buffer> where T : struct
    {
        int stride;

        public BufferPin(int size)
        {
            stride = Marshal.SizeOf<T>();
            desc = size;
        }

        public static implicit operator ComputeBuffer(BufferPin<T> self)
        {
            return self.handle.buffer;
        }

        /// <summary>
        /// Resize buffer to given size.
        /// </summary>
        /// <param name="size"></param>
        public void ReSize(int size)
        {
            desc = size;
            int k = handle.buffer.count;
            if (size > k)
            {
                while (k < size) k <<= 1;
                handle.buffer.Release();
                handle.buffer = new ComputeBuffer(k, stride);
            }
            else if (size < k / 2)
            {
                while (k > size) k >>= 1;
                if (k < size) k <<= 1;
                k = k == 0 ? 1 : k;
                handle.buffer.Release();
                handle.buffer = new ComputeBuffer(k, stride);
            }
        }

        /// <summary>
        /// Set data of buffer
        /// </summary>
        /// <param name="data"></param>
        public void SetData(List<T> data)
        {
            handle.buffer.SetData(data);
        }

        public override void Dispose()
        {
            if (handle != null)
            {
                if (handle.buffer != null)
                    handle.buffer.Release();
                handle.buffer = null;
                handle = null;
            }
        }

        public override void AllocateResourcces(RenderContext context, int id)
        {
            if (handle == null)
            {
                handle = new Buffer();
                handle.buffer = new ComputeBuffer(desc, stride);
            }
        }

        public override bool Compare(BaseNodePin<int, Buffer> pin)
        {
            return stride == (pin as BufferPin<T>).stride;
        }

        public override bool CanCastFrom(BaseNodePin<int, Buffer> pin)
        {
            return false;
        }
        public override void CastFrom(RenderContext renderContext, BaseNodePin<int, Buffer> pin)
        {
            throw new System.NotImplementedException();
        }
    }
}