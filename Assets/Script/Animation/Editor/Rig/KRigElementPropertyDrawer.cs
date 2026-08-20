using CGame.Animation.Rig;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Editor.Rig
{
    [CustomPropertyDrawer(typeof(KRigElement))]
    public sealed class KRigElementPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!(property.serializedObject.targetObject is AnimationLayerSettings))
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            return GetRig(property) == null
                ? EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing
                : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (!(property.serializedObject.targetObject is AnimationLayerSettings))
            {
                EditorGUI.PropertyField(position, property, label, true);
                EditorGUI.EndProperty();
                return;
            }

            KRig rig = GetRig(property);
            if (rig == null)
            {
                DrawMissingRig(position, label);
                EditorGUI.EndProperty();
                return;
            }

            Rect buttonRect = EditorGUI.PrefixLabel(position, label);
            SerializedProperty name = property.FindPropertyRelative("Name");
            SerializedProperty index = property.FindPropertyRelative("Index");
            string currentName = string.IsNullOrEmpty(name.stringValue) ? "None" : name.stringValue;
            if (GUI.Button(buttonRect, currentName))
            {
                OpenPicker(property, rig, index.intValue, name.stringValue);
            }

            EditorGUI.EndProperty();
        }

        private static KRig GetRig(SerializedProperty property)
        {
            SerializedProperty rig = property.serializedObject.FindProperty("rig");
            return rig == null ? null : rig.objectReferenceValue as KRig;
        }

        private static void DrawMissingRig(Rect position, GUIContent label)
        {
            Rect buttonRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(true))
            {
                GUI.Button(buttonRect, $"{label.text}: Assign Rig to select a bone");
            }

            Rect helpRect = new Rect(
                position.x,
                buttonRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.HelpBox(helpRect, "Assign a Rig before selecting a bone.", MessageType.Info);
        }

        private static void OpenPicker(SerializedProperty property, KRig rig, int currentIndex, string currentName)
        {
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;
            RigElementSelectionWindow.Open(rig, currentIndex, currentName, selectedElement =>
            {
                Undo.RecordObject(serializedObject.targetObject, "Select Rig Element");
                serializedObject.Update();
                SerializedProperty selectedProperty = serializedObject.FindProperty(propertyPath);
                selectedProperty.FindPropertyRelative("Index").intValue = selectedElement.Index;
                selectedProperty.FindPropertyRelative("Name").stringValue = selectedElement.Name;
                selectedProperty.FindPropertyRelative("Depth").intValue = selectedElement.Depth;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(serializedObject.targetObject);
            });
        }
    }
}
