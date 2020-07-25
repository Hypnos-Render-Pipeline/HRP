using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


class UIDev
{
    public static void LogChilds(VisualElement visualElement)
    {
        var a = visualElement.Children().GetEnumerator();

        while (a.MoveNext())
        {
            Debug.Log(a.Current);
            LogChilds(a.Current);
        }
    }
}