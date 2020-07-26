using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
#if UNITY_EDITOR

    public abstract class ReflectionUtil
    {
        public static string GetLastNameOfType(Type t)
        {
            if (t == null) return null;
            var ts = t.ToString().Split('.');
            ts = ts[ts.Length - 1].Split('+');
            return ts[ts.Length - 1];
        }

        public static bool IsBasedRenderNode(Type t)
        {
            if (t.IsAbstract) return false;
            var baseType = t.BaseType;
            while (baseType != null && baseType.BaseType != typeof(object)) { baseType = baseType.BaseType; }
            return baseType == typeof(BaseRenderNode);
        }
        public static bool IsEngineObject(Type t)
        {
            var baseType = t.BaseType;
            while (baseType != typeof(UnityEngine.Object) && baseType != typeof(object)) { baseType = baseType.BaseType; }
            return baseType == typeof(UnityEngine.Object);
        }

        public static Type GetTypeFromName(string typeName)
        {
            Type type = null;
            Assembly[] assemblyArray = AppDomain.CurrentDomain.GetAssemblies();
            int assemblyArrayLength = assemblyArray.Length;
            for (int i = 0; i < assemblyArrayLength; ++i)
            {
                type = assemblyArray[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            for (int i = 0; (i < assemblyArrayLength); ++i)
            {
                Type[] typeArray = assemblyArray[i].GetTypes();
                int typeArrayLength = typeArray.Length;
                for (int j = 0; j < typeArrayLength; ++j)
                {
                    if (typeArray[j].Name.Equals(typeName))
                    {
                        return typeArray[j];
                    }
                }
            }
            return type;
        }

        public static FieldInfo[] GetAllPublicProperties(Type t)
        {
            return t.GetFields(BindingFlags.Instance | BindingFlags.Public);
        }

        public static Tuple<List<FieldInfo>, List<FieldInfo>, List<FieldInfo>> GetFieldInfo(Type nodeType)
        {

            List<FieldInfo> public_fields = new List<FieldInfo>();

            FieldInfo[] fieldInfos;
            fieldInfos = ReflectionUtil.GetAllPublicProperties(nodeType);
            foreach (var field in fieldInfos)
            {
                if (field.GetCustomAttribute<HideInInspector>() == null)
                {
                    public_fields.Add(field);
                }
            }
            if (fieldInfos.Length == 0)
            {
                Debug.LogWarning(string.Format("Load RenderNode \"{0}\" Warnning! Empty Node will be ignored.", nodeType.Name));
                return null;
            }

            List<FieldInfo> input_fields = new List<FieldInfo>();
            List<FieldInfo> output_fields = new List<FieldInfo>();
            List<FieldInfo> param_fields = new List<FieldInfo>();

            foreach (var field in public_fields)
            {
                var pinInfo = field.GetCustomAttribute<BaseRenderNode.NodePinAttribute>();
                if (pinInfo != null)
                {
                    if (field.FieldType.IsValueType)
                    {
                        //Debug.LogError("Pin can only be a Reference Type. It can lead to undefined behavior.\n" + nodeType.ToString() + " + " + field.Name);
                    }

                    if (pinInfo.type != BaseRenderNode.PinType.Out)
                        input_fields.Add(field);
                    if (pinInfo.type != BaseRenderNode.PinType.In)
                        output_fields.Add(field);
                }
                else
                {
                    param_fields.Add(field);
                }
            }

            return new Tuple<List<FieldInfo>, List<FieldInfo>, List<FieldInfo>>(input_fields, output_fields, param_fields);
        }
    }

#endif
}