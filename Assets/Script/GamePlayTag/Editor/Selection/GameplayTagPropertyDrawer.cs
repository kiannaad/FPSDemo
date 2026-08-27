using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public sealed class GameplayTagPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            string currentName = GameplayTagSerializedPropertyWriter.ReadTagName(property);
            UnityEngine.Object[] targets = property.serializedObject.targetObjects;
            string propertyPath = property.propertyPath;
            GameplayTagDrawerGui.DrawTagButton(
                position,
                label,
                currentName,
                selectedName => Assign(targets, propertyPath, selectedName));
            EditorGUI.EndProperty();
        }

        private static void Assign(UnityEngine.Object[] targets, string propertyPath, string selectedName)
        {
            var serializedObject = new SerializedObject(targets);
            serializedObject.Update();
            SerializedProperty tagProperty = serializedObject.FindProperty(propertyPath);
            if (!GameplayTagPickerModel.TryCreateFromAuthoritativeConfig(out GameplayTagPickerModel model, out string error) ||
                !GameplayTagSerializedPropertyWriter.TryAssignTag(tagProperty, selectedName, model, out error))
            {
                Debug.LogError(error);
                return;
            }

            serializedObject.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in targets)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
