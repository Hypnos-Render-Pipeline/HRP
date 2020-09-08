using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        public int Excute(RenderContext context, bool debug = false)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Excute HRG");

            int result = -1;
            readyNodes.Clear();
            valuePool.Clear();
            dependency.Clear();
            pinPool.Clear();

            temp_id = context.camera.GetHashCode();
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
                        if (!debug) break;
                        if (node.nodeType == typeof(TextureDebug))
                        {
                            var debugNode = renderNode as TextureDebug;
                            if (node.debugTex == null)
                            {
                                var desc = debugNode.tex.desc.basicDesc;
                                desc.colorFormat = RenderTextureFormat.ARGBHalf;
                                node.debugTex = new RenderTexture(desc);
                                node.debugTexDesc = debugNode.tex.desc.basicDesc;
                            }
                            else
                            {
                                var desc1 = node.debugTexDesc;
                                var desc2 = debugNode.tex.desc.basicDesc;
                                var desc = desc2;
                                desc.colorFormat = RenderTextureFormat.ARGBHalf;
                                if (desc1.width != desc2.width || desc1.height != desc2.height || desc1.colorFormat != desc2.colorFormat)
                                {
                                    node.debugTex.Release();
                                    node.debugTex = new RenderTexture(desc);
                                    node.debugTexDesc = debugNode.tex.desc.basicDesc;
                                }
                            }
                            node.debugTex.name = debugNode.tex.name;
                            debugNode.texture = node.debugTex;
                        }
                        break;
                    default:
                        break;
                };

                if (debug)
                {
                    if (node.sampler == null/* || node.sampler.isValid == false*/) node.sampler = UnityEngine.Profiling.CustomSampler.Create(node.nodeName + node.GetHashCode(), true);
                    context.commandBuffer.BeginSample(node.sampler);
                }
                context.context.ExecuteCommandBuffer(context.commandBuffer);
                context.commandBuffer.Clear();
                if (node.nodeType != typeof(TextureDebug) || debug)
                {
                    if (renderNode.enabled)
                        renderNode.Excute(context);
                }
                if (debug)
                    context.commandBuffer.EndSample(node.sampler);
                context.context.ExecuteCommandBuffer(context.commandBuffer);
                context.commandBuffer.Clear();
                                
                ReleaseNode(context, renderNode);

                if (renderNode.enabled)
                {
                    var nodeRec = GetNodeInstance(node);
                    foreach (var output_value in nodeRec.outputs)
                    {
                        var output = output_value.Value.first;
                        var nameField = output.FieldType.GetField("name");
                        output.FieldType.GetField("connected").SetValue(output.GetValue(renderNode), true);
                    }
                }
            }

            context.context.ExecuteCommandBuffer(context.commandBuffer);
            context.commandBuffer.Clear();

            UnityEngine.Profiling.Profiler.EndSample();
            return result;
        }

        int temp_id;

        struct Pair<T1, T2>
        {
            public T1 first;
            public T2 last;
            public Pair(T1 first, T2 last) { this.first = first; this.last = last; }
        }

        Dictionary<RenderGraphNode, Dictionary<string, System.Object>> valuePool = new Dictionary<RenderGraphNode, Dictionary<string, object>>();
        Dictionary<string, int> pinPool = new Dictionary<string, int>();
        Dictionary<RenderGraphNode, int> dependency = new Dictionary<RenderGraphNode, int>();
        Stack<RenderGraphNode> readyNodes = new Stack<RenderGraphNode>();
        Dictionary<string, Pair<object, Type>> pin_map = new Dictionary<string, Pair<object, Type>>();
        Dictionary<string, object> existValue = new Dictionary<string, object>();

        class NodeRec
        {
            public BaseRenderNode node;
            public Dictionary<string, FieldInfo> parameters;
            public Dictionary<string, Pair<FieldInfo, object>> inputs, outputs;
        }
        Dictionary<int, NodeRec> nodes = new Dictionary<int, NodeRec>();

        NodeRec GetNodeInstance(RenderGraphNode node)
        {
            NodeRec res;
            if (!nodes.ContainsKey(node.GetHashCode()))
            {
                var nodeRec = new NodeRec();
                nodeRec.node = System.Activator.CreateInstance(node.nodeType) as BaseRenderNode;
                //var node_instance = System.Activator.CreateInstance(node.nodeType);
                var field_infos = ReflectionUtil.GetFieldInfo(node.nodeType);
                nodeRec.inputs = new Dictionary<string, Pair<FieldInfo, object>>(field_infos.Item1.Count);
                nodeRec.outputs = new Dictionary<string, Pair<FieldInfo, object>>(field_infos.Item2.Count);
                nodeRec.parameters = new Dictionary<string, FieldInfo>(field_infos.Item3.Count);
                foreach (var item in field_infos.Item1)
                {
                    nodeRec.inputs.Add(item.Name, new Pair<FieldInfo, object>(item, item.GetValue(nodeRec.node)));
                }
                foreach (var item in field_infos.Item2)
                {
                    nodeRec.outputs.Add(item.Name, new Pair<FieldInfo, object>(item, item.GetValue(nodeRec.node)));
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
                ////res.node = System.Activator.CreateInstance(node.nodeType) as BaseRenderNode;
                //foreach (var item in res.inputs)
                //{
                //    item.Value.first.FieldType.GetMethod("Move").Invoke(item.Value.first.GetValue(res.node), new object[] { item.Value.last });
                //}
                //foreach (var item in res.outputs)
                //{
                //    item.Value.first.FieldType.GetMethod("Move").Invoke(item.Value.first.GetValue(res.node), new object[] { item.Value.last });
                //}
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
                var input = input_value.Value.first;
                var pin = input.GetValue(node_instance);
                var connectedField = input.FieldType.GetField("connected");
                var nameField = input.FieldType.GetField("name");
                if (existValue.ContainsKey(input.Name))
                {
                    var from_pin = existValue[input.Name];
                    var from_pin_name = nameField.GetValue(from_pin) as string;

                    connectedField.SetValue(pin, connectedField.GetValue(from_pin));

                    var compare_method = input.FieldType.GetMethod("Compare");
                    bool same = (bool)compare_method.Invoke(pin, new object[] { context, from_pin });
                    if (same && (!nodeRec.outputs.ContainsKey(input.Name) || node.nodeType == typeof(TextureDebug) || pinPool[from_pin_name] == 1))
                    {
                        input.FieldType.GetMethod("Move").Invoke(pin, new object[] { from_pin });
                    }
                    else
                    {
                        var ca_cast_method = input.FieldType.GetMethod("CanCastFrom");
                        bool can_cast = (bool)ca_cast_method.Invoke(pin, new object[] { context, from_pin });
                        if (can_cast)
                        {
                            var init_method = input.FieldType.GetMethod("AllocateResourcces");
                            string name = input.Name + temp_id++;
                            int id = Shader.PropertyToID(name);
                            init_method.Invoke(pin, new object[] { context, id });
                            nameField.SetValue(pin, name);
                            pinPool[name] = 1;
                            var cast_method = input.FieldType.GetMethod("CastFrom");
                            cast_method.Invoke(pin, new object[] { context, from_pin });
                            if (--pinPool[from_pin_name] == 0)
                            {
                                var release_method = input.FieldType.GetMethod("ReleaseResourcces");
                                release_method.Invoke(from_pin, new object[] { context });
                            }
                        }
                        else
                        {
                            Debug.LogError("Can't auto cast to \"" + input.Name + " \" of \"" + node.nodeName + "\"");
                            return null;
                        }
                    }
                }
                else
                {
                    connectedField.SetValue(pin, false);
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
                pin_map[input.Name] = new Pair<object, Type>(input.GetValue(node_instance), input.FieldType);
            }

            var out_edges = m_graph.SearchNodeInDic(node).Item2;

            foreach (var output_value in nodeRec.outputs)
            {
                var output = output_value.Value.first;
                var nameField = output.FieldType.GetField("name");
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
                    pinPool[name] = 1;
                    nameField.SetValue(value, name);
                    output.FieldType.GetField("connected").SetValue(value, false);
                    pin_map[output.Name] = new Pair<object, Type>(value, output.FieldType);

                }
                else
                {
                    value = pin_map[output.Name].first;
                }

                pinPool[nameField.GetValue(value) as string] += edges.Count;

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
                var name = pin.Value.last.GetField("name").GetValue(pin.Value.first) as string;
                if (pinPool.ContainsKey(name))
                {
                    if (--pinPool[name] > 0) continue;
                }
                var release_method = pin.Value.last.GetMethod("ReleaseResourcces");
                release_method.Invoke(pin.Value.first, new object[] { context });
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

        public void Dispose()
        {
            HashSet<string> set = new HashSet<string>();
            foreach (var rec in nodes)
            {
                set.Clear();
                var nodeRec = rec.Value;
                var node_instance = nodeRec.node;
                foreach (var input_value in nodeRec.inputs)
                {
                    var input = input_value.Value.first;

                    var pin = input.GetValue(node_instance);
                    set.Add(input.FieldType.Name);

                    input.FieldType.GetMethod("Dispose").Invoke(pin, new object[] { });
                }
                foreach (var input_value in nodeRec.outputs)
                {
                    var input = input_value.Value.first;

                    var pin = input.GetValue(node_instance);
                    if (set.Contains(input.FieldType.Name)) continue;

                    input.FieldType.GetMethod("Dispose").Invoke(pin, new object[] { });
                }
                node_instance.Dispose();
            }
        }
    }

#endif
}