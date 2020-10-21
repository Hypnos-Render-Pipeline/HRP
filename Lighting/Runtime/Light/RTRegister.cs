using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using HypnosRenderPipeline;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HypnosRenderPipeline
{
    public class RTRegister
    {
        private HashSet<int> changed;
        private Dictionary<int, RayTracingAccelerationStructure> rayTracingAccelerationStructures;

        RayTracingAccelerationStructure fogAcc;

        private List<HRPLight> rtLights;

        private List<Smoke> smokes;

        private RTRegister()
        {
            changed = new HashSet<int>();

            rayTracingAccelerationStructures = new Dictionary<int, RayTracingAccelerationStructure>();

            rtLights = new List<HRPLight>();

#if UNITY_EDITOR
            Undo.undoRedoPerformed += UndoRedoCallback;
#endif
        }

        ~RTRegister()
        {
            foreach (var rayTraceAcc in rayTracingAccelerationStructures)
            {
                rayTraceAcc.Value.Dispose();
                rayTracingAccelerationStructures.Remove(rayTraceAcc.Key);
            }

#if UNITY_EDITOR
            Undo.undoRedoPerformed -= UndoRedoCallback;
#endif
        }


#if UNITY_EDITOR
        public static void UndoRedoCallback()
        {
            // Todo: should filter event but don't know how
            SceneChanged();
        }
#endif

        private static readonly RTRegister instance = new RTRegister();

        public static void SceneChanged()
        {
            instance.changed.Clear();
        }

        public static bool GetChanged(Camera cam)
        {
            int hash = cam.GetHashCode();
            if (instance.changed.Contains(hash))
            {
                return false;
            }
            else
            {
                instance.changed.Add(hash);
                return true;
            }
        }

        public static void UpdateLightBuffer(CommandBuffer cb, ComputeBuffer cb_LightList)
        {
            LightManager.GetVisibleLights(instance.rtLights);

            cb.SetGlobalInt("_LightCount", LightListGenerator.Generate(instance.rtLights, ref cb_LightList));
            cb.SetGlobalBuffer("_LightList", cb_LightList);
        }

        public static RayTracingAccelerationStructure AccStruct(int layer = -1)
        {
            if (instance.rayTracingAccelerationStructures.ContainsKey(layer))
                return instance.rayTracingAccelerationStructures[layer];

            RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings(
                                                                            RayTracingAccelerationStructure.ManagementMode.Automatic,
                                                                            RayTracingAccelerationStructure.RayTracingModeMask.Everything,
                                                                            layer);
            var rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);
            rayTracingAccelerationStructure.Build();
            instance.rayTracingAccelerationStructures[layer] = rayTracingAccelerationStructure;

            return rayTracingAccelerationStructure;
        }

        public static RayTracingAccelerationStructure FogAccStruct()
        {
            if (instance.fogAcc != null) return instance.fogAcc;

            RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings(
                                                                            RayTracingAccelerationStructure.ManagementMode.Automatic,
                                                                            RayTracingAccelerationStructure.RayTracingModeMask.Everything,
                                                                            1 << 31);
            var rayTracingAccelerationStructure = new RayTracingAccelerationStructure(settings);
            rayTracingAccelerationStructure.Build();
            instance.fogAcc = rayTracingAccelerationStructure;

            return rayTracingAccelerationStructure;
        }
    }
}