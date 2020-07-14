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
        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
        public class NodePinAttribute : Attribute
        {
            public bool inPin { private set; get; } = true;
            public bool outPin { private set; get; } = true;
            public bool mustConnect { private set; get; } = false;
            public NodePinAttribute(bool inPin = true, bool outPin = true, bool mustConnect = false)
            {
                this.inPin = inPin;
                this.outPin = outPin;
                this.mustConnect = mustConnect;
            }
        }

        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
        public class MustConnectAttribute : Attribute { }

        internal virtual void Init() { }
        internal virtual void Release() { }
    }
}