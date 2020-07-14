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

        [NodePin(mustConnect: true, outPin: false)]
        public InputClass mustConnectedPin;

        [Tooltip("AA")]
        public int k;

        public override void Render()
        {

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

          
        [NodePin(inPin: false)]
        public InputClass outpin;
    }

    public class TestRenderNode : BaseToolNode
    {
        [NodePin(inPin: false)]
        public InputClass outpin;
        public override void Excute()
        {

        }
    }

    
}