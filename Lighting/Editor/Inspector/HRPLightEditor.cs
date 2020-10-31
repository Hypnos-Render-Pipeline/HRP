using System;
using UnityEditor;
using UnityEngine;
using Expression = System.Linq.Expressions.Expression;

namespace HypnosRenderPipeline
{
    [CustomEditor(typeof(Light))]
    [CanEditMultipleObjects]
    public class HRPLightEditor : LightEditor
    {
        public Light m_light;
        public HRPLight m_lightData;

        Editor m_cacheEditor;

        SerializedObject m_lighdDataObject;

        Action<GUIContent, SerializedProperty> m_sliderMethod;
        Func<Vector3, Vector3, float, float> m_sizeSlider;
        Texture2D m_ColorTempTex;

        bool showLegacyLightInspector = false;
        bool showAtmoInspector = false;

        class Properties
        {
            public static SerializedProperty lightType;
            public static SerializedProperty temperature;
            public static SerializedProperty radiance;
            public static SerializedProperty IESProfile;
            public static SerializedProperty shadow;
            public static SerializedProperty areaSize;
            public static SerializedProperty areaTexture;
            public static SerializedProperty lightMesh;
            public static SerializedProperty drawLightMesh;
            public static SerializedProperty atmoPreset;


            public static void Gets(SerializedObject obj)
            {
                lightType = obj.FindProperty("lightType");
                temperature = obj.FindProperty("m_temperature");
                radiance = obj.FindProperty("radiance");
                IESProfile = obj.FindProperty("IESProfile");
                shadow = obj.FindProperty("shadow");
                areaSize = obj.FindProperty("m_areaSize");
                areaTexture = obj.FindProperty("areaTexture");
                lightMesh = obj.FindProperty("lightMesh");
                drawLightMesh = obj.FindProperty("drawLightMesh");
                atmoPreset = obj.FindProperty("atmoPreset");                
            }
        }

        void UndoRedo()
        {
            // Changes caused by undo redo, trigger report to LightManager.
            m_lightData.__TryReportSunlight__(); 
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoRedo;
        }

        protected override void OnEnable()
        {
            Undo.undoRedoPerformed += UndoRedo;
            base.OnEnable();
            m_light = target as Light;
            m_lightData = m_light.gameObject.GetComponent<HRPLight>();
            if (m_lightData == null)
            {
                m_lightData = m_light.GenerateHRPLight();
            }
            m_lighdDataObject = new SerializedObject(m_lightData);
            m_ColorTempTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PathDefine.path + "/Lighting/Editor/Textures/ColorTemperature.png");
            var sliderMethod = typeof(EditorGUILayout).
                       GetMethod(
                            "SliderWithTexture",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                            null,
                            System.Reflection.CallingConventions.Any,
                            new[] { typeof(GUIContent), typeof(SerializedProperty), typeof(float), typeof(float), typeof(float), typeof(Texture2D), typeof(GUILayoutOption[]) },
                            null);
            var paramLabel = Expression.Parameter(typeof(GUIContent), "label");
            var paramProperty = Expression.Parameter(typeof(SerializedProperty), "property");
            var call = Expression.Call(sliderMethod, paramLabel, paramProperty,
                                    Expression.Constant(0.0f),
                                    Expression.Constant(20000.0f),
                                    Expression.Constant(1.0f),
                                    Expression.Constant(m_ColorTempTex),
                                    Expression.Constant(null, typeof(GUILayoutOption[])));
            m_sliderMethod = Expression.Lambda<Action<GUIContent, SerializedProperty>>(call, paramLabel, paramProperty).Compile();

            var sizeSliderMethod = typeof(Handles).
                       GetMethod(
                               "SizeSlider",
                               System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                               null,
                               System.Reflection.CallingConventions.Any,
                               new[] { typeof(Vector3), typeof(Vector3), typeof(float) },
                               null);
            var position = Expression.Parameter(typeof(Vector3), "position");
            var direction = Expression.Parameter(typeof(Vector3), "direction");
            var size = Expression.Parameter(typeof(float), "size");
            call = Expression.Call(sizeSliderMethod, position, direction, size);
            m_sizeSlider = Expression.Lambda<Func<Vector3, Vector3, float, float>>(call, position, direction, size).Compile();
       
            Properties.Gets(m_lighdDataObject);
        }



        public override void OnInspectorGUI()
        {
            m_lighdDataObject.UpdateIfRequiredOrScript();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(Properties.lightType);

            bool sun = m_lightData.sunLight;
            if (m_lightData.lightType == HRPLightType.Directional)
            {
                sun = EditorGUILayout.Toggle("Sun Light", m_lightData.sunLight);
            }

            if (m_lightData.sunLight)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(Properties.atmoPreset);
                if (GUILayout.Button("new", new GUILayoutOption[] { GUILayout.Width(50) }))
                {
                    var atmo = HRPAtmo.Create();
                    if (atmo)
                    {
                        Properties.atmoPreset.objectReferenceValue = atmo;
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (m_lightData.atmoPreset != null)
                {
                    if (m_cacheEditor == null)
                        m_cacheEditor = Editor.CreateEditor(m_lightData.atmoPreset);

                    Rect rect = EditorGUILayout.BeginVertical();
                    EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.3f));
                    showAtmoInspector = EditorGUILayout.Foldout(showAtmoInspector, new GUIContent("Show Atmo Inspector"));
                    if (showAtmoInspector)
                        m_cacheEditor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                }
            }

            m_sliderMethod(new GUIContent("Temperature"), Properties.temperature);

            EditorGUILayout.PropertyField(Properties.radiance);

            if (m_lightData.supportIES)
            {
                EditorGUILayout.PropertyField(Properties.IESProfile);
            }


            EditorGUILayout.PropertyField(Properties.shadow);

            var lr = EditorGUILayout.Slider("Range", m_light.range, 0, 50);

            if (m_lightData.isArea)
            {
                if (m_lightData.shadow != HRPLightShadowType.Off && m_lightData.shadow != HRPLightShadowType.RayTrace)
                {
                    EditorGUILayout.HelpBox("Area light types only work with Shadow off or Ray Traced Shadow", MessageType.Error);
                }
                EditorGUILayout.Space(15);
                EditorGUILayout.PropertyField(Properties.drawLightMesh);
                EditorGUILayout.PropertyField(Properties.areaSize);
            }

            if (m_lightData.canHasTexture)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(Properties.areaTexture);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("Filter Done", GUILayout.Width(63));
                EditorGUILayout.Toggle(m_lightData.areaTextureAlreadyFiltered, GUILayout.Width(13));
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            if (m_lightData.lightType == HRPLightType.Mesh)
            {
                EditorGUILayout.PropertyField(Properties.lightMesh);                
            }


            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_lightData, "Change Light Parameter(s)");
                Undo.RecordObject(m_light, "Change Light Parameter(s)");
                m_lighdDataObject.ApplyModifiedProperties();
                m_light.Copy(m_lightData);
                m_light.range = lr;
                m_lightData.sunLight = sun;
                RTRegister.UndoRedoCallback();
            }

            {
                EditorGUILayout.Space(15);
                var rect = EditorGUILayout.BeginVertical();
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.3f));
                showLegacyLightInspector = EditorGUILayout.Foldout(showLegacyLightInspector,
                                            new GUIContent("Show Legacy Light Inspector", "Not all parameters are supported."));
                if (showLegacyLightInspector)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Space(8);
                    base.OnInspectorGUI();
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndVertical();
            }
        }


        private UnityEditor.IMGUI.Controls.SphereBoundsHandle m_BoundsHandle = new UnityEditor.IMGUI.Controls.SphereBoundsHandle();
        protected override void OnSceneGUI()
        {
            var temp = Handles.color;
            Handles.color = kGizmoLight;
            Vector3 scale = m_light.transform.lossyScale;

            if (m_lightData.lightType == HRPLightType.Mesh)
            {
                // will handle this case in render pipeline
            }
            else if (m_lightData.lightType == HRPLightType.Sphere)
            {
                float r = m_lightData.sphereRadius;
                float r1 = Handles.RadiusHandle(Quaternion.identity, m_light.transform.position, m_lightData.sphereRadius);
                r1 = Mathf.Max(0, r1);
                if (r != r1)
                {
                    Undo.RecordObject(m_lightData, "Change Light Parameter(s)");
                    m_lightData.sphereRadius = r1;
                    RTRegister.UndoRedoCallback();
                }
            }
            else if (m_lightData.lightType == HRPLightType.Tube)
            {
                bool moved = false;

                Vector3 p = m_light.transform.position;
                Vector2 r = m_lightData.tubeLengthRadius;
                Vector3 l = r.x * m_light.transform.forward;
                var a = p + m_light.transform.up * r.y;
                Handles.DrawAAPolyLine(a - l,a + l);
                a =p - m_light.transform.up * r.y;
                Handles.DrawAAPolyLine(a - l, a + l);
                a = p + m_light.transform.right * r.y;
                Handles.DrawAAPolyLine(a - l, a + l);
                a = p - m_light.transform.right * r.y;
                Handles.DrawAAPolyLine(a - l, a + l);

                m_BoundsHandle.radius = r.y;
                m_BoundsHandle.axes = UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.X | UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.Y;
                m_BoundsHandle.center = Vector3.zero;
                m_BoundsHandle.wireframeColor = Handles.color;
                m_BoundsHandle.handleColor = GetLightHandleColor(Handles.color);
                Matrix4x4 mat = new Matrix4x4();
                mat.SetTRS(p - l, m_light.transform.rotation, new Vector3(1, 1, 1));
                EditorGUI.BeginChangeCheck();
                using (new Handles.DrawingScope(Color.white, mat))
                    m_BoundsHandle.DrawHandle();
                mat.SetTRS(p + l, m_light.transform.rotation, new Vector3(1, 1, 1));
                using (new Handles.DrawingScope(Color.white, mat))
                    m_BoundsHandle.DrawHandle();
                m_BoundsHandle.radius = Mathf.Max(0, m_BoundsHandle.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    moved = true;
                    r.y = m_BoundsHandle.radius;
                }

                float len = r.x;
                len = m_sizeSlider(p, m_light.transform.forward, len);
                len = m_sizeSlider(p, -m_light.transform.forward, len);
                len = Mathf.Max(len, 0);

                if (len != r.x) { 
                    r.x = len;
                    moved = true;
                }
                if (moved)
                {
                    Undo.RecordObject(m_lightData, "Change Light Parameter(s)");
                    m_lightData.tubeLengthRadius = r;
                    RTRegister.UndoRedoCallback();
                }
            }
            else if (m_lightData.lightType == HRPLightType.Disc)
            {
                var r = m_lightData.discRadius;
                m_BoundsHandle.radius =r;
                m_BoundsHandle.axes = UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.X | UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.Y;
                m_BoundsHandle.center = Vector3.zero;
                m_BoundsHandle.wireframeColor = Handles.color;
                m_BoundsHandle.handleColor = GetLightHandleColor(Handles.color);
                Matrix4x4 mat = new Matrix4x4();
                mat.SetTRS(m_light.transform.position, m_light.transform.rotation, new Vector3(1, 1, 1));
                EditorGUI.BeginChangeCheck();
                using (new Handles.DrawingScope(Color.white, mat))
                    m_BoundsHandle.DrawHandle();
                m_BoundsHandle.radius = Mathf.Max(0, m_BoundsHandle.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_lightData, "Change Light Parameter(s)");
                    m_lightData.discRadius = m_BoundsHandle.radius;
                    RTRegister.UndoRedoCallback();
                }
                Handles.ArrowHandleCap(0, m_light.transform.position, m_light.transform.rotation, 0.5f, EventType.Repaint);
            }
            else if (m_lightData.lightType == HRPLightType.Quad)
            {
                EditorGUI.BeginChangeCheck();
                Vector2 size = DoRectHandles(m_light.transform.rotation, m_light.transform.position, m_lightData.quadSize, false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_lightData, "Change Light Parameter(s)");
                    m_lightData.quadSize = size;
                    RTRegister.UndoRedoCallback();
                }
                Handles.ArrowHandleCap(0, m_light.transform.position, m_light.transform.rotation, 0.5f, EventType.Repaint);
            }

            base.OnSceneGUI();
        }
        private Color GetLightHandleColor(Color wireframeColor)
        {
            Color color = wireframeColor;
            color.a = Mathf.Clamp01(color.a * 2);
            return ToActiveColorSpace(color);
        }

        private Color ToActiveColorSpace(Color color)
        {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
        }

        Vector2 DoRectHandles(Quaternion rotation, Vector3 position, Vector2 size, bool handlesOnly)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;

            float halfWidth = 0.5f * size.x;
            float halfHeight = 0.5f * size.y;

            if (!handlesOnly)
            {
                Vector3 topRight = position + up * halfHeight + right * halfWidth;
                Vector3 bottomRight = position - up * halfHeight + right * halfWidth;
                Vector3 bottomLeft = position - up * halfHeight - right * halfWidth;
                Vector3 topLeft = position + up * halfHeight - right * halfWidth;

                // Draw rectangle
                Handles.DrawLine(topRight, bottomRight);
                Handles.DrawLine(bottomRight, bottomLeft);
                Handles.DrawLine(bottomLeft, topLeft);
                Handles.DrawLine(topLeft, topRight);
            }

            // Give handles twice the alpha of the lines
            Color origCol = Handles.color;
            Color col = Handles.color;
            col.a = Mathf.Clamp01(Handles.color.a * 2);
            Handles.color = ToActiveColorSpace(col);

            // Draw handles
            halfHeight = m_sizeSlider(position, up, halfHeight);
            halfHeight = m_sizeSlider(position, -up, halfHeight);
            halfWidth = m_sizeSlider(position, right, halfWidth);
            halfWidth = m_sizeSlider(position, -right, halfWidth);

            size.x = Mathf.Max(0f, 2.0f * halfWidth);
            size.y = Mathf.Max(0f, 2.0f * halfHeight);

            Handles.color = origCol;

            return size;
        }
    }

    [CustomEditor(typeof(HRPLight))]
    public class HRPLightEditor2 : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Additional Light Data For HRP", MessageType.Info);

            //var rect = EditorGUILayout.BeginVertical();
            //EditorGUILayout.LabelField("This Area is for develop debug and will be hidden in the release version");
            //EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.2f));
            //EditorGUI.BeginDisabledGroup(true);
            //base.OnInspectorGUI();
            //EditorGUI.EndDisabledGroup();

            //EditorGUILayout.EndVertical();
        }
    }
}