using UnityEditor;
using UnityEngine;

namespace GAS.Editor
{
    /// <summary>
    /// 占位页签 后续模块接入前显示
    /// </summary>
    public sealed class GasPlaceholderPage : GasEditorPage
    {
        /// <summary> 页签标题 </summary>
        private readonly string mTitle;

        /// <summary> 说明文字 </summary>
        private readonly string mHint;

        public override string Title => mTitle;

        /// <summary>
        /// 构造占位页
        /// </summary>
        public GasPlaceholderPage(string title, string hint)
        {
            mTitle = title;
            mHint = hint;
        }

        /// <summary>
        /// 绘制占位提示
        /// </summary>
        public override void OnGUI()
        {
            EditorGUILayout.HelpBox(mHint, MessageType.Info);
            GUILayout.Label("此页签待实现 框架已预留注册位", EditorStyles.wordWrappedLabel);
        }
    }
}
