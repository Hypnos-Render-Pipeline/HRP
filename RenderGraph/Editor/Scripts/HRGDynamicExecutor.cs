using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{

    class HRGDynamicExecutor
    {
        RenderGraphInfo m_graph;


        public HRGDynamicExecutor(RenderGraphInfo graph)
        {
            m_graph = graph;
        }

        public bool Excute(RenderContext context)
        {
            readyNodes.Clear();
            valuePool.Clear();
            dependency.Clear();
            pinPool.Clear();
            temp_id = 1;
            FindEnterPoints();

            while (readyNodes.Count != 0)
            {
                var node = readyNodes.Pop();
                pin_map.Clear();
                BaseRenderNode renderNode = SetupNode(context, node);
                if (renderNode == null) return false;
                switch (node.nodeType.GetCustomAttribute<RenderNodeTypeAttribute>().type)
                {
                    case RenderNodeTypeAttribute.Type.OutputNode:
                        // setup rendertarget
                        ((BaseOutputNode)renderNode).target = BuiltinRenderTextureType.CameraTarget;
                        break;
                    case RenderNodeTypeAttribute.Type.ToolNode:
                        // setup debug tex
                        if (node.nodeType == typeof(TextureDebug))
                        {
                            var debugNode = renderNode as TextureDebug;
                            if (node.debugTex == null)
                                node.debugTex = new RenderTexture(debugNode.tex.desc.basicDesc);
                            else
                            {
                                var desc1 = node.debugTex.descriptor;
                                var desc2 = debugNode.tex.desc.basicDesc;
                                if (desc1.width != desc2.width || desc1.height != desc2.height || desc1.colorFormat != desc2.colorFormat)
                                {
                                    node.debugTex.Release();
                                    node.debugTex = new RenderTexture(desc2);
                                }
                            }
                            node.debugTex.name = debugNode.tex.name;
                            debugNode.texture = node.debugTex;
                        }
                        break;
                    default:
                        break;
                };

                if (node.sampler == null) node.sampler = UnityEngine.Profiling.CustomSampler.Create(node.nodeName + node.GetHashCode(), true);
                context.CmdBuffer.BeginSample(node.sampler);
                renderNode.Excute(context);
                context.CmdBuffer.EndSample(node.sampler);

                ReleaseNode(context, renderNode);
            }

            return true;
        }

        int temp_id;
        Dictionary<RenderGraphNode, Dictionary<string, System.Object>> valuePool =new Dictionary<RenderGraphNode, Dictionary<string, object>>();
        Dictionary<object, int> pinPool = new Dictionary<object, int>();
        Dictionary<RenderGraphNode, int> dependency = new Dictionary<RenderGraphNode, int>();
        Stack<RenderGraphNode> readyNodes = new Stack<RenderGraphNode>();
        Dictionary<string, Tuple<object, Type>> pin_map = new Dictionary<string, Tuple<object, Type>>();

        BaseRenderNode SetupNode(RenderContext context, RenderGraphNode node)
        {
            List<RenderGraphNode> next_nodes = new List<RenderGraphNode>();

            var field_infos = ReflectionUtil.GetFieldInfo(node.nodeType);

            var node_instance = System.Activator.CreateInstance(node.nodeType);

            foreach (var parm in field_infos.Item3)
            {
                parm.SetValue(node_instance, node.parameters.Find(p => p.name == parm.Name).value);
            }

            Dictionary<string, object> existValue = new Dictionary<string, object>();
            if (valuePool.ContainsKey(node))
            {
                existValue = valuePool[node];
            }

            foreach (var input in field_infos.Item1)
            {
                if (existValue.ContainsKey(input.Name))
                {
                    var from_pin = existValue[input.Name];

                    var compare_method = input.FieldType.GetMethod("Compare");
                    var generic_types = from_pin.GetType().BaseType.GetGenericArguments();
                    bool same = (bool)compare_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                    if (same && (node.nodeType == typeof(TextureDebug) || pinPool[from_pin] == 1))
                    {
                        input.SetValue(node_instance, from_pin);
                    }
                    else
                    {
                        var ca_cast_method = input.FieldType.GetMethod("CanCastFrom");
                        bool can_cast = (bool)ca_cast_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                        if (can_cast)
                        {
                            var init_method = input.FieldType.GetMethod("AllocateResourcces");
                            string name = input.Name + temp_id++;
                            int id = Shader.PropertyToID(name);
                            init_method.Invoke(input.GetValue(node_instance), new object[] { context, id });
                            input.FieldType.GetField("name").SetValue(input.GetValue(node_instance), name);
                            pinPool[input.GetValue(node_instance)] = 1;
                            var cast_method = input.FieldType.GetMethod("CastFrom");
                            cast_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                            if (--pinPool[from_pin] == 0)
                            {
                                var release_method = input.FieldType.GetMethod("ReleaseResourcces");
                                release_method.Invoke(from_pin, new object[] { context });
                            }
                        }
                        else
                        {
                            Debug.LogError("Can't auto cast from Pin " + from_pin.ToString() + " to " + input);
                            return null;
                        }
                    }
                }
                else
                {
                    if (input.GetCustomAttribute<BaseRenderNode.NodePinAttribute>().mustConnect)
                    {
                        return null;
                    }
                    else
                    {
                        // create resources for bare pin
                        var init_method = input.FieldType.GetMethod("AllocateResourcces");
                        string name = input.Name + temp_id++;
                        int id = Shader.PropertyToID(name);
                        init_method.Invoke(input.GetValue(node_instance), new object[] { context, id });
                        input.FieldType.GetField("name").SetValue(input.GetValue(node_instance), name);
                        pinPool[input.GetValue(node_instance)] = 1;
                    }
                }
                pin_map[input.Name] = new Tuple<object, Type>(input.GetValue(node_instance), input.FieldType);
            }

            var out_edges = m_graph.SearchNodeInDic(node).Item2;

            foreach (var output in field_infos.Item2)
            {
                System.Object value = null;

                var edges = out_edges.FindAll(e => e.output.name == output.Name);

                if (!pin_map.ContainsKey(output.Name))
                {
                    // create resources for out pin
                    var init_method = output.FieldType.GetMethod("AllocateResourcces");
                    string name = output.Name + temp_id++;
                    int id = Shader.PropertyToID(name);
                    init_method.Invoke(output.GetValue(node_instance), new object[] { context, id });
                    value = output.GetValue(node_instance);
                    pinPool[value] = 0;
                    output.FieldType.GetField("name").SetValue(value, name);

                }
                else
                {
                    value = pin_map[output.Name].Item1;
                }

                pinPool[value] += edges.Count;

                foreach (var edge in edges)
                {
                    if (!valuePool.ContainsKey(edge.input.node))
                        valuePool[edge.input.node] = new Dictionary<string, object>();

                    valuePool[edge.input.node].Add(edge.input.name, value);
                    if (--dependency[edge.input.node] == 0)
                    {
                        next_nodes.Add(edge.input.node);
                    }
                }
            }

            next_nodes.Sort((x, y) => {
                bool x_ = x.nodeType == typeof(TextureDebug);
                bool y_ = y.nodeType == typeof(TextureDebug);
                return (x_ && y_) || !(x_ || y_) ? 0 : x_ ? 1 : -1;
            });
            foreach (var n in next_nodes)
            {
                readyNodes.Push(n);
            }

            return node_instance as BaseRenderNode;
        }

        void ReleaseNode(RenderContext context, BaseRenderNode node)
        {
            foreach (var pin in pin_map)
            {
                if (pinPool.ContainsKey(pin.Value.Item1))
                {
                    if (--pinPool[pin.Value.Item1] > 0) continue;
                }
                var release_method = pin.Value.Item2.GetMethod("ReleaseResourcces");
                release_method.Invoke(pin.Value.Item1, new object[] { context });
            }
        }


        void FindEnterPoints()
        {
            foreach (var node in m_graph.node_edge)
            {
                if (node.Value.Item1.Count == 0)
                {
                    readyNodes.Push(node.Key);
                }
                else
                {
                    dependency[node.Key] = node.Value.Item1.Count;
                }
            }
        }
    }



}