using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public sealed class GameplayTagContainerDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 24f;
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            SerializedProperty tags = property.FindPropertyRelative("serializedTags");
            int rowCount = tags?.arraySize ?? 0;
            return EditorGUIUtility.singleLineHeight * (rowCount + 2) + Spacing * (rowCount + 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            SerializedProperty tags = property.FindPropertyRelative("serializedTags");
            UnityEngine.Object[] targets = property.serializedObject.targetObjects;
            string containerPath = property.propertyPath;
            int removeIndex = -1;

            EditorGUI.indentLevel++;
            for (int index = 0; index < tags.arraySize; index++)
            {
                row.y += EditorGUIUtility.singleLineHeight + Spacing;
                Rect tagRect = new Rect(row.x, row.y, row.width - ButtonWidth - Spacing, row.height);
                Rect removeRect = new Rect(tagRect.xMax + Spacing, row.y, ButtonWidth, row.height);
                SerializedProperty tagProperty = tags.GetArrayElementAtIndex(index);
                string currentName = GameplayTagSerializedPropertyWriter.ReadTagName(tagProperty);
                int capturedIndex = index;
                GameplayTagDrawerGui.DrawTagButton(
                    tagRect,
                    GUIContent.none,
                    currentName,
                    selectedName => AssignElement(targets, containerPath, capturedIndex, selectedName));

                if (GUI.Button(removeRect, "−"))
                {
                    removeIndex = index;
                }
            }

            row.y += EditorGUIUtility.singleLineHeight + Spacing;
            if (GUI.Button(row, "+ Add Tag"))
            {
                MutateContainer(targets, containerPath, propertyValue =>
                    GameplayTagSerializedPropertyWriter.AddEmptyContainerRow(propertyValue, out string error)
                        ? string.Empty
                        : error);
            }

            if (removeIndex >= 0)
            {
                int capturedRemoveIndex = removeIndex;
                MutateContainer(targets, containerPath, propertyValue =>
                    GameplayTagSerializedPropertyWriter.RemoveContainerElement(propertyValue, capturedRemoveIndex, out string error)
                        ? string.Empty
                        : error);
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static void AssignElement(UnityEngine.Object[] targets, string containerPath, int index, string selectedName)
        {
            if (!GameplayTagPickerModel.TryCreateFromAuthoritativeConfig(out GameplayTagPickerModel model, out string error))
            {
                Debug.LogError(error);
                return;
            }

            MutateContainer(targets, containerPath, propertyValue =>
                GameplayTagSerializedPropertyWriter.TryAssignContainerElement(propertyValue, index, selectedName, model, out string assignError)
                    ? string.Empty
                    : assignError);
        }

        private static void MutateContainer(
            UnityEngine.Object[] targets,
            string containerPath,
            System.Func<SerializedProperty, string> mutation)
        {
            var serializedObject = new SerializedObject(targets);
            serializedObject.Update();
            SerializedProperty containerProperty = serializedObject.FindProperty(containerPath);
            string error = mutation(containerProperty);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning(error);
                return;
            }

            serializedObject.ApplyModifiedProperties();
            GameplayTagSerializedPropertyWriter.RefreshContainerCaches(targets, containerPath);
            foreach (UnityEngine.Object target in targets)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
