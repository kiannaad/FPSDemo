using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Tag
{
    /// <summary>
    /// Editor 侧绘制 string[] 标签列表 不污染 Runtime
    /// 不缓存 SerializedProperty 避免 Disposed 报错
    /// </summary>
    public static class GasTagEditorGUI
    {
        /// <summary>
        /// 绘制标签字符串数组
        /// </summary>
        public static void DrawStringArrayLayout(SerializedProperty arrayProp, string label)
        {
            if (arrayProp == null || !arrayProp.isArray) return;

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (arrayProp.arraySize == 0)
                EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);

            int removeIndex = -1;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
                Rect rect = EditorGUILayout.GetControlRect();
                GasTagDrawHelper.DrawStringPopup(rect, element, GUIContent.none);
                if (GUILayout.Button("-", GUILayout.Width(22f)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
                arrayProp.DeleteArrayElementAtIndex(removeIndex);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加标签", GUILayout.Width(90f)))
            {
                int index = arrayProp.arraySize;
                arrayProp.arraySize++;
                arrayProp.GetArrayElementAtIndex(index).stringValue = string.Empty;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
