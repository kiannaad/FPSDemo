using System.Collections.Generic;
using GAS.StateSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Stat
{
    /// <summary>
    /// Unity 侧 GasStat 字段下拉
    /// </summary>
    [CustomPropertyDrawer(typeof(GasStatAttribute))]
    public sealed class GasStatPropertyDrawer : PropertyDrawer
    {
        /// <summary>
        /// 绘制
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            GasStatAttribute attr = (GasStatAttribute)attribute;
            GasStatDrawHelper.DrawStringPopup(position, property, label, attr.PassiveOnly, attr.ImmediateOnly);
        }

        /// <summary>
        /// 高度
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    /// <summary>
    /// StatId 下拉辅助
    /// </summary>
    public static class GasStatDrawHelper
    {
        /// <summary> 空选项 </summary>
        private const string EmptyOption = "<None>";

        /// <summary>
        /// 绘制下拉
        /// </summary>
        public static void DrawStringPopup(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            bool passiveOnly,
            bool immediateOnly)
        {
            List<string> idList = GasStatEditorUtility.GetStatIdList(passiveOnly, immediateOnly);
            List<string> optionList = new List<string>(idList.Count + 2) { EmptyOption };
            optionList.AddRange(idList);

            string current = property.stringValue ?? string.Empty;
            int index = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int found = optionList.IndexOf(current);
                if (found >= 0) index = found;
                else
                {
                    optionList.Add(current + " (未找到资产)");
                    index = optionList.Count - 1;
                }
            }

            EditorGUI.BeginProperty(position, label, property);
            int newIndex = EditorGUI.Popup(position, label.text, index, optionList.ToArray());
            if (newIndex != index)
            {
                if (newIndex <= 0) property.stringValue = string.Empty;
                else if (newIndex <= idList.Count) property.stringValue = optionList[newIndex];
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Layout 版下拉
        /// </summary>
        public static string DrawStringPopupLayout(
            string label,
            string current,
            bool passiveOnly = false,
            bool immediateOnly = false)
        {
            List<string> idList = GasStatEditorUtility.GetStatIdList(passiveOnly, immediateOnly);
            List<string> optionList = new List<string>(idList.Count + 2) { EmptyOption };
            optionList.AddRange(idList);

            int index = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int found = optionList.IndexOf(current);
                if (found >= 0) index = found;
                else
                {
                    optionList.Add(current + " (未找到资产)");
                    index = optionList.Count - 1;
                }
            }

            int newIndex = string.IsNullOrEmpty(label)
                ? EditorGUILayout.Popup(index, optionList.ToArray())
                : EditorGUILayout.Popup(label, index, optionList.ToArray());

            if (newIndex <= 0) return string.Empty;
            if (newIndex <= idList.Count) return optionList[newIndex];
            return current;
        }
    }
}
