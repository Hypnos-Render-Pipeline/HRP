using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphInfo : ScriptableObject, ISerializationCallbackReceiver
    {
        #region Parameters

        [SerializeField]
        public Vector3 viewPosition;
        [SerializeField]
        public Vector3 viewScale;

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

        public class Group
        {
            public string name;
            public Color color;
            public List<RenderGraphNode> nodes;
            public RenderGraphGroupView groupView;
        }

        [NonSerialized]
        public List<Group> groups;

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

        [Serializable]
        class GroupRec
        {
            public string name;
            public Color color;
            public List<int> nodes;
        }

        [SerializeField]
        List<GroupRec> groupRecs;

        public void OnBeforeSerialize()
        {
            edgeRecs.Clear();
            node_edgeRecs.Clear();
            groupRecs.Clear();

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
            foreach (var group in groups)
            {
                var rec = new GroupRec();
                rec.name = group.name;
                rec.color = group.color;
                rec.nodes = new List<int>();
                foreach (var node in group.nodes)
                {
                    rec.nodes.Add(nodes.IndexOf(node));
                }
                groupRecs.Add(rec);
            }
        }

        public void OnAfterDeserialize()
        {
            edges.Clear();
            node_edge.Clear();
            groups.Clear();

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

            foreach (var rec in groupRecs)
            {
                var group = new Group();
                group.name = rec.name;
                group.color = rec.color;
                group.nodes = new List<RenderGraphNode>();
                foreach (var node in rec.nodes)
                {
                    group.nodes.Add(nodes[node]);
                }
                groups.Add(group);
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
            groups = new List<Group>();
            node_edge = new Dictionary<RenderGraphNode, Tuple<List<Edge>, List<Edge>>>();
            edgeRecs = new List<EdgeRec>();
            node_edgeRecs = new List<NodeEdgeRec>();
            groupRecs = new List<GroupRec>();
        }

        public void AddNode(RenderGraphNode node)
        {
            nodes.Add(node);
            node_edge.Add(node, new Tuple<List<Edge>, List<Edge>>(new List<Edge>(), new List<Edge>()));
        }

        public void AddEdge(Edge edge)
        {
            edges.Add(edge);
            SearchNodeInDic(edge.output.node).Item2.Add(edge);
            SearchNodeInDic(edge.input.node).Item1.Add(edge);
        }

        public void RemoveNode(RenderGraphNode node)
        {
            nodes.Remove(node);
            node_edge.Remove(node);
            RemoveNodeFromGroup(node);
        }

        public void RemoveEdge(Edge edge)
        {
            edges.Remove(edge);
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

        public void AddGroup(Group group)
        {
            groups.Add(group);
        }

        public void RemoveGroup(Group group)
        {
            groups.Remove(group);
        }
        public List<Group> RemoveNodeFromGroup(RenderGraphNode node)
        {
            var gs = groups.FindAll(g => g.nodes.Contains(node));
            var remove_groups = new List<Group>();
            foreach (var g in gs)
            {
                g.nodes.Remove(node);
                remove_groups.Add(g);
            }
            foreach (var g in remove_groups)
            {
                if (g.nodes.Count == 0)
                    groups.Remove(g);
            }
            return remove_groups;
        }
        public void AddNodeToGroup(RenderGraphNode node, Group group)
        {
            RemoveNodeFromGroup(node);
            group.nodes.Add(node);
        }

        public bool InGroup(RenderGraphNode node)
        {
            return groups.Find(g => g.nodes.Contains(node)) != null;
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
