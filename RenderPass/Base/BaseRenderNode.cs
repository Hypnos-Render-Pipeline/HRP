using HypnosRenderPipeline.RenderPass;
using System;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RenderNodePathAttribute : Attribute
    {
        public string path { private set; get; } = string.Empty;
        public bool hidden = false;
        public RenderNodePathAttribute(string path, bool hidden = false)
        {
            this.path = path;
            this.hidden = hidden;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RenderNodeInformationAttribute : Attribute
    {
        public string info { private set; get; } = string.Empty;
        public RenderNodeInformationAttribute(string info)
        {
            this.info = info;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class NodeColorAttribute : Attribute
    {
        public Color color { private set; get; } = Color.clear;
        public NodeColorAttribute(float r,float g, float b, float a)
        {
            this.color = new Color(r,g,b,a);
        }
    }

    [RenderNodePath("")]
    [RenderNodeInformation("")]
    [NodeColor(1, 1, 1, 0)]
    public abstract class BaseRenderNode
    {
        public enum PinType { In, Out, InOut };

        [Tooltip("Should this node be excuted.")]
        public bool enabled = true;

        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
        public class NodePinAttribute : Attribute
        {
            public PinType type { private set; get; } = PinType.InOut;
            public bool mustConnect { private set; get; } = false;
            public NodePinAttribute(PinType type = PinType.InOut, bool mustConnect = false)
            {
                this.type = type;
                this.mustConnect = mustConnect;
            }
        }

        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
        public class PinColorAttribute : Attribute
        {
            public Color color { private set; get; } = Color.clear;
            public PinColorAttribute(float r, float g, float b, float a)
            {
                this.color = new Color(r, g, b, a);
            }
        }

        /// <summary>
        /// This will be called when node is enabled.
        /// </summary>
        /// <param name="context"></param>
        public abstract void Execute(RenderContext context);

        /// <summary>
        /// This will be called when node is disabled.
        /// </summary>
        /// <param name="context"></param>
        public virtual void DisExecute(RenderContext context) { }

        public virtual void Dispose() { }
    }
}