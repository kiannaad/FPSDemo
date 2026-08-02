using UnityEditor;
using UnityEngine;

namespace GAS.Editor
{
    /// <summary>
    /// 编辑器列表行点击辅助 避免 MouseDown 命中区过小导致点不中
    /// </summary>
    public static class GasEditorListGUI
    {
        /// <summary> 选中底色 </summary>
        private static readonly Color SelectedColor = new Color(0.24f, 0.37f, 0.60f, 0.35f);

        /// <summary> 分割线颜色 </summary>
        private static readonly Color LineColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);

        /// <summary>
        /// 申请一行矩形并画底色与底部分割线
        /// </summary>
        public static Rect BeginRow(float height, bool selected)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                if (selected)
                    EditorGUI.DrawRect(rowRect, SelectedColor);

                Rect lineRect = new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f);
                EditorGUI.DrawRect(lineRect, LineColor);
            }
            return rowRect;
        }

        /// <summary>
        /// 左侧可点选区域 用隐形 Button 吃点击 比手写 MouseDown 稳
        /// </summary>
        public static bool SelectableContent(Rect rowRect, float rightReservedWidth)
        {
            Rect clickRect = new Rect(
                rowRect.x,
                rowRect.y,
                Mathf.Max(0f, rowRect.width - rightReservedWidth),
                rowRect.height);

            return GUI.Button(clickRect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>
        /// 延迟重建详情 Editor 避免选中当帧 DestroyImmediate 吞事件
        /// </summary>
        public static void DelayRebuild(System.Action rebuildAction)
        {
            if (rebuildAction == null) return;
            EditorApplication.delayCall += () =>
            {
                rebuildAction();
                EditorWindow[] windowList = Resources.FindObjectsOfTypeAll<GasEditorWindow>();
                for (int i = 0; i < windowList.Length; i++)
                {
                    if (windowList[i] != null)
                        windowList[i].Repaint();
                }
            };
        }
    }
}
