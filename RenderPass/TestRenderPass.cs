using HypnosRenderPipeline.RenderGraph;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderPass
{
    public struct InputClass { }

    public class TestRenderPass : BaseRenderPass
    {

        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public int inoutPin;

        [NodePin(mustConnect: true, type: PinType.In)]
        public InputClass mustConnectedPin;

        [Tooltip("AA")]
        public int k;

        public override void Excute(RenderContext RenderingContext)
        {
            Debug.Log("TestRenderPass: Render " + k.ToString());
        }
    }

    [RenderNodePath("AAA")]
    public class TestRenderPass2 : BaseRenderPass
    {
        [NodePin]
        [Tooltip("It's a test inout pin.")]
        public int inoutPin;

        [Tooltip("AA")]
        [ColorUsage(true, true)]
        public Color k;

          
        [NodePin(type: PinType.Out)]
        public InputClass outpin;

        public override void Excute(RenderContext RenderingContext)
        {
            Debug.Log("TestRenderPass2: Render " + k.ToString());
        }
    }

    public class TestRenderNode : BaseToolNode
    {
        [NodePin(type: PinType.Out)]
        public InputClass outpin;

        public override void Excute(RenderContext RenderingContext)
        {
            Debug.Log("TestRenderNode: Generate output");
        }
    }


    public class TestOutputNode : BaseOutputNode
    {
        [NodePin(type: PinType.In)]
        public InputClass input;

        public override void Excute(RenderContext RenderingContext)
        {
            Debug.Log("TestOutputNode: Output to screen " + target.ToString());
        }
    }
}