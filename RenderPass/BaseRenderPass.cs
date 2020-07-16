using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using HypnosRenderPipeline.RenderGraph;

namespace HypnosRenderPipeline.RenderPass
{
    public struct RenderContext
    {
        Camera RenderCamera;
        CommandBuffer CmdBuffer;
        ScriptableRenderContext Context;
    }


    [RenderNodePath("RenderPass")]
    public abstract class BaseRenderPass : BaseRenderNode 
    {
        public string name { get; protected set; }

        //public ProfilingSampler ProfileSampler { get; protected set; }


        public virtual void Init(RenderContext RenderingContext) { }

        public virtual void OnRender(RenderContext RenderingContext) { }

        public virtual void Release(RenderContext RenderingContext) { }
    }


    [RenderNodePath("ToolNodes")]
    public abstract class BaseToolNode : BaseRenderNode
    {
        public virtual void Excute()
        {

        }
    }
}
