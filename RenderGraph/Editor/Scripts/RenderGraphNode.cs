using HypnosRenderPipeline.RenderPass;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Profiling;

namespace HypnosRenderPipeline.RenderGraph
{
    [Serializable]
    internal class RenderGraphNode : ISerializationCallbackReceiver
    {
        #region Parameter Define

        [Serializable]
        public class Parameter : ISerializationCallbackReceiver
        {
            public Type type = null;
            [SerializeField]
            public string name = null;
            [SerializeField]
            public System.Object value = null;

            public FieldInfo raw_data = null;

            [SerializeField]
            string type_str = null;
            [SerializeField]
            byte[] value_bytes = null;
            [SerializeField]
            public UnityEngine.Object obj_ref = null;
            public void OnAfterDeserialize()
            {
                type = ReflectionUtil.GetTypeFromName(type_str);
                if (type == null) return;
                if (ReflectionUtil.IsEngineObject(type))
                {
                    if (obj_ref != null)
                        value = obj_ref;
                }
                else if (value_bytes != null && value_bytes.Length != 0)
                {
                    try
                    {
                        MemoryStream stream = new MemoryStream(value_bytes);
                        value = new XmlSerializer(type).Deserialize(stream);
                    }
                    catch
                    {
                        if (type != null) value = System.Activator.CreateInstance(type);
                        Debug.LogWarning(string.Format("Load data \"{0}: {1}\" faild!", type_str, name));
                    }
                }
            }

            public void OnBeforeSerialize()
            {
                if (type != null) type_str = type.ToString();
                if (value != null)
                {
                    if (ReflectionUtil.IsEngineObject(type))
                    {
                        obj_ref = value as UnityEngine.Object;
                    }
                    else
                    {
                        MemoryStream stream = new MemoryStream();
                        new XmlSerializer(type).Serialize(stream, value);
                        value_bytes = stream.GetBuffer();
                    }
                }
            }

        }

        #endregion

        #region Slot Define

        [Serializable]
        public class Slot
        {
            public Type slotType;
            public string name;
            public string info;
            public bool mustConnect;
        }

        #endregion

        #region Properties

        public Type nodeType;
        [SerializeField]
        public string nodeName;

        [SerializeField]
        public List<Parameter> parameters;

        public List<Slot> inputs, outputs;

        [NonSerialized]
        public HashSet<RenderGraphNode> parent, child;

        [SerializeField]
        public Rect positon;

        public RenderGraphNodeView NodeView;

        #endregion

        #region Serialize

        [SerializeField]
        string nodeType_str;
        public void OnBeforeSerialize()
        {
            if (nodeType != null)
                nodeType_str = nodeType.ToString();
        }

        public void OnAfterDeserialize()
        {
            //Debug.Log(this + ": " + "OnAfterDeserialize");
            nodeType = ReflectionUtil.GetTypeFromName(nodeType_str);
        }

        #endregion

        [NonSerialized]
        public RenderTexture debugTex;

        [NonSerialized]
        public CustomSampler sampler;

        public RenderGraphNode()
        {
            parent = new HashSet<RenderGraphNode>();
            child = new HashSet<RenderGraphNode>();
        }

        void BindSlots(List<FieldInfo> infos, List<Slot> slots)
        {
            foreach (var info in infos)
            {
                var tipattri = info.GetCustomAttribute<TooltipAttribute>();
                string tips = tipattri != null ? tipattri.tooltip : "";
                var pinInfo = info.GetCustomAttribute<BaseRenderNode.NodePinAttribute>();

                string name = info.Name;// + " (" + ReflectionUtil.GetLastNameOfType(info.FieldType) + ")";

                slots.Add(new Slot()
                {
                    slotType = info.FieldType,
                    name = name,
                    info = tips,
                    mustConnect = pinInfo.mustConnect
                });
            }
        }

        public bool Init(Type t) 
        {
            inputs = new List<Slot>();
            outputs = new List<Slot>();

            if (parameters == null)
                parameters = new List<Parameter>();

            nodeName = "UNKNOWN";

            nodeType = t;
            if (nodeType == null)
            {
                Debug.LogError(string.Format("Load RenderNode \"{0}\" faild! This may caused by mismatched RG version and scripts version.", nodeName));
                return false;
            }

            nodeName = ReflectionUtil.GetLastNameOfType(t);

            if (!ReflectionUtil.IsBasedRenderNode(t)) 
            {
                Debug.LogError(string.Format("Load RenderNode \"{0}\" faild! RenderNode must inherit from BaseRenderNode.", nodeName));
                return false;
            }

            var field_infos = ReflectionUtil.GetFieldInfo(nodeType);
            if (field_infos == null) return false;
            var input_fields = field_infos.Item1;
            var output_fields = field_infos.Item2;
            var param_fields = field_infos.Item3;

            BindSlots(input_fields, inputs);
            BindSlots(output_fields, outputs);


            List<Parameter> new_parameters = new List<Parameter>();

            foreach (var parm in param_fields)
            {
                string name = parm.Name;// + " (" + ReflectionUtil.GetLastNameOfType(parm.FieldType) + ")";
                bool find_saved = false;
                if (parameters != null)
                {
                    foreach (var saved_parm in parameters)
                    {
                        if (saved_parm.name == name && parm.FieldType == saved_parm.type)
                        {
                            new_parameters.Add(new Parameter() { type = parm.FieldType, name = name, raw_data = parm, value = saved_parm.value });
                            find_saved = true;
                            break;
                        }
                    }
                }
                if (!find_saved) new_parameters.Add(new Parameter() { type = parm.FieldType, name = name, raw_data = parm, 
                                                                        value = parm.FieldType.IsValueType ? Activator.CreateInstance(parm.FieldType) : null
            });
            }
            parameters = new_parameters;
            return true;
        }
    }
}