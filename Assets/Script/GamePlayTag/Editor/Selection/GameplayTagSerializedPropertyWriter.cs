using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor
{
    public static class GameplayTagSerializedPropertyWriter
    {
        private const string TagNameField = "tagName";
        private const string SerializedTagsField = "serializedTags";

        public static string ReadTagName(SerializedProperty tagProperty)
        {
            SerializedProperty nameProperty = tagProperty?.FindPropertyRelative(TagNameField);
            return nameProperty?.stringValue ?? string.Empty;
        }

        public static bool TryAssignTag(
            SerializedProperty tagProperty,
            string requestedName,
            GameplayTagPickerModel model,
            out string error)
        {
            if (tagProperty == null || model == null)
            {
                error = "Tag property and picker model are required.";
                return false;
            }

            if (!model.TryGetCanonicalName(requestedName, out string canonicalName))
            {
                error = $"GameplayTag '{requestedName}' is not an explicit registered tag.";
                return false;
            }

            SerializedProperty nameProperty = tagProperty.FindPropertyRelative(TagNameField);
            if (nameProperty == null)
            {
                error = $"Serialized field '{TagNameField}' was not found.";
                return false;
            }

            nameProperty.stringValue = canonicalName;
            error = string.Empty;
            return true;
        }

        public static bool AddEmptyContainerRow(SerializedProperty containerProperty, out string error)
        {
            SerializedProperty tags = GetTags(containerProperty, out error);
            if (tags == null)
            {
                return false;
            }

            for (int index = 0; index < tags.arraySize; index++)
            {
                if (string.IsNullOrEmpty(ReadTagName(tags.GetArrayElementAtIndex(index))))
                {
                    error = "Choose the existing empty row before adding another tag.";
                    return false;
                }
            }

            int newIndex = tags.arraySize;
            tags.InsertArrayElementAtIndex(newIndex);
            SerializedProperty inserted = tags.GetArrayElementAtIndex(newIndex);
            SerializedProperty nameProperty = inserted.FindPropertyRelative(TagNameField);
            if (nameProperty != null)
            {
                nameProperty.stringValue = string.Empty;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryAssignContainerElement(
            SerializedProperty containerProperty,
            int elementIndex,
            string requestedName,
            GameplayTagPickerModel model,
            out string error)
        {
            SerializedProperty tags = GetTags(containerProperty, out error);
            if (tags == null || elementIndex < 0 || elementIndex >= tags.arraySize)
            {
                error = tags == null ? error : "Container row index is out of range.";
                return false;
            }

            if (!model.TryGetCanonicalName(requestedName, out string canonicalName))
            {
                error = $"GameplayTag '{requestedName}' is not an explicit registered tag.";
                return false;
            }

            if (!string.IsNullOrEmpty(canonicalName))
            {
                for (int index = 0; index < tags.arraySize; index++)
                {
                    if (index != elementIndex &&
                        ReadTagName(tags.GetArrayElementAtIndex(index)).Equals(canonicalName, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"GameplayTag '{canonicalName}' is already in this container.";
                        return false;
                    }
                }
            }

            return TryAssignTag(tags.GetArrayElementAtIndex(elementIndex), canonicalName, model, out error);
        }

        public static bool RemoveContainerElement(SerializedProperty containerProperty, int elementIndex, out string error)
        {
            SerializedProperty tags = GetTags(containerProperty, out error);
            if (tags == null || elementIndex < 0 || elementIndex >= tags.arraySize)
            {
                error = tags == null ? error : "Container row index is out of range.";
                return false;
            }

            tags.DeleteArrayElementAtIndex(elementIndex);
            error = string.Empty;
            return true;
        }

        public static void RefreshContainerCaches(UnityEngine.Object[] targets, string containerPropertyPath)
        {
            foreach (UnityEngine.Object target in targets ?? Array.Empty<UnityEngine.Object>())
            {
                if (ResolvePath(target, containerPropertyPath) is GameplayTagContainer container)
                {
                    container.OnAfterDeserialize();
                }
            }
        }

        private static SerializedProperty GetTags(SerializedProperty containerProperty, out string error)
        {
            SerializedProperty tags = containerProperty?.FindPropertyRelative(SerializedTagsField);
            if (tags == null || !tags.isArray)
            {
                error = $"Serialized array '{SerializedTagsField}' was not found.";
                return null;
            }

            error = string.Empty;
            return tags;
        }

        private static object ResolvePath(object root, string propertyPath)
        {
            object current = root;
            string normalizedPath = propertyPath.Replace(".Array.data[", "[");
            foreach (string segment in normalizedPath.Split('.'))
            {
                if (current == null)
                {
                    return null;
                }

                int bracketIndex = segment.IndexOf('[');
                string fieldName = bracketIndex < 0 ? segment : segment.Substring(0, bracketIndex);
                FieldInfo field = FindField(current.GetType(), fieldName);
                current = field?.GetValue(current);
                if (bracketIndex >= 0 && current is IList list)
                {
                    string indexText = segment.Substring(bracketIndex + 1).TrimEnd(']');
                    current = int.TryParse(indexText, out int index) && index >= 0 && index < list.Count
                        ? list[index]
                        : null;
                }
            }

            return current;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
