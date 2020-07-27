using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace HypnosRenderPipeline.RenderGraph
{
#if UNITY_EDITOR

    internal class HRGDynamicExecutor
    {
        RenderGraphInfo m_graph;


        public HRGDynamicExecutor(RenderGraphInfo graph)
        {
            m_graph = graph;
        }

        public int Excute(RenderContext context)
        {
            int result = -1;
            readyNodes.Clear();
            valuePool.Clear();
            dependency.Clear();
            pinPool.Clear();
            temp_id = context.RenderCamera.GetHashCode();
            FindEnterPoints();

            while (readyNodes.Count != 0)
            {
                var node = readyNodes.Pop();
                pin_map.Clear();
                BaseRenderNode renderNode = SetupNode(context, node);
                if (renderNode == null) return result;
                var node_type = node.nodeType.GetCustomAttribute<RenderNodeTypeAttribute>().type;
                switch (node_type)
                {
                    case RenderNodeTypeAttribute.Type.OutputNode:
                        // setup rendertarget
                        result = ((BaseOutputNode)renderNode).result.handle;
                        return result;
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

                context.Context.ExecuteCommandBuffer(context.CmdBuffer);
                context.CmdBuffer.Clear();

                if (node.sampler == null) node.sampler = UnityEngine.Profiling.CustomSampler.Create(node.nodeName + node.GetHashCode(), true);
                context.CmdBuffer.BeginSample(node.sampler);
                renderNode.Excute(context);
                context.CmdBuffer.EndSample(node.sampler);

                context.Context.ExecuteCommandBuffer(context.CmdBuffer);
                context.CmdBuffer.Clear();
                
                ReleaseNode(context, renderNode);
            }

            context.Context.ExecuteCommandBuffer(context.CmdBuffer);
            context.CmdBuffer.Clear();

            return result;
        }

        int temp_id;
        Dictionary<RenderGraphNode, Dictionary<string, System.Object>> valuePool = new Dictionary<RenderGraphNode, Dictionary<string, object>>();
        Dictionary<string, int> pinPool = new Dictionary<string, int>();
        Dictionary<RenderGraphNode, int> dependency = new Dictionary<RenderGraphNode, int>();
        Stack<RenderGraphNode> readyNodes = new Stack<RenderGraphNode>();
        Dictionary<string, Tuple<object, Type>> pin_map = new Dictionary<string, Tuple<object, Type>>();
        Dictionary<string, object> existValue = new Dictionary<string, object>();

        class NodeRec
        {
            public BaseRenderNode node;
            public Dictionary<string, FieldInfo> parameters;
            public List<Tuple<FieldInfo, object>> inputs, outputs;
        }
        Dictionary<int, NodeRec> nodes = new Dictionary<int, NodeRec>();

        NodeRec GetNodeInstance(RenderGraphNode node)
        {
            NodeRec res;
            if (!nodes.ContainsKey(node.GetHashCode()))
            {
                var nodeRec = new NodeRec();
                nodeRec.node = System.Activator.CreateInstance(node.nodeType) as BaseRenderNode;
                var node_instance = System.Activator.CreateInstance(node.nodeType);
                var field_infos = ReflectionUtil.GetFieldInfo(node.nodeType);
                nodeRec.inputs = new List<Tuple<FieldInfo, object>>(field_infos.Item1.Count);
                nodeRec.outputs = new List<Tuple<FieldInfo, object>>(field_infos.Item2.Count);
                nodeRec.parameters = new Dictionary<string, FieldInfo>(field_infos.Item3.Count);
                foreach (var item in field_infos.Item1)
                {
                    nodeRec.inputs.Add(new Tuple<FieldInfo, object>(item, item.GetValue(node_instance)));
                }
                foreach (var item in field_infos.Item2)
                {
                    nodeRec.outputs.Add(new Tuple<FieldInfo, object>(item, item.GetValue(node_instance)));
                }
                foreach (var item in field_infos.Item3)
                {
                    nodeRec.parameters.Add(item.Name, item);
                }
                res = nodeRec;
                nodes.Add(node.GetHashCode(), res);
            }
            else
            {
                res = nodes[node.GetHashCode()];
                //res.node = System.Activator.CreateInstance(node.nodeType) as BaseRenderNode;
                foreach (var item in res.inputs)
                {
                    item.Item1.FieldType.GetMethod("Move").Invoke(item.Item1.GetValue(res.node), new object[] { item.Item2 });
                }
                foreach (var item in res.outputs)
                {
                    item.Item1.FieldType.GetMethod("Move").Invoke(item.Item1.GetValue(res.node), new object[] { item.Item2 });
                }
            }
            return res;
        }

        BaseRenderNode SetupNode(RenderContext context, RenderGraphNode node)
        {
            List<RenderGraphNode> next_nodes = new List<RenderGraphNode>();

            var nodeRec = GetNodeInstance(node);
            var node_instance = nodeRec.node;

            foreach (var parm in node.parameters)
            {
                nodeRec.parameters[parm.name].SetValue(node_instance, parm.value);
            }

            existValue.Clear();
            if (valuePool.ContainsKey(node))
            {
                existValue = valuePool[node];
            }

            foreach (var input_value in nodeRec.inputs)
            {
                var input = input_value.Item1;
                if (existValue.ContainsKey(input.Name))
                {
                    var from_pin = existValue[input.Name];
                    var from_pin_name = input.FieldType.GetField("name").GetValue(from_pin) as string;

                    var compare_method = input.FieldType.GetMethod("Compare");
                    var generic_types = from_pin.GetType().BaseType.GetGenericArguments();
                    bool same = (bool)compare_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                    if (same && (node.nodeType == typeof(TextureDebug) || pinPool[from_pin_name] == 1))
                    {
                        input.FieldType.GetMethod("Move").Invoke(input.GetValue(node_instance), new object[] { from_pin });
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
                            pinPool[name] = 1;
                            var cast_method = input.FieldType.GetMethod("CastFrom");
                            cast_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                            if (--pinPool[from_pin_name] == 0)
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
                        Debug.LogError("Must connect Pin \"" + input.Name + "\" of \"" + node.nodeName + "\" is not connected.");
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
                        pinPool[input.FieldType.GetField("name").GetValue(input.GetValue(node_instance)) as string] = 1;
                    }
                }
                pin_map[input.Name] = new Tuple<object, Type>(input.GetValue(node_instance), input.FieldType);
            }

            var out_edges = m_graph.SearchNodeInDic(node).Item2;

            foreach (var output_value in nodeRec.outputs)
            {
                var output = output_value.Item1;
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
                    pinPool[name] = 0;
                    output.FieldType.GetField("name").SetValue(value, name);

                }
                else
                {
                    value = pin_map[output.Name].Item1;
                }

                pinPool[output.FieldType.GetField("name").GetValue(value) as string] += edges.Count;

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

            return nodeRec.node;
        }

        void ReleaseNode(RenderContext context, BaseRenderNode node)
        {
            foreach (var pin in pin_map)
            {
                var name = pin.Value.Item2.GetField("name").GetValue(pin.Value.Item1) as string;
                if (pinPool.ContainsKey(name))
                {
                    if (--pinPool[name] > 0) continue;
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

#endif
}