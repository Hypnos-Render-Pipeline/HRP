using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphGroupView : Group
    {
        HypnosRenderGraph m_renderGraphInfo;
        public HypnosRenderGraph.Group group;
        Color color;
        public RenderGraphGroupView(HypnosRenderGraph info, HypnosRenderGraph.Group group)
        {
            m_renderGraphInfo = info;
            title = group.name;
            this.group = group;
            userData = group;
            group.groupView = this;

            foreach (var node in group.nodes)
            {
                (node.NodeView as RenderGraphNodeView).operate_by_unity = false;
                AddElement(node.NodeView as RenderGraphNodeView);
            }

            style.borderBottomWidth = style.borderLeftWidth = style.borderTopWidth = style.borderRightWidth = 3;

            var timeLabel = new UnityEngine.UIElements.Label("AAA");
            timeLabel.style.unityTextAlign = TextAnchor.LowerRight;
            timeLabel.style.fontSize = new StyleLength(40);
            timeLabel.style.top = 27;
            timeLabel.pickingMode = PickingMode.Ignore;

            var titile_label = this.Q("titleLabel");
            var bgcolor = new ColorField();
            bgcolor.value = group.color;
            ChangeColor(group.color);
            titile_label.Add(bgcolor);
            bgcolor.style.alignSelf = Align.FlexStart;
            bgcolor.style.top = 61;
            bgcolor.style.width = 50;
            bgcolor.RegisterValueChangedCallback((e) => {
                ChangeColor(e.newValue, true);
            });

            titile_label.style.fontSize = new StyleLength(50);
            titile_label.Add(timeLabel);

            this.Q("titleLabel").Add(new IMGUIContainer(()=> {
                var ms = 0.0f;
                foreach (var node in group.nodes)
                {
                    if (node.sampler != null)
                    {
                        ms += node.sampler.GetRecorder().gpuElapsedNanoseconds / 1000000.0f;
                    }
                }
                var color = Color.Lerp(Color.green, Color.yellow, ms);
                timeLabel.style.color = color;
                timeLabel.text = ms.ToString("F2") + "ms";
                bgcolor.style.visibility = selected ? Visibility.Visible : Visibility.Hidden;
            }));
        }

        void ChangeColor(Color c, bool undo = false)
        {
            var a = c;
            a.a = Mathf.Clamp(a.a, 0.1f, 0.9f);
            style.backgroundColor = a;
            var alpha = a.a * 2;
            a /= a.maxColorComponent;
            a.a = alpha;
            style.borderBottomColor = style.borderLeftColor = style.borderRightColor = style.borderTopColor = a;
            if (undo)
                Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Change Group Color");
            group.color = c;
        }

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            foreach (var ele in elements)
            {
                var nodeView = ele as RenderGraphNodeView;
                if (nodeView != null)
                {
                    if (nodeView.operate_by_unity)
                    {
                        Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Add Node To Group");
                        m_renderGraphInfo.AddNodeToGroup(nodeView.Node, group);
                    }
                    else
                    {
                        nodeView.operate_by_unity = true;
                    }
                }
            }
            base.OnElementsAdded(elements);
        }


        protected override void OnGroupRenamed(string oldName, string newName)
        {
            base.OnGroupRenamed(oldName, newName);

            Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Change GroupName");
            group.name = newName;
        }
    }
}