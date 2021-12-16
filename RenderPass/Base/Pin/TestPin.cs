using HypnosRenderPipeline.RenderGraph;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HypnosRenderPipeline.RenderPass
{

    public class IntPin : BaseNodePin<int, int>
    {
        public static implicit operator int(IntPin self)
        {
            return self.handle;
        }
    }

    public class TestInit : BaseRenderPass
    {
        [NodePin(PinType.Out)]
        public IntPin output = new IntPin();
        public int value = -1;
        public override void Execute(RenderContext context)
        {
            output.handle = value;
        }
    }

    [RenderNodePath("Test")]
    [RenderNodeInformation("Test Node")]
    [NodeColor(0, 1, 0, 1)]
    public class TestAdd : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public IntPin value0 = new IntPin();

        [PinColor(0,0,1,1)]
        [NodePin(PinType.In, true)]
        public IntPin value1 = new IntPin();

        [NodePin(PinType.Out)]
        public IntPin value2 = new IntPin();

        public override void Execute(RenderContext context)
        {
            value2.handle = value0 + value1;
            Debug.Log(value2.handle);
        }
    }
}