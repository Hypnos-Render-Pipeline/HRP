using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphGroupView : Group
    {
        RenderGraphInfo m_renderGraphInfo;
        public RenderGraphInfo.Group group;
        Color color;
        public RenderGraphGroupView(RenderGraphInfo info, RenderGraphInfo.Group group)
        {
            m_renderGraphInfo = info;
            title = group.name;
            this.group = group;
            userData = group;
            group.groupView = this;

            foreach (var node in group.nodes)
            {
                node.NodeView.operate_by_unity = false;
                AddElement(node.NodeView);
            }

            var timeLabel = new UnityEngine.UIElements.Label("AAA");
            timeLabel.style.unityTextAlign = TextAnchor.LowerRight;
            timeLabel.style.fontSize = new StyleLength(40);
            timeLabel.style.top = -20;
            timeLabel.pickingMode = PickingMode.Ignore;

            var titile_label = this.Q("titleLabel");
            var bgcolor = new ColorField();
            bgcolor.value = group.color;
            style.backgroundColor = bgcolor.value;
            titile_label.Add(bgcolor);
            bgcolor.style.alignSelf = Align.FlexStart;
            bgcolor.style.top = 6;
            bgcolor.style.width = 50;
            bgcolor.RegisterValueChangedCallback((e) => {
                style.backgroundColor = e.newValue;
                Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Change Group Color");
                group.color = e.newValue;
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

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            foreach (var ele in elements)
            {
                var nodeView = ele as RenderGraphNodeView;
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