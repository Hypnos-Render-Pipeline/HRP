using HypnosRenderPipeline.RenderPass;
using System;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RenderNodePathAttribute : Attribute
    {
        public string path { private set; get; } = string.Empty;
        public RenderNodePathAttribute(string path)
        {
            this.path = path;
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


    [RenderNodePath("")]
    [RenderNodeInformation("")]
    public abstract class BaseRenderNode
    {
        public enum PinType { In, Out, InOut };

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

        public virtual void Excute(RenderContext RenderingContext) { }
    }
}