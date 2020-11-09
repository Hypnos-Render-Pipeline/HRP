using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphNodeView : Node
    {
        VisualElement m_controlsDivider;
        VisualElement m_controlItems;
        RenderGraphInfo m_renderGraphInfo;
        RenderGraphView m_renderGraphView;

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

        float t = -1;
        public void InitView(IEdgeConnectorListener listener)
        {
            title = Node.nodeName;
            inputs = new List<Port>();
            outputs = new List<Port>();

            if (Node.nodeType != typeof(TextureDebug))
            {
                var openButton = new Button(() => {
                    if ((DateTime.Now.Second - t) > 0.1f) { t = DateTime.Now.Second; return; }
                    string floderPath = PathDefine.path + "RenderPass/Implementations";
                    FileInfo[] files = new DirectoryInfo(Application.dataPath).GetFiles("*.cs", SearchOption.AllDirectories);

                    DirectoryInfo direction = new DirectoryInfo(floderPath);
                    files = files.Concat(direction.GetFiles("*.cs", SearchOption.AllDirectories)).ToArray();

                    bool find = false;
                    for (int i = 0; i < files.Length; i++)
                    {
                        var file = files[i];
                        if (file.Name == Node.nodeName + ".cs")
                        {
                            find = true;
                            System.Diagnostics.Process.Start(file.FullName);
                            break;
                        }
                    }
                    if (!find)
                        Debug.LogWarning("Didn't find source file, make sure your code is under 'Implementations' floder (or under 'Assets') and has same name of the class.");
                });

                openButton.style.borderBottomColor
                    = openButton.style.borderLeftColor
                    = openButton.style.borderTopColor
                    = openButton.style.borderRightColor = openButton.style.backgroundColor = Color.clear;
                openButton.style.width = 160;
                openButton.style.height = 25;
                this.Q("title-label").Add(openButton);
            }
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
                titleContainer.Remove(this.Q("title-button-container"));

                Image image = null;

                if (Node.nodeType != typeof(TextureDebug))
                {
                    this.Q("title-label").style.fontSize = new StyleLength(25);
                    titleContainer.Add(m_timeLabel);
                }

                var toolbar = new IMGUIContainer();
                toolbar.onGUIHandler = () =>
                {
                    if (Node.nodeType == typeof(TextureDebug))
                    {
                        var tex = Node.debugTex;
                        if (tex != null && inputs[0].connected)
                        {
                            image.image = tex;
                            var rt = tex as RenderTexture;
                            title = rt.name + ": " + rt.width + "x" + rt.height + " " + Node.debugTexDesc.colorFormat;
                            style.width = image.style.width = Mathf.Max(296, rt.width / 2);
                            image.style.height = rt.height / 2;
                        }
                        else
                        {
                            var rt = tex as RenderTexture;
                            title = Node.nodeName;
                            style.width = 220;
                        }
                        contentContainer.MarkDirtyRepaint();
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
                            this.Q("title-label").style.color = Color.red;
                        }
                        else
                        {
                            this.Q("title-label").style.color = Color.white;
                        }
                    }
                };

                if (Node.nodeType == typeof(TextureDebug))
                    toolbar.pickingMode = PickingMode.Ignore;

                void RegisterChange(RenderGraphNode.Parameter parm, object value)
                {
                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Change Parameter(s)");
                    parm.value = value;
                    m_renderGraphInfo.TestExecute();
                }

                //if (Node.nodeType != typeof(TextureDebug))
                {
                    foreach (var param in Node.parameters)
                    {
                        VisualElement ele = null;
                        if (param.type == typeof(bool))
                        {
                            var field = new Toggle(param.name);
                            field.value = (bool)param.value;
                            field.RegisterValueChangedCallback(e=> {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(int))
                        {
                            var arts = param.raw_data.GetCustomAttributes(typeof(RangeAttribute), false);
                            var art = arts.Length > 0 ? (arts[0] as RangeAttribute) : null;
                            if (art != null)
                            {
                                var field = new SliderInt(param.name, (int)art.min, (int)art.max);
                                field.value = (int)param.value;
                                var valueField = new IntegerField("");
                                valueField.value = field.value;
                                valueField.style.width = 65;
                                var lables = field.Children().ElementAt(1);
                                lables.style.minWidth = lables.style.maxWidth = 116;
                                field.RegisterValueChangedCallback(e => {
                                    RegisterChange(param, e.newValue);
                                    valueField.SetValueWithoutNotify(e.newValue);

                                });
                                valueField.RegisterValueChangedCallback(e => {
                                    RegisterChange(param, e.newValue);
                                    field.SetValueWithoutNotify(e.newValue);
                                });
                                field.Add(valueField);
                                ele = field;
                            }
                            else
                            {
                                IntegerField field = new IntegerField(param.name);
                                field.value = (int)param.value;
                                var lables = field.Children().ElementAt(1);
                                lables.style.minWidth = lables.style.maxWidth = 184;
                                field.RegisterValueChangedCallback(e => {
                                    RegisterChange(param, e.newValue);
                                });
                                ele = field;
                            }
                        }
                        else if (param.type == typeof(float))
                        {
                            var arts = param.raw_data.GetCustomAttributes(typeof(RangeAttribute), false);
                            var art = arts.Length > 0 ? (arts[0] as RangeAttribute) : null;
                            if (art != null)
                            {
                                Slider field = new Slider(param.name, art.min, art.max);
                                field.value = (float)param.value;
                                var valueField = new FloatField("");
                                valueField.value = field.value;
                                valueField.style.width = 65;
                                var lables = field.Children().ElementAt(1);
                                lables.style.minWidth = lables.style.maxWidth = 118;
                                field.RegisterValueChangedCallback(e => {
                                    RegisterChange(param, e.newValue);
                                    valueField.SetValueWithoutNotify(e.newValue);

                                });
                                valueField.RegisterValueChangedCallback(e => {
                                    RegisterChange(param, e.newValue);
                                    field.SetValueWithoutNotify(e.newValue);
                                });
                                field.Add(valueField);
                                ele = field;
                            }
                            else
                            {
                                FloatField field = new FloatField(param.name);
                                field.value = (float)param.value;
                                var lables = field.Children().ElementAt(1);
                                lables.style.minWidth = lables.style.maxWidth = 180;
                                field.RegisterValueChangedCallback(e => {
                                    RegisterChange(param, e.newValue);
                                });
                                ele = field;
                            }
                        }
                        else if (param.type.IsEnum)
                        {
                            var field = new EnumField(param.name, (Enum)param.value);
                            field.value = (Enum)param.value;
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(Vector2))
                        {
                            var field = new Vector2Field(param.name);
                            field.value = (Vector2)param.value;
                            var lables = field.Children().ElementAt(1);
                            lables.style.minWidth = lables.style.maxWidth = 280;
                            var a = lables.Children().GetEnumerator();
                            while (a.MoveNext())
                            {
                                if (a.Current.childCount == 0) continue;
                                var l = a.Current.Children().ElementAt(0);
                                l.style.minWidth = l.style.maxWidth = 12;
                                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                                l = a.Current;
                                l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
                            }
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(Vector2Int))
                        {
                            var field = new Vector2IntField(param.name);
                            field.value = (Vector2Int)param.value;
                            var lables = field.Children().ElementAt(1);
                            lables.style.minWidth = lables.style.maxWidth = 280;
                            var a = lables.Children().GetEnumerator();
                            while (a.MoveNext())
                            {
                                if (a.Current.childCount == 0) continue;
                                var l = a.Current.Children().ElementAt(0);
                                l.style.minWidth = l.style.maxWidth = 12;
                                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                                l = a.Current;
                                l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
                            }
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(Vector3))
                        {
                            var field = new Vector3Field(param.name);
                            field.value = (Vector3)param.value;
                            var lables = field.Children().ElementAt(1);
                            lables.style.minWidth = lables.style.maxWidth = 186;
                            var a = lables.Children().GetEnumerator();
                            while (a.MoveNext())
                            {
                                var l = a.Current.Children().ElementAt(0);
                                l.style.minWidth = l.style.maxWidth = 12;
                                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                                l = a.Current;
                                l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
                            }
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(Vector3Int))
                        {
                            var field = new Vector3IntField(param.name);
                            field.value = (Vector3Int)param.value;
                            var lables = field.Children().ElementAt(1);
                            lables.style.minWidth = lables.style.maxWidth = 186;
                            var a = lables.Children().GetEnumerator();
                            while (a.MoveNext())
                            {
                                var l = a.Current.Children().ElementAt(0);
                                l.style.minWidth = l.style.maxWidth = 12;
                                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                                l = a.Current;
                                l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
                            }
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(Vector4))
                        {
                            var field = new Vector4Field(param.name);
                            field.value = (Vector4)param.value;
                            var lables = field.Children().ElementAt(1);
                            lables.style.minWidth = lables.style.maxWidth = 226;
                            var a = lables.Children().GetEnumerator();
                            while (a.MoveNext())
                            {
                                var l = a.Current.Children().ElementAt(0);
                                l.style.minWidth = l.style.maxWidth = 12;
                                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                                l = a.Current;
                                l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
                            }
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        else if (param.type == typeof(Color))
                        {
                            var field = new ColorField(param.name);
                            field.value = (Color)param.value;
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                            var arts = param.raw_data.GetCustomAttributes(typeof(ColorUsageAttribute), false);
                            var art = arts.Length > 0 ? (arts[0] as ColorUsageAttribute) : null;
                            if (art != null)
                            {
                                field.hdr = art.hdr;
                                field.showAlpha = art.showAlpha;
                            }
                        }
                        else if (param.type == typeof(LayerMask))
                        {
                            var field = new LayerMaskField(param.name);
                            field.value = (LayerMask)param.value;
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, (LayerMask)e.newValue);
                            });
                            ele = field;
                        }
                        else if (ReflectionUtil.IsEngineObject(param.type))
                        {
                            var field = new ObjectField(param.name);
                            field.objectType = param.type;
                            field.value = (UnityEngine.Object)param.value;
                            field.allowSceneObjects = false;
                            field.RegisterValueChangedCallback(e => {
                                RegisterChange(param, e.newValue);
                            });
                            ele = field;
                        }
                        var label = ele.Children().ElementAt(0);
                        if (param.type == typeof(Vector4))
                            label.style.maxWidth = label.style.minWidth = 60;
                        else
                            label.style.maxWidth = label.style.minWidth = 100;
                        ele.Children().First().tooltip = param.info;
                        toolbar.Add(ele);
                    }
                }

                m_controlItems.Add(toolbar);

                if (Node.nodeType == typeof(TextureDebug))
                {
                    image = new Image();
                    image.scaleMode = ScaleMode.ScaleToFit;
                    m_controlItems.Add(image);
                }
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
                        (removed_group.groupView as RenderGraphGroupView).RemoveElement(this);
                        if (removed_group.nodes.Count == 0)
                            m_renderGraphView.RemoveElement(removed_group.groupView as RenderGraphGroupView);
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

            if (slot.slotType.IsGenericType && slot.slotType.GetGenericTypeDefinition() == typeof(BufferPin<>))
            {
                port.portColor = new Color(0.5f, 0.5f, 1f);
            }
            if (slot.color != null)
            {
                port.portColor = slot.color.Value;
            }
                        
            if (slot.mustConnect && input)
                port.Q("type").style.color = new Color(1, 0.7f, 0.8f);

            return port;
        }
    }

}