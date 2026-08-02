using System;
using System.Collections;
using System.Reflection;

namespace CGame.GameplayTags.Editor
{
    internal static class GameplayTagSerializedPathUtility
    {
        public static object Resolve(object root, string propertyPath)
        {
            object current = root;
            string normalizedPath = (propertyPath ?? string.Empty).Replace(".Array.data[", "[");
            foreach (string segment in normalizedPath.Split('.'))
            {
                if (current == null || string.IsNullOrEmpty(segment))
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
