using GAS.TagSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Tag
{
    /// <summary>
    /// Unity 侧 GasTag 仅绘制单个 string
    /// </summary>
    [CustomPropertyDrawer(typeof(GasTagAttribute))]
    public sealed class GasTagPropertyDrawer : PropertyDrawer
    {
        /// <summary>
        /// 绘制
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                GasTagDrawHelper.DrawStringPopup(position, property, label);
                return;
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        /// <summary>
        /// 高度
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
                return EditorGUIUtility.singleLineHeight;
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    /// <summary>
    /// GameplayTag 结构体绘制
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public sealed class GameplayTagPropertyDrawer : PropertyDrawer
    {
        /// <summary>
        /// 绘制 GameplayTag
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty tagNameProp = property.FindPropertyRelative("tagName");
            if (tagNameProp == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            GasTagDrawHelper.DrawStringPopup(position, tagNameProp, label);
        }

        /// <summary>
        /// 单行高度
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
