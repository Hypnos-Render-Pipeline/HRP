using HypnosRenderPipeline.RenderPass;
using ICSharpCode.NRefactory.Ast;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphNodeView : Node
    {
        VisualElement m_controlsDivider;
        VisualElement m_controlItems;
        VisualElement m_portInputContainer;
        RenderGraphInfo m_renderGraphInfo;
        RenderGraphView m_renderGraphView;

        static Material __unlit_srgb_mat__;
        static Material __unlit_mat__;
        static Material m_unlitMat
        {
            get
            {
                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                {
                    if (__unlit_srgb_mat__ == null) __unlit_srgb_mat__ = new Material(Shader.Find("Unlit/GammaCorrect")); return __unlit_srgb_mat__;
                }
                else
                {
                    if (__unlit_mat__ == null) __unlit_mat__ = new Material(Shader.Find("Unlit/Transparent")); return __unlit_mat__;
                }
            }
        }
        
        public List<Port> inputs;
        public List<Port> outputs;

        public RenderGraphNode Node;
        public bool operate_by_unity = true;

        Label m_timeLabel;

        public RenderGraphNodeView(RenderGraphView renderGraphView, RenderGraphInfo renderGraphInfo)
        {
            StyleLoader.Load(this);
            Node = new RenderGraphNode();
            userData = Node;
            m_renderGraphInfo = renderGraphInfo;
            m_renderGraphView = renderGraphView;
        }

        public RenderGraphNodeView(RenderGraphView renderGraphView, RenderGraphNode node, RenderGraphInfo renderGraphInfo)
        {
            StyleLoader.Load(this);
            Node = node;
            userData = Node;
            m_renderGraphInfo = renderGraphInfo;
            m_renderGraphView = renderGraphView;
        }

        public void SetType(Type t)
        {
            Node.Init(t);
        }

        public void InitView(IEdgeConnectorListener listener)
        {
            title = Node.nodeName;
            inputs = new List<Port>();
            outputs = new List<Port>();

            Color nodeColor = Node.nodeType.GetCustomAttribute<NodeColorAttribute>().color;
            this.Children().ElementAt(0).style.backgroundColor = new StyleColor(nodeColor);
            var contents = this.Q("contents");
            m_timeLabel = new Label();
            m_timeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            m_timeLabel.style.fontSize = new StyleLength(20);

            var controlsContainer = new VisualElement { name = "controls" };
            {
                m_controlsDivider = new VisualElement { name = "divider" };
                m_controlsDivider.AddToClassList("horizontal");
                controlsContainer.Add(m_controlsDivider);
                m_controlItems = new VisualElement { name = "items" };
                controlsContainer.Add(m_controlItems);

                if (Node.nodeType != typeof(TextureDebug))
                {
                    this.Q("title-label").style.fontSize = new StyleLength(25);
                    titleContainer.Remove(this.Q("title-button-container"));
                    titleContainer.Add(m_timeLabel);
                }

                var toolbar = new IMGUIContainer(() =>
                {
                    if (Node.nodeType == typeof(TextureDebug))
                    {
                        var tex = Node.debugTex;
                        if (tex != null && inputs[0].connected)
                        {
                            var rt = tex as RenderTexture;
                            title = rt.name + ": " + rt.width + "x" + rt.height + " " + rt.format;
                            style.width = Mathf.Max(220, rt.width / 2);
                            var rect = EditorGUILayout.GetControlRect(false, rt.height / 2);
                            EditorGUI.DrawPreviewTexture(rect, rt, m_unlitMat, scaleMode: ScaleMode.ScaleToFit);
                        }
                        else
                        {
                            var rt = tex as RenderTexture;
                            title = Node.nodeName;
                            style.width = 220;
                        }
                    }
                    else
                    {
                        if (Node.sampler != null)
                        {
                            var ms = Node.sampler.GetRecorder().gpuElapsedNanoseconds / 1000000.0f;
                            var color = Color.Lerp(Color.green, Color.yellow, ms);
                            m_timeLabel.style.color = color;
                            m_timeLabel.text = ms.ToString("F2") + "ms  ";
                        }
                        if (expanded)
                        {
                            var parms = new RenderGraphNode.Parameter[Node.parameters.Count];
                            Node.parameters.CopyTo(parms);
                            var rect = EditorGUILayout.BeginVertical();
                            bool u_dep = false;
                            foreach (var input_slot in inputs)
                            {
                                if (input_slot.connected == false
                                        && (input_slot.userData as RenderGraphNode.Slot).mustConnect == true)
                                {
                                    u_dep = true;
                                    break;
                                }
                            }
                            if (u_dep)
                            {
                                EditorGUI.DrawRect(rect, new Color(0.4f, 0.1f, 0.1f, 0.5f));
                                EditorGUILayout.LabelField("Unsatisfied dependency");
                            }
                            foreach (var parm in parms)
                            {
                                EditorGUI.BeginChangeCheck();
                                var value = parm.value;
                                if (parm.type == typeof(bool))
                                {
                                    value = EditorGUILayout.Toggle(parm.name, (bool)parm.value);
                                }
                                else if (parm.type == typeof(int))
                                {
                                    var arts = parm.raw_data.GetCustomAttributes(typeof(RangeAttribute), false);
                                    var art = arts.Length > 0 ? (arts[0] as RangeAttribute) : null;
                                    if (art != null)
                                    {
                                        value = EditorGUILayout.IntSlider(parm.name, (int)parm.value, (int)art.min, (int)art.max);
                                    }
                                    else
                                    {
                                        value = EditorGUILayout.IntField(parm.name, (int)parm.value);
                                    }
                                }
                                else if (parm.type == typeof(float))
                                {
                                    var arts = parm.raw_data.GetCustomAttributes(typeof(RangeAttribute), false);
                                    var art = arts.Length > 0 ? (arts[0] as RangeAttribute) : null;
                                    if (art != null)
                                    {
                                        value = EditorGUILayout.Slider(parm.name, (float)parm.value, (float)art.min, (float)art.max);
                                    }
                                    else
                                    {
                                        value = EditorGUILayout.FloatField(parm.name, (float)parm.value);
                                    }
                                }
                                else if (parm.type.IsEnum)
                                {
                                    value = EditorGUILayout.EnumPopup(parm.name, (Enum)parm.value);
                                }
                                else if (parm.type == typeof(Vector2))
                                {
                                    value = EditorGUILayout.Vector2Field(parm.name, (Vector2)parm.value);
                                }
                                else if (parm.type == typeof(Vector2Int))
                                {
                                    value = EditorGUILayout.Vector2IntField(parm.name, (Vector2Int)parm.value);
                                }
                                else if (parm.type == typeof(Vector3))
                                {
                                    value = EditorGUILayout.Vector3Field(parm.name, (Vector3)parm.value);
                                }
                                else if (parm.type == typeof(Vector3Int))
                                {
                                    value = EditorGUILayout.Vector3IntField(parm.name, (Vector3Int)parm.value);
                                }
                                else if (parm.type == typeof(Vector4))
                                {
                                    value = EditorGUILayout.Vector4Field(parm.name, (Vector4)parm.value);
                                }
                                else if (parm.type == typeof(Color))
                                {
                                    var arts = parm.raw_data.GetCustomAttributes(typeof(ColorUsageAttribute), false);
                                    var art = arts.Length > 0 ? (arts[0] as ColorUsageAttribute) : null;
                                    if (art != null)
                                    {
                                        value = EditorGUILayout.ColorField(new GUIContent(parm.name), (Color)parm.value,
                                                                                    true, art.showAlpha, art.hdr);
                                    }
                                    else
                                    {
                                        value = EditorGUILayout.ColorField(parm.name, (Color)parm.value);
                                    }
                                }
                                else if (ReflectionUtil.IsEngineObject(parm.type))
                                {
                                    value = EditorGUILayout.ObjectField(parm.name, parm.value as UnityEngine.Object, parm.type, allowSceneObjects: false);
                                }
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Change Parameter");
                                    parm.value = value;
                                    m_renderGraphInfo.TestExecute();
                                }
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }
                });

                toolbar.style.unityOverflowClipBox = OverflowClipBox.ContentBox;

                if (Node.nodeType == typeof(TextureDebug))
                    toolbar.pickingMode = PickingMode.Ignore;
                m_controlItems.Add(toolbar);
            }
            contents.Add(controlsContainer);

            foreach (var slot in Node.inputs)
            {
                var inp = PortView.Create(true, listener, slot);
                inputs.Add(inp);
                inputContainer.Add(inp);
            }
            foreach (var slot in Node.outputs)
            {
                var oup = PortView.Create(false, listener, slot);
                outputs.Add(oup);
                inputContainer.Add(oup);
            }

            tooltip = Node.info;

            RefreshExpandedState();
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Move Node");
            Node.position = newPos;
        }
        public void SetPositionWithoutUndo(Rect newPos)
        {
            base.SetPosition(newPos);
            Node.position = newPos;
        }
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Remove From Group", 
                e => { 
                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Remove Node From Group");
                    foreach (var removed_group in m_renderGraphInfo.RemoveNodeFromGroup(Node))
                    {
                        removed_group.groupView.RemoveElement(this);
                        if (removed_group.nodes.Count == 0)
                            m_renderGraphView.RemoveElement(removed_group.groupView);
                    }
                }, m_renderGraphInfo.InGroup(Node) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }
    }

    internal sealed class PortView : Port
    {
        PortView(Orientation portOrientation, Direction portDirection, Capacity portCapacity, Type type)
            : base(portOrientation, portDirection, portCapacity, type)
        {
            StyleLoader.Load(this);
        }

        public static Port Create(bool input, IEdgeConnectorListener connectorListener, RenderGraphNode.Slot slot)
        {
            var port = new PortView(Orientation.Horizontal, input ? Direction.Input : Direction.Output, input ? Capacity.Single : Capacity.Multi, slot.slotType)
            {
                m_EdgeConnector = new EdgeConnector<RenderGraphEdgeView>(connectorListener),
            };
            port.AddManipulator(port.m_EdgeConnector);
            port.userData = slot;
            port.portName = slot.name;
            port.Q("type").style.fontSize = new StyleLength(16);
            port.tooltip = slot.info;
            if (slot.mustConnect && input)
                port.portColor = Color.red;
            return port;
        }
    }

}