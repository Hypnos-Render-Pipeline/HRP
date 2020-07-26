using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HypnosRenderPipeline.RenderGraph
{

    internal class RenderGraphView : GraphView, IEdgeConnectorListener
    {
        EditorWindow m_editorWindow;
        RenderGraphEditorView m_renderGraphEditorView;

        RenderGraphNodeSeacher m_searcher;

        RenderGraphInfo m_renderGraphInfo;

        public RenderGraphView(RenderGraphEditorView renderGraphEditorView, EditorWindow editorWindow)
        {
            StyleLoader.Load(this);
            m_renderGraphEditorView = renderGraphEditorView;
            m_editorWindow = editorWindow;
            Undo.undoRedoPerformed += Refresh;

            m_searcher = ScriptableObject.CreateInstance<RenderGraphNodeSeacher>();
            m_searcher.Init(this, m_editorWindow, m_renderGraphEditorView);

            graphViewChanged = (GraphViewChange change) =>
            {
                if (change.elementsToRemove != null)
                {
                    bool custom_dele_flag = false;
                    foreach (var ele in change.elementsToRemove)
                    {
                        if (ele.userData as RenderGraphNode != null || ele.userData as RenderGraphInfo.Edge != null || ele.userData as RenderGraphInfo.Group != null)
                        {
                            custom_dele_flag = true;
                            break;
                        }
                    }
                    if (!custom_dele_flag) return change;

                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Delete");

                    var remove_group = new List<GraphElement>();
                    foreach (var sele in selection)
                    {
                        var groupView = sele as RenderGraphGroupView;
                        if (groupView != null)
                        {
                            foreach (var n in groupView.group.nodes)
                            {
                                groupView.RemoveElement(n.NodeView as RenderGraphNodeView);
                            }
                            change.elementsToRemove.Clear();
                            remove_group.Add(groupView);
                            m_renderGraphInfo.RemoveGroup(groupView.group);
                        }
                    }
                    if (remove_group.Count != 0)
                    {
                        change.elementsToRemove.Clear();
                        foreach (var ele in remove_group)
                        {
                            change.elementsToRemove.Add(ele);
                        }
                        return change;
                    }

                    foreach (var ele in change.elementsToRemove)
                    {
                        var node = ele.userData as RenderGraphNode;
                        if (node != null)
                        {
                            m_renderGraphInfo.RemoveNode(node);
                        }
                        else
                        {
                            var edge = ele.userData as RenderGraphInfo.Edge;
                            if (edge != null)
                            {
                                m_renderGraphInfo.RemoveEdge(edge);
                            }
                            else
                            {
                                var group = ele.userData as RenderGraphInfo.Group;
                                if (group != null)
                                {
                                    m_renderGraphInfo.RemoveGroup(group);
                                }
                            }
                        }
                    }
                }
                return change;
            };

            nodeCreationRequest = (c) =>
            {
                SearchWindow.Open(new SearchWindowContext(c.screenMousePosition), m_searcher);
            };

            RegisterCallback<KeyDownEvent>(KeyDown);
            RegisterCopyPast();
        }

        public bool IsParent(RenderGraphNodeView a, RenderGraphNodeView b)
        {
            RenderGraphNode node = a.Node;
            RenderGraphNode current;
            HashSet<RenderGraphNode> nodes = new HashSet<RenderGraphNode>();
            nodes.Add(b.Node);
            while (nodes.Count != 0)
            {
                var enu = nodes.GetEnumerator();
                enu.MoveNext();
                current = enu.Current;
                nodes.Remove(current);
                var edges = m_renderGraphInfo.SearchNodeInDic(current).Item1;
                if (edges.Find(e=>e.output.node == node) != null) return true;
                foreach (var par in edges)
                {
                    nodes.Add(par.output.node);
                }
            }
            return false;
        }


        public override List<Port> GetCompatiblePorts(Port startAnchor, NodeAdapter nodeAdapter)
        {
            var compatibleAnchors = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (port.direction != startAnchor.direction && port.node != startAnchor.node && port.portType == startAnchor.portType)
                {
                    if (startAnchor.direction == Direction.Output)
                    {
                        if (!IsParent(port.node as RenderGraphNodeView, startAnchor.node as RenderGraphNodeView))
                            compatibleAnchors.Add(port);
                    }
                    else
                    {
                        if (!IsParent(startAnchor.node as RenderGraphNodeView, port.node as RenderGraphNodeView))
                            compatibleAnchors.Add(port);
                    }
                }
            }
            return compatibleAnchors;
        }

        public RenderGraphNodeView AddNodeFromTemplate(Type type, Rect pos)
        {
            Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Add Node");

            var nodeView = new RenderGraphNodeView(this, m_renderGraphInfo);
            nodeView.SetType(type);
            nodeView.InitView(this);
            nodeView.MarkDirtyRepaint();

            nodeView.SetPositionWithoutUndo(pos);

            AddElement(nodeView);

            nodeView.Node.NodeView = nodeView;

            m_renderGraphInfo.AddNode(nodeView.Node);
            return nodeView;
        }
        public void AddEdge(Edge edgeView)
        {
            RenderGraphInfo.Edge remove_edge = null;

            if (edgeView.input.connected == true)
            {
                var old_edge_enum = edgeView.input.connections.GetEnumerator();
                old_edge_enum.MoveNext();
                var old_edge = old_edge_enum.Current;
                remove_edge = old_edge.userData as RenderGraphInfo.Edge;
                old_edge.input.Disconnect(old_edge);
                old_edge.output.Disconnect(old_edge);
                old_edge.userData = null; // set this to null means delete from code and don't need redo register
                //old_edge.output.MarkDirtyRepaint(); // don't work, don't know why
                RemoveElement(old_edge);
            }

            var input_node = edgeView.input.node as RenderGraphNodeView;
            var output_node = edgeView.output.node as RenderGraphNodeView;
            var in_name = edgeView.input.portName;
            var out_name = edgeView.output.portName;
            var edge = new RenderGraphInfo.Edge()
            {
                input = new RenderGraphInfo.Port { node = input_node.Node, name = in_name },
                output = new RenderGraphInfo.Port { node = output_node.Node, name = out_name }
            };

            RenderGraphEdgeView rgeView = new RenderGraphEdgeView();
            rgeView.userData = edge;
            rgeView.output = edgeView.output;
            rgeView.input = edgeView.input;

            rgeView.output.Connect(rgeView);
            rgeView.input.Connect(rgeView);

            AddElement(rgeView);

            if (remove_edge != null)
            {
                Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Change Edge");
                m_renderGraphInfo.ChangeEdge(remove_edge, edge);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Add Edge");
                m_renderGraphInfo.AddEdge(edge);
            }
            m_renderGraphInfo.TestExecute();
        }

        void AddSerializedNode(RenderGraphNode node)
        {
            var nodeView = new RenderGraphNodeView(this, node, m_renderGraphInfo);
            node.NodeView = nodeView;
            node.Init(node.nodeType);
            nodeView.InitView(this);
            nodeView.MarkDirtyRepaint();

            nodeView.SetPositionWithoutUndo(node.position);


            AddElement(nodeView);
        }
        void AddSerializedEdge(RenderGraphInfo.Edge edge)
        {
            var input_node = edge.input.node;
            var output_node = edge.output.node;
            var in_name = edge.input.name;
            var out_name = edge.output.name;
            var edgeView = new RenderGraphEdgeView();

            edgeView.userData = edge;

            foreach (var port in (input_node.NodeView as RenderGraphNodeView).inputs)
            {
                if (port.portName == in_name)
                {
                    edgeView.input = port;
                    port.Connect(edgeView);
                    break;
                }
            }
            foreach (var port in (output_node.NodeView as RenderGraphNodeView).outputs)
            {
                if (port.portName == out_name)
                {
                    edgeView.output = port;
                    port.Connect(edgeView);
                    break;
                }
            }
            AddElement(edgeView);
        }
        RenderGraphGroupView AddSerializedGroup(RenderGraphInfo.Group group)
        {
            RenderGraphGroupView groupView = new RenderGraphGroupView(m_renderGraphInfo, group);
            AddElement(groupView);
            return groupView;
        }

        public void SetGraphInfo(RenderGraphInfo info)
        {
            m_renderGraphInfo = info;
            name = AssetDatabase.GetAssetPath(info);
            Refresh();
        }

        public void Refresh()
        {
            var eles = graphElements.ToList();
            foreach (var ele in eles)
            {
                RemoveElement(ele);
            }
            Rect rect = new Rect(0, 0, 0, 0);
            foreach (var node in m_renderGraphInfo.nodes)
            {
                AddSerializedNode(node);
                rect.x = Mathf.Min(node.position.x, rect.x);
                rect.y = Mathf.Min(node.position.y, rect.y);
                rect.xMax = Mathf.Max(node.position.xMax, rect.xMax);
                rect.yMax = Mathf.Max(node.position.yMax, rect.yMax);
            }
            foreach (var node in m_renderGraphInfo.nodes)
            {
                node.position.x -= rect.x;
                node.position.y -= rect.y;
            }
            foreach (var edge in m_renderGraphInfo.edges)
            {
                AddSerializedEdge(edge);
            }
            foreach (var group in m_renderGraphInfo.groups)
            {
                AddSerializedGroup(group);
            }
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var draggedPort = (edge.output != null ? edge.output.edgeConnector.edgeDragHelper.draggedPort : null);
            if (draggedPort != null)
            {
                if (draggedPort.connected)
                {
                    var tex_debug = false;
                    var en = draggedPort.connections.GetEnumerator();
                    while (en.MoveNext())
                    {
                        var exist_edge = en.Current;
                        tex_debug |= (exist_edge.input.node.userData as RenderGraphNode).nodeType == typeof(TextureDebug)
                                        || (exist_edge.output.node.userData as RenderGraphNode).nodeType == typeof(TextureDebug);
                        if (tex_debug) break;
                    }
                    if (tex_debug) return;
                }

                var slot = draggedPort.userData as RenderGraphNode.Slot;
                if (slot.slotType == typeof(RenderPass.TexturePin))
                {
                    var windowRoot = m_editorWindow.rootVisualElement;
                    var windowMousePosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent, position);
                    var graphMousePosition = contentViewContainer.WorldToLocal(windowMousePosition);
                    var nodeView = AddNodeFromTemplate(typeof(RenderPass.TextureDebug), new Rect(graphMousePosition, Vector2.zero));
                    edge.input = nodeView.inputs[0];
                    AddEdge(edge);
                }
            }
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            //Debug.Log("OnDrop");
            AddEdge(edge);
        }

        void KeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.G)
            {
                var nodes = selection.FindAll(element => (element as RenderGraphNodeView) != null);
                if (nodes.Count != 0)
                {
                    RenderGraphInfo.Group group = new RenderGraphInfo.Group();
                    group.name = "New Graph";
                    group.nodes = new List<RenderGraphNode>();
                    group.color = new Color(0.09803922f, 0.09803922f, 0.09803922f, 0.4f);
                    foreach (var node in nodes)
                    {
                        group.nodes.Add((node as RenderGraphNodeView).Node);
                    }
                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Add Group");
                    foreach (var node in group.nodes)
                    {
                        foreach (var removed_group in m_renderGraphInfo.RemoveNodeFromGroup(node))
                        {
                            if (removed_group.nodes.Count == 0)
                                RemoveElement(removed_group.groupView as RenderGraphGroupView);
                        }
                    }
                    m_renderGraphInfo.AddGroup(group);
                    var groupView = AddSerializedGroup(group);
                    groupView.FocusTitleTextField();
                    groupView.title = "New Group";
                }
            }
            else if (e.keyCode == KeyCode.R)
            {
                var nodes = selection.FindAll(element => (element as RenderGraphNodeView) != null);
                if (nodes.Count != 0)
                {
                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Remove Node From Group");
                    foreach (var node in nodes)
                    {
                        foreach (var removed_group in m_renderGraphInfo.RemoveNodeFromGroup((node as RenderGraphNodeView).Node))
                        {
                            (removed_group.groupView as RenderGraphGroupView).RemoveElement(node as RenderGraphNodeView);
                            if (removed_group.nodes.Count == 0)
                                RemoveElement(removed_group.groupView as RenderGraphGroupView);
                        }
                    }
                }
            }
        }

        public override EventPropagation DeleteSelection()
        {
            var groups = selection.FindAll(sele => sele as RenderGraphGroupView != null);
            if (groups.Count != 0)
            {
                ClearSelection();
                selection = groups;
            }
            return base.DeleteSelection();
        }


        void RegisterCopyPast()
        {
            serializeGraphElements =
                 elements =>
                 {
                     string res = "";
                     foreach (var ele in elements)
                     {
                         var node = ele as RenderGraphNodeView;
                         if (node != null)
                         {
                             res += node.Node.TypeString() + "*" + node.Node.position.x + "," + node.Node.position.y + "|";
                         }
                     }
                     if (res.Length != 0) res = res.Substring(0, res.Length - 1);
                     return res;
                 };

            canPasteSerializedData =
                (data) =>
                {
                    if (data != null && data.Length != 0) return true;
                    return false;
                };

            unserializeAndPaste =
                (op, data) =>
                {
                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, op);
                    var strs = data.Split('|');
                    List<RenderGraphNodeView> news = new List<RenderGraphNodeView>();
                    ClearSelection();
                    foreach (var str in strs)
                    {
                        var type_rect = str.Split('*');
                        var type = ReflectionUtil.GetTypeFromName(type_rect[0]);
                        if (type != null)
                        {
                            var rect = type_rect[1].Split(',');
                            AddToSelection(AddNodeFromTemplate(type, new Rect(new Vector2(float.Parse(rect[0]), float.Parse(rect[1])) + Vector2.one * 100f, Vector2.one)));
                        }
                    }
                };
        }

        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        public static extern void Keybd_event(byte bvk, byte bScan, int dwFlags, int dwExtraInfo);
    }
}