using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{
    public class RenderGraphEdgeView : Edge
    {
        public RenderGraphEdgeView() : base() { }
        public RenderGraphEdgeView(bool editable = true) : base()
        {
            if (!editable)
                this.edgeControl.interceptWidth = 0;
        }

        public void DisableEditable()
        {
            this.edgeControl.interceptWidth = 0;
        }

        public override bool IsSelectable()
        {
            return this.edgeControl.interceptWidth != 0;
        }
    }
}