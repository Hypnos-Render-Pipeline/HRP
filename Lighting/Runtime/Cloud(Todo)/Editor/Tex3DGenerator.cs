using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering;


public class TexArrayGenerator : EditorWindow
{
    [MenuItem("Tools/Tex3DGenerator")]
    public static void Create()
    {
        var window = GetWindow<TexArrayGenerator>(true);
        window.maxSize = window.minSize = new Vector2(284, 144);
    }

    Texture2D AtlasTex;

    int volumeSize;

    int atlasWdith;
    int atlasHeight;
    
    GraphicsFormat format;


    void OnGUI()
    {

        EditorGUILayout.BeginVertical();
        var style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperLeft;
        style.fixedWidth = 150;
        GUILayout.Label("Atlas texture", style);
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        AtlasTex = (Texture2D)EditorGUILayout.ObjectField(AtlasTex, typeof(Texture2D), false, GUILayout.Width(70), GUILayout.Height(70));
        if (EditorGUI.EndChangeCheck())
        {
            if (AtlasTex != null)
            {
                volumeSize = (int)Mathf.Pow(AtlasTex.width * AtlasTex.height, 1.0f/3.0f);
                atlasWdith = AtlasTex.width / volumeSize;
                atlasHeight = AtlasTex.height / volumeSize;
            }
        }

        EditorGUILayout.BeginVertical();
                
        EditorGUI.BeginDisabledGroup(AtlasTex == false);

        if (GUILayout.Button("Reset"))
        {
            EditorGUI.FocusTextInControl("");
            volumeSize = (int)Mathf.Pow(AtlasTex.width * AtlasTex.height, 1.0f / 3.0f);
            atlasWdith = AtlasTex.width / volumeSize;
            atlasHeight = AtlasTex.height / volumeSize;
        }

        EditorGUI.BeginChangeCheck();
        volumeSize = EditorGUILayout.IntField("Volume size", volumeSize);
        if (EditorGUI.EndChangeCheck())
        {
            atlasWdith = AtlasTex.width / volumeSize;
            atlasHeight = AtlasTex.height / volumeSize;
        }

        atlasWdith = EditorGUILayout.IntField("Atlas wdith", atlasWdith);
        atlasHeight = EditorGUILayout.IntField("Atlas height", atlasHeight);

        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
        
        format = (GraphicsFormat)EditorGUILayout.EnumPopup(label: new GUIContent("Format"), selected: format, checkEnabled: 
            (System.Enum e) => {
                return SystemInfo.IsFormatSupported((GraphicsFormat)e, FormatUsage.Sample);
            }, false);


        EditorGUI.BeginDisabledGroup(format == GraphicsFormat.None);

        if (GUILayout.Button("Generate"))
        {
            GenerateVolumeTex(AtlasTex, format, volumeSize, atlasWdith, atlasHeight);
        }

        EditorGUI.EndDisabledGroup();

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }


    public static void GenerateVolumeTex(Texture2D atlas, GraphicsFormat format, int size, int w, int h)
    {
        Texture2DArray texture3D = new Texture2DArray(size, size, 0, format, TextureCreationFlags.None);

        var path = AssetDatabase.GetAssetPath(atlas);
        path = path.Substring(0, path.LastIndexOf('.')) + ".asset";

        AssetDatabase.CreateAsset(texture3D, path);
    }
}
