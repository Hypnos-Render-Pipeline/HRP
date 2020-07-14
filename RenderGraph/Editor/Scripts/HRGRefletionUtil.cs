using System;
using System.Reflection;

namespace HypnosRenderPipeline.RenderGraph
{
    internal abstract class ReflectionUtil
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
            while (baseType.BaseType != typeof(object)) { baseType = baseType.BaseType; }
            return baseType == typeof(BaseRenderNode);
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
    }



}