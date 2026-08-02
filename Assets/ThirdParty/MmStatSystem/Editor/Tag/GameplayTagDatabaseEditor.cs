using GAS.TagSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Tag
{
    /// <summary>
    /// 标签库 Inspector 简洁展示 避免 Runtime 挂 Odin 列表特性
    /// </summary>
    [CustomEditor(typeof(GameplayTagDatabase))]
    public sealed class GameplayTagDatabaseEditor : UnityEditor.Editor
    {
        /// <summary> 列表属性 </summary>
        private SerializedProperty mTagListProp;

        /// <summary>
        /// 启用时缓存属性
        /// </summary>
        private void OnEnable()
        {
            mTagListProp = serializedObject.FindProperty(GasTagEditorUtility.TagListPropertyName);
        }

        /// <summary>
        /// 绘制 Inspector
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("推荐使用菜单 Tools/MmGAS 的 Tag 页管理标签", MessageType.Info);
            EditorGUILayout.PropertyField(mTagListProp, new GUIContent("标签列表"), true);
            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("打开 MmGAS"))
            {
                GasEditorWindow.Open();
            }
        }
    }
}
