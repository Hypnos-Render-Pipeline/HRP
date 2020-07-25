using System;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.RenderGraph;

namespace HypnosRenderPipeline.RenderPass
{
    public struct RenderContext
    {
        public Camera RenderCamera;
        public CommandBuffer CmdBuffer;
        public ScriptableRenderContext Context;
        public RenderGraphResourcePool ResourcePool;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RenderNodeTypeAttribute : Attribute
    {
        public enum Type { RenderPass, ToolNode, OutputNode };
        public Type type;
        public RenderNodeTypeAttribute(Type type)
        {
            this.type = type;
        }
    }

    [RenderNodePath("RenderPass")]
    [NodeColor(0, 0.2f, 1, 0.4f)]
    [RenderNodeType(RenderNodeTypeAttribute.Type.RenderPass)]
    public abstract class BaseRenderPass : BaseRenderNode 
    {
    }


    [RenderNodePath("ToolNodes")]
    [NodeColor(1, 1, 0.2f, 0.4f)]
    [RenderNodeType(RenderNodeTypeAttribute.Type.ToolNode)]
    public abstract class BaseToolNode : BaseRenderNode
    {
    }


    [RenderNodePath("OutputNodes")]
    [NodeColor(1f, 0.2f, 0.2f, 0.4f)]
    [RenderNodeType(RenderNodeTypeAttribute.Type.OutputNode)]
    public abstract class BaseOutputNode : BaseRenderNode
    {
        [NodePin(PinType.In, true)]
        [Tooltip("Output to screen")]
        public TexturePin result = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1,1)));

        [HideInInspector]
        public RenderTargetIdentifier target;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.Blit(result.handle, target);
        }
    }


    [NodeColor(1, 0, 1, 0.5f)]
    [RenderNodePath("ToolNodes/Debug", true)]
    [RenderNodeInformation("Use this to debug texture, drag from output pin to create it.")]
    public class TextureDebug : BaseToolNode
    {
        [NodePin(type: PinType.In)]
        [PinColor(1,0,1,1)]
        public TexturePin tex = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1, 1)));


        public RenderTexture texture;

        public override void Excute(RenderContext context)
        {
            if (texture != null)
            {
                context.CmdBuffer.Blit(tex.handle, texture);
            }
        }
    }

    public class OutputNode : BaseOutputNode
    {
        public override void Excute(RenderContext context)
        {
            base.Excute(context);
        }
    }
}
