using HypnosRenderPipeline.RenderGraph;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{

    [RenderNodePath("RenderPass")]
    public abstract class BaseRenderPass : BaseRenderNode {
        public virtual void Render()
        {

        }
    }


    [RenderNodePath("ToolNodes")]
    public abstract class BaseToolNode : BaseRenderNode
    {
        public virtual void Excute()
        {

        }
    }
}
