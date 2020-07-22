using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphInfo : ScriptableObject, ISerializationCallbackReceiver
    {
        #region Parameters

        [SerializeField]
        public List<RenderGraphNode> nodes;

        public class Port
        {
            public RenderGraphNode node;
            public string name;
        }

        public class Edge
        {
            public Port output, input;
        }

        [NonSerialized]
        public List<Edge> edges;

        [NonSerialized]
        public Dictionary<RenderGraphNode, Tuple<List<Edge>, List<Edge>>> node_edge;

        #endregion

        #region Serialize

        [Serializable]
        class EdgeRec
        {
            public int i, o;
            public string i_n, o_n;
        }

        [SerializeField]
        List<EdgeRec> edgeRecs;

        [Serializable]
        class NodeEdgeRec
        {
            public int node;
            public List<int> in_edges, out_edge;
        }

        [SerializeField]
        List<NodeEdgeRec> node_edgeRecs;

        public void OnBeforeSerialize()
        {
            edgeRecs.Clear();
            node_edgeRecs.Clear();

            foreach(var edge in edges)
            {
                edgeRecs.Add(new EdgeRec()
                {
                    i = nodes.IndexOf(edge.input.node),
                    o = nodes.IndexOf(edge.output.node),
                    i_n = edge.input.name,
                    o_n = edge.output.name
                });
            }

            foreach (var rec in node_edge)
            {
                List<int> in_edge = new List<int>();
                foreach (var edge in rec.Value.Item1)
                {
                    in_edge.Add(edges.IndexOf(edge));
                }
                List<int> out_edge = new List<int>();
                foreach (var edge in rec.Value.Item2)
                {
                    out_edge.Add(edges.IndexOf(edge));
                }
                node_edgeRecs.Add(new NodeEdgeRec() { in_edges = in_edge, out_edge = out_edge, node = nodes.IndexOf(rec.Key) });
            }
        }

        public void OnAfterDeserialize()
        {
            edges.Clear();
            node_edge.Clear();

            foreach (var edge in edgeRecs)
            {
                AddEdge(new Edge()
                {
                    input = new Port() { node = nodes[edge.i], name = edge.i_n },
                    output = new Port() { node = nodes[edge.o], name = edge.o_n }
                });
            }
            
            foreach (var rec in node_edgeRecs)
            {
                List<Edge> in_edge = new List<Edge>();
                foreach (var edge in rec.in_edges)
                {
                    in_edge.Add(edges[edge]);
                }
                List<Edge> out_edge = new List<Edge>();
                foreach (var edge in rec.out_edge)
                {
                    out_edge.Add(edges[edge]);
                }
                node_edge[nodes[rec.node]] = new Tuple<List<Edge>, List<Edge>>(in_edge, out_edge);
            }

            RenderGraphNode[] nodes_ = new RenderGraphNode[nodes.Count];
            nodes.CopyTo(nodes_);
            foreach (var node in nodes_)
            {
                if (node.nodeType == null)
                {
                    RemoveNode(node);
                }
            }

            Edge[] edges_ = new Edge[edges.Count];
            edges.CopyTo(edges_);
            foreach (var edge in edges_)
            {
                if (edge.input.node.nodeType != null
                    && edge.output.node.nodeType != null
                    && edge.input.node.inputs.Find(i => i.name == edge.input.name) != null
                    && edge.output.node.outputs.Find(i => i.name == edge.output.name) != null)
                { }
                else
                {
                    RemoveEdge(edge);
                }
            }
        }

        #endregion

        public RenderGraphInfo()
        {
            nodes = new List<RenderGraphNode>();
            edges = new List<Edge>();
            node_edge = new Dictionary<RenderGraphNode, Tuple<List<Edge>, List<Edge>>>();
            edgeRecs = new List<EdgeRec>();
            node_edgeRecs = new List<NodeEdgeRec>();
        }

        public void AddNode(RenderGraphNode node)
        {
            nodes.Add(node);
            node_edge.Add(node, new Tuple<List<Edge>, List<Edge>>(new List<Edge>(), new List<Edge>()));
        }

        public void AddEdge(Edge edge)
        {
            edges.Add(edge);
            edge.input.node.parent.Add(edge.output.node);
            edge.output.node.child.Add(edge.input.node);
            SearchNodeInDic(edge.output.node).Item2.Add(edge);
            SearchNodeInDic(edge.input.node).Item1.Add(edge);
        }

        public void RemoveNode(RenderGraphNode node)
        {
            nodes.Remove(node);
            node_edge.Remove(node);
        }

        public void RemoveEdge(Edge edge)
        {
            edges.Remove(edge);
            edge.input.node.parent.Remove(edge.output.node);
            edge.output.node.child.Remove(edge.input.node);
            var t = SearchNodeInDic(edge.output.node, false);
            if (t != null) t.Item2.Remove(edge);
            t = SearchNodeInDic(edge.input.node, false);
            if (t != null) t.Item1.Remove(edge);
        }

        public void ChangeEdge(Edge e1, Edge e2)
        {
            RemoveEdge(e1);
            AddEdge(e2);
        }

        public Tuple<List<Edge>, List<Edge>> SearchNodeInDic(RenderGraphNode node, bool createWhenFailed = true)
        {
            if (!node_edge.ContainsKey(node))
            {
                if (createWhenFailed)
                    node_edge[node] = new Tuple<List<Edge>, List<Edge>>(new List<Edge>(), new List<Edge>());
                else return null;
            }
            var inn = node_edge[node];
            return node_edge[node];
        }

        public void TestExecute()
        {
            HRGDynamicExecutor executor = new HRGDynamicExecutor(this);
            RenderPass.RenderContext context = new RenderPass.RenderContext() { RenderCamera = Camera.main, CmdBuffer = new UnityEngine.Rendering.CommandBuffer() };
            if (!executor.Excute(context))
                Debug.LogError("execute failed");
            context.RenderCamera.RemoveAllCommandBuffers();
            context.RenderCamera.AddCommandBuffer(CameraEvent.AfterEverything, context.CmdBuffer);

            GameObject gameObject = new GameObject();
            GameObject.DestroyImmediate(gameObject); // trigger repaint
        }
    }
}
