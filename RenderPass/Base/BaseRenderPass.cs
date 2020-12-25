using HypnosRenderPipeline.RenderGraph;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using HypnosRenderPipeline.Tools;
using System.Collections.Generic;

namespace HypnosRenderPipeline.RenderPass
{
    public class RenderGraphResourcesPool
    {
        Dictionary<int, RenderTexture> texs;
        public RenderGraphResourcesPool() { texs = new Dictionary<int, RenderTexture>(); }
        public RenderTexture GetTexture(int id, RenderTextureDescriptor desc)
        {
            RenderTexture res = null;
            if (texs.ContainsKey(id))
            {
                res = texs[id];
            }
            // RenderTextureDescriptor RenderTexture.RenderTextureDescriptor is not equal, 
            // even when their values are all same.
            if (res == null || !desc.Equal(res.descriptor)) 
            {
                if (res != null)
                {
                    res.Release();
                    Debug.LogWarning("Desc Changed");
                }
                res = new RenderTexture(desc);
                res.Create();
                texs[id] = res;
            }
            return res;
        }
        public void Dispose()
        {
            foreach (var pair in texs)
            {
                if (pair.Value != null)
                    pair.Value.Release();
            }
            texs.Clear();
        }
    }


    public struct RenderContext
    {
        public Camera camera;
        public CommandBuffer commandBuffer;
        public ScriptableRenderContext context;
        public CullingResults defaultCullingResult;
        public RenderGraphResourcesPool resourcesPool;
        public int frameIndex;
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
        public TexturePin result = new TexturePin(new TexturePinDesc(new RenderTextureDescriptor(1,1, RenderTextureFormat.DefaultHDR), SizeCastMode.Fixed, ColorCastMode.Fixed, SizeScale.Full));

        public override void Execute(RenderContext context) { }
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

        [Range(0, 15)]
        public int lod = 0;

        public enum Channal { RGBA, R, G, B, A, RGB };

        [Range(0.1f, 10)]
        public Channal channal = Channal.RGBA;

        public bool checkboard = true;

        [HideInInspector]
        public RenderTexture texture;

        public override void Execute(RenderContext context)
        {
            if (texture != null)
            {
                context.commandBuffer.SetGlobalFloat("_Multiplier", multiplier);
                context.commandBuffer.SetGlobalInt("_Channel", (int)channal);
                context.commandBuffer.SetGlobalInt("_Checkboard", checkboard ? 1 : 0);
                context.commandBuffer.SetGlobalFloat("_Aspect", (float)tex.desc.basicDesc.width / tex.desc.basicDesc.height);
                context.commandBuffer.SetGlobalInt("_Lod", lod);
                context.commandBuffer.Blit(tex.handle, texture, MaterialWithName.debugBlit);
            }
        }
    }

    public class OutputNode : BaseOutputNode
    {
        public override void Execute(RenderContext context)
        {
            base.Execute(context);
        }
    }
}
