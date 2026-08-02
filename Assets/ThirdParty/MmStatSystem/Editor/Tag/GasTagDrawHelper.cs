using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Tag
{
    /// <summary>
    /// Tag 下拉绘制辅助
    /// </summary>
    public static class GasTagDrawHelper
    {
        /// <summary> 空选项 </summary>
        private const string EmptyOption = "<None>";

        /// <summary>
        /// 绘制字符串标签下拉
        /// </summary>
        public static void DrawStringPopup(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> tagList = GasTagEditorUtility.GetTagNameList();
            List<string> optionList = new List<string>(tagList.Count + 2) { EmptyOption };
            optionList.AddRange(tagList);

            string current = property.stringValue ?? string.Empty;
            int index = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int found = optionList.IndexOf(current);
                if (found >= 0)
                {
                    index = found;
                }
                else
                {
                    optionList.Add(current + " (未注册)");
                    index = optionList.Count - 1;
                }
            }

            EditorGUI.BeginProperty(position, label, property);
            int newIndex = string.IsNullOrEmpty(label.text)
                ? EditorGUI.Popup(position, index, optionList.ToArray())
                : EditorGUI.Popup(position, label.text, index, optionList.ToArray());
            if (newIndex != index)
            {
                if (newIndex <= 0)
                    property.stringValue = string.Empty;
                else if (newIndex <= tagList.Count)
                    property.stringValue = optionList[newIndex];
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// IMGUI Layout 版下拉
        /// </summary>
        public static string DrawStringPopupLayout(string label, string current)
        {
            List<string> tagList = GasTagEditorUtility.GetTagNameList();
            List<string> optionList = new List<string>(tagList.Count + 2) { EmptyOption };
            optionList.AddRange(tagList);

            int index = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int found = optionList.IndexOf(current);
                if (found >= 0)
                {
                    index = found;
                }
                else
                {
                    optionList.Add(current + " (未注册)");
                    index = optionList.Count - 1;
                }
            }

            int newIndex = string.IsNullOrEmpty(label)
                ? EditorGUILayout.Popup(index, optionList.ToArray())
                : EditorGUILayout.Popup(label, index, optionList.ToArray());

            if (newIndex <= 0) return string.Empty;
            if (newIndex <= tagList.Count) return optionList[newIndex];
            return current;
        }
    }
}
