using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor
{
    /// <summary>
    /// GAS 编辑器主窗口 负责页签框架
    /// </summary>
    public sealed class GasEditorWindow : EditorWindow
    {
        /// <summary> 页签列表 </summary>
        private readonly List<IGasEditorPage> mPageList = new List<IGasEditorPage>();

        /// <summary> 当前页签下标 </summary>
        private int mSelectedIndex;

        /// <summary> 页签标题缓存 </summary>
        private string[] mTabTitleList;

        /// <summary>
        /// 打开 GAS 编辑器
        /// </summary>
        [MenuItem("Tools/MmGAS/Editor", priority = 0)]
        public static void Open()
        {
            GasEditorWindow window = GetWindow<GasEditorWindow>();
            window.titleContent = new GUIContent("MmGAS");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
        }

        /// <summary>
        /// 启用时注册页签
        /// </summary>
        private void OnEnable()
        {
            mPageList.Clear();
            mPageList.Add(new Tag.GasTagBrowserPage());
            mPageList.Add(new Stat.GasStatBrowserPage());
            mPageList.Add(new Effect.GasEffectBrowserPage());
            mPageList.Add(new Ability.GasAbilityBrowserPage());
            mPageList.Add(new GasPlaceholderPage("Debug", "Play 模式 ASC 调试 待接入"));


            mTabTitleList = new string[mPageList.Count];
            for (int i = 0; i < mPageList.Count; i++)
            {
                mTabTitleList[i] = mPageList[i].Title;
                mPageList[i].OnEnable();
            }

            mSelectedIndex = Mathf.Clamp(mSelectedIndex, 0, mPageList.Count - 1);
        }

        /// <summary>
        /// 关闭时释放页签
        /// </summary>
        private void OnDisable()
        {
            for (int i = 0; i < mPageList.Count; i++)
            {
                mPageList[i].OnDisable();
            }
            mPageList.Clear();
        }

        /// <summary>
        /// 绘制窗口
        /// </summary>
        private void OnGUI()
        {
            if (mPageList.Count == 0) return;

            EditorGUILayout.Space(4f);
            mSelectedIndex = GUILayout.Toolbar(mSelectedIndex, mTabTitleList);
            EditorGUILayout.Space(6f);

            //页签内容占满剩余高度 方便子页 ExpandHeight 适配分辨率
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            IGasEditorPage page = mPageList[mSelectedIndex];
            page.OnGUI();
            EditorGUILayout.EndVertical();
        }
    }
}
