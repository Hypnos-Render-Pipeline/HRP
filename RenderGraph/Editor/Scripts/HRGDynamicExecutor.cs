using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Schema;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Must;
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
            readyNodes = new List<RenderGraphNode>();
            valuePool = new Dictionary<RenderGraphNode, Dictionary<string, object>>();
            dependency = new Dictionary<RenderGraphNode, int>();
            FindEnterPoints();

            for (int i = 0; i < readyNodes.Count; i++)
            {
                var node = readyNodes[i];
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
                            var tex_param = node.parameters.Find(p => p.name == "texture");
                            if (tex_param.value == null) 
                                tex_param.value = new RenderTexture(debugNode.tex.desc.basicDesc);
                            else
                            {
                                var tex = tex_param.value as RenderTexture;
                                var desc1 = tex.descriptor;
                                var desc2 = debugNode.tex.desc.basicDesc;
                                if (desc1.width != desc2.width || desc1.height != desc2.height || desc1.colorFormat != desc2.colorFormat)
                                {
                                    tex.Release();
                                    tex_param.value = new RenderTexture(desc2);
                                }
                            }
                            debugNode.texture = tex_param.value as RenderTexture;
                        }
                        break;
                    default:
                        break;
                };
                renderNode.Excute(context);
            }
            return true;
        }

        Dictionary<RenderGraphNode, Dictionary<string, System.Object>> valuePool;
        Dictionary<RenderGraphNode, int> dependency;
        List<RenderGraphNode> readyNodes;


        BaseRenderNode SetupNode(RenderContext context, RenderGraphNode node)
        {
            var field_infos = ReflectionUtil.GetFieldInfo(node.nodeType);

            var node_instance = System.Activator.CreateInstance(node.nodeType);

            foreach (var parm in field_infos.Item3)
            {
                parm.SetValue(node_instance, node.parameters.Find(p => p.name == parm.Name).value);
            }

            Dictionary<string, System.Object> existValue = new Dictionary<string, object>();
            if (valuePool.ContainsKey(node))
            {
                existValue = valuePool[node];
            }


            Dictionary<string, System.Object> pin_map = new Dictionary<string, System.Object>();

            foreach (var input in field_infos.Item1)
            {
                if (existValue.ContainsKey(input.Name))
                {
                    var from_pin = existValue[input.Name];

                    var compare_method = input.FieldType.GetMethod("Compare");
                    var generic_types = from_pin.GetType().BaseType.GetGenericArguments();
                    bool same = (bool)compare_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                    if (same)
                    {
                        var move_method = input.FieldType.GetMethod("MoveFrom");
                        move_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { from_pin });
                    }
                    else
                    {
                        var ca_cast_method = input.FieldType.GetMethod("CanCastFrom");
                        bool can_cast = (bool)ca_cast_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
                        if (can_cast)
                        {
                            var init_method = input.FieldType.GetMethod("AllocateResourcces");
                            int id = Shader.PropertyToID(input.Name + UnityEngine.Random.Range(0, 1000000));
                            init_method.Invoke(input.GetValue(node_instance), new object[] { context, id });
                            var cast_method = input.FieldType.GetMethod("CastFrom");
                            cast_method.MakeGenericMethod(generic_types).Invoke(input.GetValue(node_instance), new object[] { context, from_pin });
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
                        int id = Shader.PropertyToID(input.Name + UnityEngine.Random.Range(0, 1000000));
                        init_method.Invoke(input.GetValue(node_instance), new object[] { context, id });
                    }
                }
                pin_map[input.Name] = input.GetValue(node_instance);
            }

            var out_edges = m_graph.SearchNodeInDic(node).Item2;

            foreach (var output in field_infos.Item2)
            {
                System.Object value = null;
                if (!pin_map.ContainsKey(output.Name))
                {
                    // create resources for out pin
                    var init_method = output.FieldType.GetMethod("AllocateResourcces");
                    int id = Shader.PropertyToID(output.Name + UnityEngine.Random.Range(0, 1000000));
                    init_method.Invoke(output.GetValue(node_instance), new object[] { context, id });
                    value = output.GetValue(node_instance);
                }
                else
                {
                    value = pin_map[output.Name];
                }

                var edges = out_edges.FindAll(e => e.output.name == output.Name);
                foreach (var edge in edges)
                {
                    if (!valuePool.ContainsKey(edge.input.node))
                        valuePool[edge.input.node] = new Dictionary<string, object>();

                    valuePool[edge.input.node].Add(edge.input.name, value);
                    if (--dependency[edge.input.node] == 0)
                    {
                        readyNodes.Add(edge.input.node);
                    }
                }
            }

            return node_instance as BaseRenderNode;
        }



        void FindEnterPoints()
        {
            foreach (var node in m_graph.node_edge)
            {
                if (node.Value.Item1.Count == 0)
                {
                    readyNodes.Add(node.Key);
                }
                else
                {
                    dependency[node.Key] = node.Value.Item1.Count;
                }
            }
        }
    }



}