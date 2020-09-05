using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

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

        /// <summary>
        /// <para>Whether this pin is connected to an *enabled* parent node and thus contains valid data,</para>
        /// <para>so as you don't need to do necessary init of the pin resources. For example, TexturePin</para>
        /// <para>with 'connected == true' means it contains valid pixel data from parent node, otherwise</para>
        /// <para>you may want to clear it before starting to use it, this is determined by its usage.   </para>
        /// </summary>
        public bool connected;

        /// <summary>
        /// Name of this pin.
        /// </summary>
        public string name;
        
        public Desc desc;

        public Handle handle { internal set; get; }

        public static implicit operator Handle(BaseNodePin<Desc, Handle> self)
        {
            return self.handle;
        }

        public virtual void Move(BaseNodePin<Desc, Handle> pin) { connected = pin.connected; desc = pin.desc; handle = pin.handle; name = pin.name; }

        public virtual void AllocateResourcces(RenderContext renderContext, int id) { handle = Pool.Get(); }
        public virtual void ReleaseResourcces(RenderContext renderContext) { Pool.Release(handle); }
        public virtual bool Compare(RenderContext renderContext, BaseNodePin<Desc, Handle> pin) { return true; }
        public virtual bool CanCastFrom(RenderContext renderContext, BaseNodePin<Desc, Handle> pin) { return true; }
        public virtual void CastFrom(RenderContext renderContext, BaseNodePin<Desc, Handle> pin) { connected = pin.connected; desc = pin.desc; handle = pin.handle; }

        public static bool CompareType()
        {
            if (typeof(Desc) is Desc && typeof(Handle) is Handle) return true;
            return false;
        }

        public virtual void Dispose() { }
    }
}
