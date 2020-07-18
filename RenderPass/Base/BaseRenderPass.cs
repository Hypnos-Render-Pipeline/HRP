﻿using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using HypnosRenderPipeline.RenderGraph;

namespace HypnosRenderPipeline.RenderPass
{
    public struct RenderContext
    {
        public Camera RenderCamera;
        public CommandBuffer CmdBuffer;
        public ScriptableRenderContext Context;
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
    [RenderNodeType(RenderNodeTypeAttribute.Type.RenderPass)]
    public abstract class BaseRenderPass : BaseRenderNode 
    {
    }


    [RenderNodePath("ToolNodes")]
    [RenderNodeType(RenderNodeTypeAttribute.Type.ToolNode)]
    public abstract class BaseToolNode : BaseRenderNode
    {
    }


    [RenderNodePath("OutputNodes")]
    [RenderNodeType(RenderNodeTypeAttribute.Type.OutputNode)]
    public abstract class BaseOutputNode : BaseRenderNode
    {
        [NodePin(PinType.In, true)]
        public TexturePin result = new TexturePin(new TexturePin.TexturePinDesc(new RenderTextureDescriptor(1,1)));

        [HideInInspector]
        public RenderTargetIdentifier target;

        public override void Excute(RenderContext context)
        {
            context.CmdBuffer.Blit(result.handle, target);
        }
    }
}