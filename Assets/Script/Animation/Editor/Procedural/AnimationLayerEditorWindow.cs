using System;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor
{
    public sealed class AnimationLayerEditorWindow : EditorWindow
    {
        [SerializeField] private AnimationLayerSettings layer;
        private SerializedObject serializedLayer;

        public static void Open(AnimationLayerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            AnimationLayerEditorWindow window = GetWindow<AnimationLayerEditorWindow>();
            window.titleContent = new GUIContent("Animation Layer");
            window.layer = settings;
            window.serializedLayer = new SerializedObject(settings);
            window.Show();
        }

        private void OnEnable()
        {
            if (layer != null)
            {
                serializedLayer = new SerializedObject(layer);
            }
        }

        private void OnGUI()
        {
            if (layer == null)
            {
                EditorGUILayout.HelpBox("Select an Animation Layer from a Bone Profile.", MessageType.Info);
                return;
            }

            if (serializedLayer == null || serializedLayer.targetObject != layer)
            {
                serializedLayer = new SerializedObject(layer);
            }

            serializedLayer.Update();
            EditorGUILayout.LabelField(layer.name, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            SerializedProperty property = serializedLayer.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedLayer.ApplyModifiedProperties();
                EditorUtility.SetDirty(layer);
            }

            try
            {
                layer.Validate(layer.Rig);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }
    }
}
