using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Runtime.InteropServices;
using System;
using HypnosRenderPipeline;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RTRegister
{
    private HashSet<int> changed;
    private Dictionary<int, RayTracingAccelerationStructure> rayTracingAccelerationStructures;

    private Dictionary<int, Material[]> materialTable;

    private List<HRPLight> rtLights;

    private RTRegister()
    {
        changed = new HashSet<int>();
        materialTable = new Dictionary<int, Material[]>();

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

    public static Material GetOriginalMaterial(MeshRenderer render, int index)
    {
        if (instance.materialTable.ContainsKey(render.GetInstanceID()))
            return instance.materialTable[render.GetInstanceID()][index];
        return null;
    }

    public static void AddMaterialPair(MeshRenderer render, Material[] mats)
    {
        if (!instance.materialTable.ContainsKey(render.GetInstanceID()))
            instance.materialTable.Add(render.GetInstanceID(), mats);
    }

    public static void ClearMaterialCache()
    {
        var objs = GameObject.FindObjectsOfType<MeshRenderer>();
        foreach (var obj in objs)
        {
            var mats = obj.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].name.Contains("(Temp)"))
                {
                    mats[i] = GetOriginalMaterial(obj, i);
                }
            }
            obj.sharedMaterials = mats;
        }
        instance.materialTable.Clear();
    }
}
