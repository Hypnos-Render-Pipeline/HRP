using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    internal class RenderGraphInfo : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        public List<RenderGraphNode> nodes;

        [Serializable]
        public class Port
        {
            public RenderGraphNode node;
            public string name;
        }

        [Serializable]
        public class Edge
        {
            public Port output, input;
        }

        [SerializeField]
        public List<Edge> edges;

        [Serializable]
        class EdgeRec  {
            public int i, o;
            public string i_n, o_n;
        }

        [SerializeField]
        [HideInInspector]
        List<EdgeRec> edgeRecs;

        public void OnBeforeSerialize()
        {
            if (edges == null) return;
            edgeRecs = new List<EdgeRec>();
            foreach (var edge in edges)
            {
                edgeRecs.Add(new EdgeRec()
                {
                    i = nodes.IndexOf(edge.input.node),
                    o = nodes.IndexOf(edge.output.node),
                    i_n = edge.input.name,
                    o_n = edge.output.name
                });
            }
        }

        public void OnAfterDeserialize()
        {
            edges = new List<Edge>();
            if (edgeRecs == null) return;
            foreach (var edge in edgeRecs)
            {
                edges.Add(new Edge()
                {
                    input = new Port() { name = edge.i_n, node = nodes[edge.i] },
                    output = new Port() { name = edge.o_n, node = nodes[edge.o] }
                });
            }
        }



        public RenderGraphInfo()
        {
            nodes = new List<RenderGraphNode>();
            edges = new List<Edge>();
        }

        public void AddNode(RenderGraphNode node)
        {
            nodes.Add(node);
        }

        public void AddEdge(Edge edge)
        {
            edges.Add(edge);
            edge.input.node.parent.Add(edge.output.node);
            edge.output.node.child.Add(edge.input.node);
        }

        public void RemoveNode(RenderGraphNode node)
        {
            nodes.Remove(node);
        }

        public void RemoveEdge(Edge edge)
        {
            edges.Remove(edge);
        }
    }
}
