using GAS.StateSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Stat
{
    /// <summary>
    /// StatData Inspector 引导到 GAS Editor
    /// </summary>
    [CustomEditor(typeof(StatData))]
    public sealed class StatDataEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 绘制
        /// </summary>
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "推荐使用菜单 Tools/MmGAS 的 Stat 页统一管理属性资产",
                MessageType.Info);

            DrawDefaultInspector();

            if (GUILayout.Button("打开 MmGAS · Stat"))
                GasEditorWindow.Open();
        }
    }
}
