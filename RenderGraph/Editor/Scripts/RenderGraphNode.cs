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

namespace HypnosRenderPipeline.RenderGraph
{
    [Serializable]
    internal class RenderGraphNode : ISerializationCallbackReceiver
    {
        #region Parameter Define

        [Serializable]
        public class Parameter : ISerializationCallbackReceiver
        {
            public Type type;
            [SerializeField]
            public string name;
            [SerializeField]
            public System.Object value;

            public FieldInfo raw_data;

            [SerializeField]
            string type_str;
            [SerializeField]
            byte[] value_bytes;
            public void OnAfterDeserialize()
            {
                type = ReflectionUtil.GetTypeFromName(type_str);
                if (type != null && value_bytes != null && value_bytes.Length != 0)
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
                    MemoryStream stream = new MemoryStream();
                    new XmlSerializer(type).Serialize(stream, value);
                    value_bytes = stream.GetBuffer();
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
        public List<RenderGraphNode> parent, child;

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

        public void Init(Type t) 
        {
            inputs = new List<Slot>();
            outputs = new List<Slot>();
            parent = new List<RenderGraphNode>();
            child = new List<RenderGraphNode>();


            if (parameters == null)
                parameters = new List<Parameter>();

            nodeName = "UNKNOWN";

            nodeType = t;
            if (nodeType == null)
            {
                Debug.LogError(string.Format("Load RenderNode \"{0}\" faild! This may caused by mismatched RG version and scripts version.", nodeName));
                return;
            }

            nodeName = ReflectionUtil.GetLastNameOfType(t);

            if (!ReflectionUtil.IsBasedRenderNode(t)) 
            {
                Debug.LogError(string.Format("Load RenderNode \"{0}\" faild! RenderNode must inherit from BaseRenderNode.", nodeName));
                return;
            }

            var field_infos = ReflectionUtil.GetFieldInfo(nodeType);
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
        }
    }
}