using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using System.CodeDom.Compiler;
using System.Linq;

namespace HypnosRenderPipeline.RenderGraph
{
    public interface HRGExecutor
    {
        void Init();

        int Execute(RenderContext context, bool debug = false);

        void Dispose();
    }



    public class HRGCompiler
    {
#if UNITY_EDITOR

        HypnosRenderGraph m_graph;
        HypnosRenderGraph m_origin_graph;

        class CodeGenerator
        {
            StringBuilder m_code;
            int m_indentation;

            public CodeGenerator()
            {
                m_code = new StringBuilder();
                m_indentation = 0;
            }
            public void Indent()
            {
                m_indentation++;
            }
            public void Deindent()
            {
                m_indentation = Mathf.Max(0, m_indentation - 1);
            }
            public void Line(string line = null)
            {
                for (int i = 0; i < m_indentation; i++)
                {
                    m_code.Append("    ");
                }
                if (line != null) m_code.AppendLine(line);
                else m_code.AppendLine("");
            }

            public void Comment(string c)
            {
                Line("// " + c);
            }

            public void Brace()
            {
                Line("{");
                Indent();
            }
            public void EndBrace()
            {
                Deindent();
                Line("}");
            }

            public void Add(string value)
            {
                for (int i = 0; i < m_indentation; i++)
                {
                    m_code.Append("    ");
                }
                m_code.Append(value);
            }

            public string DecclareName(Type type)
            {
                return type.FullName.Replace("+", ".").Replace("HypnosRenderPipeline.RenderPass.", "");
            }

            public void Declare(Type type, string name, string value = null)
            {
                if (value != null)
                {
                    Line(DecclareName(type) + " " + name + " = " + value + ";");
                }
                else
                {
                    Line(DecclareName(type) + " " + name + ";");
                }
            }
            public void Declare<type>(string name, string value = null)
            {
                Declare(typeof(type), name, value);
            }

            public void New(Type type, string name)
            {
                Declare(type, name, "new " + DecclareName(type) + "()");
            }

            public void New<type>(string name)
            {
                New(typeof(type), name);
            }

            public void SetValue(Type type, string name, object value)
            {

                if (type.IsEnum)
                {
                    Line(name + " = " + DecclareName(type) + "." + value + ";");
                }
                else if (type == typeof(float))
                {
                    Line(name + " = " + value + "f;");
                }
                else if (type == typeof(bool))
                {
                    Line(name + " = " + value.ToString().ToLower() + ";");
                }
                else if (type == typeof(Vector2))
                {
                    Vector2 v = (Vector2)value;
                    Line(name + " = new Vector2(" + v.x + "f, " + v.y + "f);");
                }
                else if (type == typeof(Vector3))
                {
                    Vector3 v = (Vector3)value;
                    Line(name + " = new Vector3(" + v.x + "f, " + v.y + "f, " + v.z + "f);");
                }
                else if (type == typeof(Vector4))
                {
                    Vector4 v = (Vector4)value;
                    Line(name + " = new Vector4(" + v.x + "f, " + v.y + "f, " + v.z + "f, " + v.w + "f);");
                }
                else if (type == typeof(Vector2Int))
                {
                    Vector2Int v = (Vector2Int)value;
                    Line(name + " = new Vector2Int(" + v.x + ", " + v.y + ");");
                }
                else if (type == typeof(Vector3Int))
                {
                    Vector3Int v = (Vector3Int)value;
                    Line(name + " = new Vector3Int(" + v.x + ", " + v.y + ", " + v.z + ");");
                }
                else if (type == typeof(Color))
                {
                    Color v = (Color)value;
                    Line(name + " = new Color(" + v.r + "f, " + v.g + "f, " + v.b + "f, " + v.a + "f);");
                }
                else if (type == typeof(LayerMask))
                {
                    LayerMask v = (LayerMask)value;
                    Line(name + " = " + v.value + ";");
                }
                else
                {
                    Line(name + " = " + value.ToString() + ";");
                }
            }
            public void SetValue<type>(string name, object value)
            {
                SetValue(typeof(type), name, value);
            }

            public string Result() { return m_code.ToString(); }

            public struct AutoBrance_ : IDisposable
            {
                CodeGenerator m_c;
                public AutoBrance_(CodeGenerator c)
                {
                    m_c = c;
                    c.Brace();
                }

                public void Dispose()
                {
                    m_c.EndBrace();
                }
            }

            public AutoBrance_ AutoBrance(string title = null) {
                if (title != null) Line(title);
                return new AutoBrance_(this); 
            }

        }
        CodeGenerator m_code;


        public HRGCompiler(HypnosRenderGraph graph)
        {
            m_origin_graph = graph;
            m_graph = graph.Copy();
            m_code = new CodeGenerator();
        }

        public string Compile()
        {
            ClearUselessNodes();

            FindEnterPoints();

            m_code.Line("using HypnosRenderPipeline.RenderPass;");
            m_code.Line("using UnityEngine;");
            m_code.Line();

            using (m_code.AutoBrance("namespace HypnosRenderPipeline.RenderGraph"))
            {
                using (m_code.AutoBrance("public class HRG_" + m_graph.name + "_Executor : HRGExecutor"))
                {
                    // nodes
                    m_code.Comment("Nodes:");
                    m_code.Comment("----------------------------");
                    int postfix = 0;
                    foreach (var node in m_graph.nodes)
                    {
                        var name = node.nodeName + (postfix++).ToString();
                        nodeName[node] = name;
                        m_code.Declare(node.nodeType, name);
                    }
                    m_code.Comment("----------------------------");
                    m_code.Line();

                    m_code.Comment("ShaderIDs:");
                    m_code.Comment("----------------------------");
                    HashSet<string> pinNames = new HashSet<string>();
                    foreach (var node in m_graph.nodes)
                    {
                        foreach (var p in node.inputs)
                            pinNames.Add(nodeName[node] + "_" + p.name);
                        foreach (var p in node.outputs)
                            pinNames.Add(nodeName[node] + "_" + p.name);
                    }
                    foreach (var p in pinNames)
                        m_code.Declare<int>(p, "Shader.PropertyToID(\"" + p.Replace("_", ".") + "\")");
                    m_code.Comment("----------------------------");
                    m_code.Line();


                    // init
                    using (m_code.AutoBrance("public void Init()"))
                    {
                        foreach (var node in m_graph.nodes)
                        {
                            SetupNodeProperties(node);
                        }
                    }

                    m_code.Line("");

                    using (m_code.AutoBrance("public int Execute(RenderContext context, bool debug = false)"))
                    {

                        {
                            m_code.Declare<int>("result", "-1");

                            m_code.Line("");

                            while (readyNodes.Count != 0)
                            {
                                var node = readyNodes.Pop();
                                using (m_code.AutoBrance("//" + nodeName[node]))
                                {
                                    bool enabled;
                                    bool has_disExecute = false;
                                    bool has_post = false;
                                    m_code.Line("context.commandBuffer.name = \"" + nodeName[node] + "\";");
                                    using (m_code.AutoBrance("// preprocess node"))
                                    {
                                        bool execute_preprocess;
                                        enabled = PrepareNode(node, out execute_preprocess);
                                        if (execute_preprocess)
                                        {
                                            m_code.Line("context.context.ExecuteCommandBuffer(context.commandBuffer);");
                                            m_code.Line("context.commandBuffer.Clear();");
                                        }

                                    }
                                    m_code.Line("// perform node");
                                    {
                                        var node_type = node.nodeType.GetCustomAttribute<RenderNodeTypeAttribute>().type;
                                        if (node_type == RenderNodeTypeAttribute.Type.OutputNode)
                                        {
                                            m_code.Line("result = " + nodeName[node] + ".result.handle;");
                                            m_code.Line("return result;");
                                            break;
                                        }
                                        if (enabled)
                                        {
                                            m_code.Line(nodeName[node] + ".Execute(context);");
                                        }
                                        else if (node.nodeType.GetMethod("DisExecute").DeclaringType != typeof(BaseRenderNode))
                                        {
                                            has_disExecute = true;
                                            m_code.Line(nodeName[node] + ".DisExecute(context);");
                                        }
                                    }
                                    using (m_code.AutoBrance("// postprocess node"))
                                    {
                                        has_post = ReleaseNode(node);
                                        var node_name = nodeName[node];
                                        foreach (var pin in pin_map)
                                        {
                                            var name = node_name + "." + pin.Key;
                                            if (node.outputs.FindIndex(e => { return e.name == pin.Key; }) > 0)
                                            {
                                                m_code.SetValue<bool>(name + ".connected", enabled);
                                            }
                                        }
                                    }
                                    if (enabled || has_disExecute || has_post) {
                                        m_code.Line("context.context.ExecuteCommandBuffer(context.commandBuffer);");
                                        m_code.Line("context.commandBuffer.Clear();");

                                    }
                                    {
                                        var nodeRec = GetNodeInstance(node);
                                        foreach (var output_value in nodeRec.outputs)
                                        {
                                            var from_name = nodeName[node] + "." + output_value.Key;

                                            List<string> next_pins = new List<string>();
                                            foreach (var val in valuePool)
                                            {
                                                if (val.Value.first == from_name)
                                                    next_pins.Add(val.Key);
                                            }
                                            foreach (var n in next_pins)
                                            {
                                                var t = valuePool[n];
                                                t.last = enabled;
                                                valuePool[n] = t;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    m_code.Line("");

                    using (m_code.AutoBrance("public void Dispose()"))
                    {
                        foreach (var node in m_graph.nodes)
                        {
                            var nname = nodeName[node];
                            var info = GetNodeInstance(node);
                            using (m_code.AutoBrance("// " + nname))
                            {
                                HashSet<string> pins = new HashSet<string>();
                                foreach (var p in info.inputs)
                                    pins.Add(p.Value.first.Name);
                                foreach (var p in info.outputs)
                                    pins.Add(p.Value.first.Name);
                                foreach (var pn in pins)
                                {
                                    m_code.Line(nname + "." + pn + ".Dispose();");
                                }
                                m_code.Line(nname + ".Dispose();");
                            }
                        }
                    }
                }
            }

            // clear temp objects
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

            //string path = Application.dataPath + "/A.txt";
            //System.IO.FileStream fs = System.IO.File.OpenWrite(path);

            //var map = Encoding.UTF8.GetBytes(m_code.Result());
            //fs.Write(map, 0, map.Length);
            //fs.Close();

            CompileFromString(m_graph.name, m_code.Result());

            m_origin_graph.code = m_code.Result();
            m_origin_graph.recompiled = true;
            UnityEditor.EditorUtility.SetDirty(m_origin_graph);

            return m_code.Result();
        }



        bool ReleaseNode(RenderGraphNode node)
        {
            bool execute = false;
            var node_name = nodeName[node];
            foreach (var pin in pin_map)
            {
                var name = node_name + "." + pin.Key;
                if (pinPool.ContainsKey(name))
                {
                    if (--pinPool[name] > 0) continue;
                }
                execute = true;
                m_code.Line(name + ".ReleaseResourcces(context);");
            }
            return execute;
        }

        bool PrepareNode(RenderGraphNode node, out bool needExecute)
        {
            needExecute = false;

            var nodeInfo = GetNodeInstance(node);
            var node_instance = nodeInfo.node;
            var node_name = nodeName[node];
            pin_map.Clear();

            m_code.Comment("inputs");
            foreach (var input_value in nodeInfo.inputs)
            {
                var input_pin_name = node_name + "." + input_value.Key;
                var input_pin_name_str = "\"" + input_pin_name + "\"";

                var input = input_value.Value.first;
                bool connected = false;

                if (valuePool.ContainsKey(input_pin_name))
                {
                    var from_pin = valuePool[input_pin_name];
                    var from_pin_name = from_pin.first;

                    connected = from_pin.last;
                    m_code.SetValue<bool>(input_pin_name + ".connected", connected);

                    bool same = false;
                    if (!nodeInfo.outputs.ContainsKey(input.Name) || pinPool[from_pin_name] == 1)
                    {
                        var compare_method = input.FieldType.GetMethod("Compare");
                        same = (bool)compare_method.Invoke(input_value.Value.last, new object[] { parent_pin[input_pin_name] });

                    }

                    if (same)
                    {
                        m_code.Line(input_pin_name + ".Move(" + from_pin_name + ");");
                        pinPool[input_pin_name] = pinPool[from_pin_name];
                    }
                    else
                    {
                        needExecute = true;
                        var ca_cast_method = input.FieldType.GetMethod("CanCastFrom");
                        bool can_cast = (bool)ca_cast_method.Invoke(input_value.Value.last, new object[] { parent_pin[input_pin_name] });

                        if (can_cast)
                        {
                            m_code.Line(input_pin_name + ".AllocateResourcces(context, " + input_pin_name.Replace(".", "_") + ");");
                            m_code.Line(input_pin_name + ".name = " + input_pin_name_str + ";");
                            pinPool[input_pin_name] = 1;
                            m_code.Line(input_pin_name + ".CastFrom(context, " + from_pin_name + ");");
                            if (--pinPool[from_pin_name] == 0)
                            {
                                m_code.Line(from_pin_name + ".ReleaseResourcces(context);");
                            }
                        }
                        else
                        {
                            throw new Exception("Can't auto cast to \"" + input.Name + " \" of \"" + node.nodeName + "\"");
                        }
                    }
                }
                else
                {
                    needExecute = true;
                    // create resources for bare pin
                    m_code.Line(input_pin_name + ".AllocateResourcces(context, " + input_pin_name.Replace(".", "_") + ");");
                    m_code.Line(input_pin_name + ".name = " + input_pin_name_str + ";");
                    pinPool[input_pin_name] = 1;
                }
                pin_map[input.Name] = connected;
            }

            m_code.Comment("outputs");

            var out_edges = m_graph.SearchNodeInDic(node).Item2;

            foreach (var output_value in nodeInfo.outputs)
            {
                var output_pin_name = node_name + "." + output_value.Key;
                var output_pin_name_str = "\"" + output_pin_name + "\"";

                var output = output_value.Value.first;

                var edges = out_edges.FindAll(e => e.output.name == output.Name);

                if (!pin_map.ContainsKey(output.Name))
                {
                    needExecute = true;
                    // create resources for out pin
                    m_code.Line(output_pin_name + ".AllocateResourcces(context, " + output_pin_name.Replace(".", "_") + ");");
                    m_code.Line(output_pin_name + ".name = " + output_pin_name_str + ";");
                    pinPool[output_pin_name] = 1;
                    m_code.SetValue<bool>(output_pin_name + ".connected", edges.Count != 0);
                    pin_map[output.Name] = edges.Count != 0;
                }

                pinPool[output_pin_name] += edges.Count;

                foreach (var edge in edges)
                {
                    var next_pin_name = nodeName[edge.input.node] + "." + edge.input.name;

                    parent_pin[next_pin_name] = output_value.Value.last;
                    valuePool[next_pin_name] = new Pair<string, bool>(output_pin_name, pin_map[edge.output.name]);
                    if (--dependency[edge.input.node] == 0)
                    {
                        readyNodes.Push(edge.input.node);
                    }
                }
            }
            return (bool)node.parameters.Find((e) => { return e.name == "enabled"; }).value;
        }

        void SetupNodeProperties(RenderGraphNode node)
        {

            List<RenderGraphNode> next_nodes = new List<RenderGraphNode>();

            var name = nodeName[node];
            m_code.Line(name + " = new " + m_code.DecclareName(node.nodeType) + "();");

            foreach (var parm in node.parameters)
            {
                m_code.SetValue(parm.type, name + "." + parm.name, parm.value);
            }
        }

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
            }
            return res;
        }

        void ClearUselessNodes()
        {
            while (true)
            {
                List<RenderGraphNode> nodes = new List<RenderGraphNode>();
                foreach (var node in m_graph.node_edge)
                {
                    if (node.Value.Item2.Count == 0)
                    {
                        if (node.Key.nodeType != typeof(OutputNode))
                        {
                            nodes.Add(node.Key);
                        }
                    }
                }
                foreach (var node in nodes)
                {
                    m_graph.RemoveNode(node);
                }
                if (nodes.Count == 0) break;
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

        struct Pair<T1, T2>
        {
            public T1 first;
            public T2 last;
            public Pair(T1 first, T2 last) { this.first = first; this.last = last; }
        }

        Dictionary<RenderGraphNode, int> dependency = new Dictionary<RenderGraphNode, int>();
        Stack<RenderGraphNode> readyNodes = new Stack<RenderGraphNode>();

        Dictionary<RenderGraphNode, string> nodeName = new Dictionary<RenderGraphNode, string>();

        Dictionary<string, Pair<string, bool>> valuePool = new Dictionary<string, Pair<string, bool>>();
        Dictionary<string, int> pinPool = new Dictionary<string, int>();
        Dictionary<string, bool> pin_map = new Dictionary<string, bool>();
        Dictionary<string, object> parent_pin = new Dictionary<string, object>();

        class NodeRec
        {
            public BaseRenderNode node;
            public Dictionary<string, FieldInfo> parameters;
            public Dictionary<string, Pair<FieldInfo, object>> inputs, outputs;
        }
        Dictionary<int, NodeRec> nodes = new Dictionary<int, NodeRec>();



        [UnityEditor.MenuItem("HypnosRenderPipeline/Pipeline Graph/Compile Current")]
        static public void CompileButton()
        {
            Compile((UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset as HypnosRenderPipelineAsset).hypnosRenderPipelineGraph);
        }

        public static void Compile(HypnosRenderGraph graph)
        {
            HRGCompiler hRGCompiler = new HRGCompiler(graph);
            hRGCompiler.Compile();
        }
#endif

        public static HRGExecutor CompileFromString(string name, string code)
        {
            Tools.CSharpCodeCompiler cdp = new Tools.CSharpCodeCompiler();

            CompilerParameters cp = new CompilerParameters();
            cp.ReferencedAssemblies.Add(typeof(HRGCompiler).Assembly.Location);
            foreach (var ass in typeof(HRGCompiler).Assembly.GetReferencedAssemblies())
            {
                if (ass.Name != "System.Core")
                {
                    var refAss = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == ass.Name);
                    if (refAss != null)
                        cp.ReferencedAssemblies.Add(refAss.Location);
                }
            }

            try
            {
                cp.ReferencedAssemblies.Add(Assembly.Load("Assembly-CSharp").Location);
            }
            catch (Exception) { }

            cp.GenerateExecutable = false;
            cp.GenerateInMemory = true;
            cp.TempFiles = new TempFileCollection(System.IO.Path.GetTempPath());
            CompilerResults cr = cdp.CompileAssemblyFromSource(cp, code);

            if (cr.Errors.HasErrors)
            {
                string errorString = "Compile HRG \"" + name + "\" faild!\n";
                foreach (var et in cr.Errors)
                {
                    errorString += (et as CompilerError).ErrorText + "\n";
                }
                throw new Exception(errorString);
            }
            else
            {
                Assembly ass = cr.CompiledAssembly;
                Type type = ass.GetType(string.Format("{0}.{1}", "HypnosRenderPipeline.RenderGraph", "HRG_" + name + "_Executor"));
                return Activator.CreateInstance(type) as HRGExecutor;
            }
        }
    }
}