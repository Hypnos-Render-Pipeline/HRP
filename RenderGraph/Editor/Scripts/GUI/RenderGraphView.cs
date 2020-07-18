using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering;
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
                        if (ele.userData as RenderGraphNode != null || ele.userData as RenderGraphInfo.Edge != null)
                        {
                            custom_dele_flag = true;
                            break;
                        }
                    }
                    if (!custom_dele_flag) return change;

                    Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Delete");
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
                        }
                    }

                    HRGDynamicExecutor executor = new HRGDynamicExecutor(m_renderGraphInfo);

                    RenderPass.RenderContext context = new RenderPass.RenderContext() { RenderCamera = Camera.main, CmdBuffer = new UnityEngine.Rendering.CommandBuffer() };
                    Debug.Log(executor.Excute(context));
                    context.RenderCamera.RemoveAllCommandBuffers();
                    context.RenderCamera.AddCommandBuffer(CameraEvent.AfterEverything, context.CmdBuffer);

                }
                return change;
            };
            
            nodeCreationRequest = (c) =>
            {
                SearchWindow.Open(new SearchWindowContext(c.screenMousePosition), m_searcher);
            };
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
                if (current.parent.Contains(node)) return true;
                foreach (var par in current.parent)
                {
                    nodes.Add(par);
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
            var nodeView = new RenderGraphNodeView(m_renderGraphInfo);
            nodeView.SetType(type);
            nodeView.InitView(this);
            nodeView.MarkDirtyRepaint();
            
            nodeView.SetPosition(pos);

            AddElement(nodeView);

            nodeView.Node.NodeView = nodeView;

            Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Add Node");
            m_renderGraphInfo.AddNode(nodeView.Node);
            return nodeView;
        }
        public void AddEdge(Edge edgeView)
        {
            Undo.RegisterCompleteObjectUndo(m_renderGraphInfo, "Add Edge");

            if (edgeView.input.connected == true)
            {
                var old_edge_enum = edgeView.input.connections.GetEnumerator();
                old_edge_enum.MoveNext();
                var old_edge = old_edge_enum.Current;
                old_edge.input.Disconnect(old_edge);
                old_edge.output.Disconnect(old_edge);
                m_renderGraphInfo.RemoveEdge(old_edge.userData as RenderGraphInfo.Edge);
                old_edge.userData = null; // set this to null means delete from code and don't need redo register
                old_edge.output.MarkDirtyRepaint(); // don't work, don't know why
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
            m_renderGraphInfo.AddEdge(edge);
            
            RenderGraphEdgeView rgeView = new RenderGraphEdgeView();
            rgeView.userData = edge;
            rgeView.output = edgeView.output;
            rgeView.input = edgeView.input;

            rgeView.output.Connect(rgeView);
            rgeView.input.Connect(rgeView);

            AddElement(rgeView);


            HRGDynamicExecutor executor = new HRGDynamicExecutor(m_renderGraphInfo);

            RenderPass.RenderContext context = new RenderPass.RenderContext() { RenderCamera = Camera.main, CmdBuffer = new UnityEngine.Rendering.CommandBuffer() };
            Debug.Log(executor.Excute(context));
            context.RenderCamera.RemoveAllCommandBuffers();
            context.RenderCamera.AddCommandBuffer(CameraEvent.AfterEverything, context.CmdBuffer);
        }

        void AddSerializedNode(RenderGraphNode node)
        {
            var nodeView = new RenderGraphNodeView(node, m_renderGraphInfo);
            node.NodeView = nodeView;
            node.Init(node.nodeType);
            nodeView.InitView(this);
            nodeView.MarkDirtyRepaint();

            nodeView.SetPosition(node.positon);

            
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

            foreach (var port in input_node.NodeView.inputs)
            {
                if (port.portName == in_name)
                {
                    edgeView.input = port;
                    port.Connect(edgeView);
                    break;
                }
            }
            foreach (var port in output_node.NodeView.outputs)
            {
                if (port.portName == out_name)
                {
                    edgeView.output = port;
                    port.Connect(edgeView);
                    break;
                }
            }
            input_node.parent.Add(output_node);
            output_node.child.Add(input_node);
            AddElement(edgeView);
        }

        public void SetGraphInfo(RenderGraphInfo info)
        {
            m_renderGraphInfo = info;
            name = AssetDatabase.GetAssetPath(info);
            Refresh();
        }

        public void Refresh()
        {
            var nodes_ = nodes.ToList();
            foreach (var node in nodes_)
            {
                RemoveElement(node);
            }
            foreach (var node in m_renderGraphInfo.nodes)
            {
                AddSerializedNode(node);
            }
            var edges_ = edges.ToList();
            foreach (var edge in edges_)
            {
                RemoveElement(edge);
            }
            foreach (var edge in m_renderGraphInfo.edges)
            {
                AddSerializedEdge(edge);
            }
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var draggedPort = (edge.output != null ? edge.output.edgeConnector.edgeDragHelper.draggedPort : null);
            if (draggedPort != null)
            {
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
    }
}