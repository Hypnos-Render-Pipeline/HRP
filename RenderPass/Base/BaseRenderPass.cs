using HypnosRenderPipeline.RenderGraph;
using System;
using UnityEngine;
using UnityEngine.Rendering;

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
        public TexturePin result = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1,1)));

        public override void Excute(RenderContext context) { }
    }


    [NodeColor(1, 0, 1, 0.5f)]
    [RenderNodePath("ToolNodes/Debug", true)]
    [RenderNodeInformation("Use this to debug texture, drag from output pin to create it.")]
    public class TextureDebug : BaseToolNode
    {
        [NodePin(type: PinType.In)]
        [PinColor(1,0,1,1)]
        public TexturePin tex = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1, 1)));

        [Range(0.1f, 10)]
        public float multiplier = 1;

        public enum Channal { RGBA,R,G,B,A };

        [Range(0.1f, 10)]
        public Channal channal = Channal.RGBA;

        [HideInInspector]
        public RenderTexture texture;

        public override void Excute(RenderContext context)
        {
            if (texture != null)
            {
                context.CmdBuffer.SetGlobalFloat("_Multiplier", multiplier);
                context.CmdBuffer.SetGlobalInt("_Channel", (int)channal);
                context.CmdBuffer.SetGlobalFloat("_Aspect", (float)tex.desc.basicDesc.width / tex.desc.basicDesc.height);
                context.CmdBuffer.Blit(tex.handle, texture, MaterialWithName.debugBlit);
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
