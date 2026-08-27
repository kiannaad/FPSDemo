using System.Collections.Generic;
using GAS.Editor;
using GAS.TagSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Tag
{
    /// <summary>
    /// Tag Browser 页签 管理全局标签库
    /// </summary>
    public sealed class GasTagBrowserPage : GasEditorPage
    {
        public override string Title => "Tag";

        /// <summary> 当前标签库 </summary>
        private GameplayTagDatabase mDatabase;

        /// <summary> 搜索关键字 </summary>
        private string mSearchText = string.Empty;

        /// <summary> 新建标签输入 </summary>
        private string mNewTagText = string.Empty;

        /// <summary> 重命名输入 </summary>
        private string mRenameText = string.Empty;

        /// <summary> 当前选中标签 </summary>
        private string mSelectedTag;

        /// <summary> 列表滚动 </summary>
        private Vector2 mScroll;

        /// <summary> 状态提示 </summary>
        private string mStatusMessage = string.Empty;

        /// <summary>
        /// 启用时加载库
        /// </summary>
        public override void OnEnable()
        {
            RefreshDatabase();
        }

        /// <summary>
        /// 绘制页签
        /// </summary>
        public override void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);

            if (mDatabase == null)
            {
                EditorGUILayout.HelpBox("未找到 GameplayTagDatabase 可点击上方创建", MessageType.Warning);
                return;
            }

            DrawAddRow();
            EditorGUILayout.Space(4f);
            DrawTagList();
            EditorGUILayout.Space(4f);
            DrawDetailPanel();

            if (!string.IsNullOrEmpty(mStatusMessage))
            {
                EditorGUILayout.HelpBox(mStatusMessage, MessageType.Info);
            }
        }

        /// <summary>
        /// 绘制工具条
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshDatabase();
                mStatusMessage = "已刷新";
            }

            if (GUILayout.Button("选中库资产", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                if (mDatabase != null)
                {
                    Selection.activeObject = mDatabase;
                    EditorGUIUtility.PingObject(mDatabase);
                }
            }

            if (mDatabase == null)
            {
                if (GUILayout.Button("创建 Database", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    mDatabase = GasTagEditorUtility.EnsureDatabase();
                    mStatusMessage = "已创建 GameplayTagDatabase";
                }
            }

            GUILayout.FlexibleSpace();
            mSearchText = GUILayout.TextField(mSearchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制新增行
        /// </summary>
        private void DrawAddRow()
        {
            EditorGUILayout.BeginHorizontal();
            mNewTagText = EditorGUILayout.TextField("新建标签", mNewTagText);
            if (GUILayout.Button("添加", GUILayout.Width(60f)))
            {
                if (GasTagEditorUtility.TryAddTag(mDatabase, mNewTagText, out string error))
                {
                    mSelectedTag = mNewTagText.Trim();
                    mRenameText = mSelectedTag;
                    mNewTagText = string.Empty;
                    mStatusMessage = $"已添加 {mSelectedTag}";
                }
                else
                {
                    mStatusMessage = error;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制标签列表
        /// </summary>
        private void DrawTagList()
        {
            List<string> tagNameList = GasTagEditorUtility.GetTagNameList();
            EditorGUILayout.LabelField($"标签列表 ({tagNameList.Count})", EditorStyles.boldLabel);

            mScroll = EditorGUILayout.BeginScrollView(mScroll, GUILayout.MinHeight(220f));
            for (int i = 0; i < tagNameList.Count; i++)
            {
                string tag = tagNameList[i];
                if (!string.IsNullOrEmpty(mSearchText) &&
                    tag.IndexOf(mSearchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                bool selected = tag == mSelectedTag;
                Rect rowRect = GasEditorListGUI.BeginRow(22f, selected);

                const float copyWidth = 44f;
                const float deleteWidth = 32f;
                const float gap = 2f;
                float rightReserved = copyWidth + deleteWidth + gap * 3f;

                Rect deleteRect = new Rect(rowRect.xMax - deleteWidth - gap, rowRect.y + 1f, deleteWidth, 18f);
                Rect copyRect = new Rect(deleteRect.x - copyWidth - gap, rowRect.y + 1f, copyWidth, 18f);
                Rect labelRect = new Rect(rowRect.x + 4f, rowRect.y + 1f, copyRect.x - rowRect.x - 8f, 18f);

                string label = GasTagEditorUtility.BuildTreeLabel(tag);
                if (Event.current.type == EventType.Repaint)
                    EditorStyles.label.Draw(labelRect, label, false, false, false, false);

                if (GasEditorListGUI.SelectableContent(rowRect, rightReserved))
                {
                    mSelectedTag = tag;
                    mRenameText = tag;
                }

                if (GUI.Button(copyRect, "复制"))
                {
                    EditorGUIUtility.systemCopyBuffer = tag;
                    mStatusMessage = $"已复制 {tag}";
                }

                if (GUI.Button(deleteRect, "删"))
                {
                    if (EditorUtility.DisplayDialog("删除标签", $"确认删除 {tag} ?", "删除", "取消"))
                    {
                        GasTagEditorUtility.TryRemoveTag(mDatabase, tag);
                        if (mSelectedTag == tag)
                        {
                            mSelectedTag = null;
                            mRenameText = string.Empty;
                        }
                        mStatusMessage = $"已删除 {tag}";
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制选中详情
        /// </summary>
        private void DrawDetailPanel()
        {
            EditorGUILayout.LabelField("选中详情", EditorStyles.boldLabel);
            if (string.IsNullOrEmpty(mSelectedTag))
            {
                EditorGUILayout.HelpBox("在列表中点选一个标签", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("完整路径", mSelectedTag);
            EditorGUILayout.LabelField("层级深度", mSelectedTag.Split('.').Length.ToString());

            EditorGUILayout.BeginHorizontal();
            mRenameText = EditorGUILayout.TextField("重命名", mRenameText);
            if (GUILayout.Button("应用", GUILayout.Width(60f)))
            {
                if (GasTagEditorUtility.TryRenameTag(mDatabase, mSelectedTag, mRenameText, out string error))
                {
                    mSelectedTag = mRenameText.Trim();
                    mStatusMessage = $"已重命名为 {mSelectedTag}";
                }
                else
                {
                    mStatusMessage = error;
                }
            }
            EditorGUILayout.EndHorizontal();

            DrawParentChildHint(mSelectedTag);
        }

        /// <summary>
        /// 绘制父子提示
        /// </summary>
        private void DrawParentChildHint(string tag)
        {
            List<string> allList = GasTagEditorUtility.GetTagNameList();
            GameplayTag selected = new GameplayTag(tag);

            List<string> parentList = new List<string>();
            List<string> childList = new List<string>();
            for (int i = 0; i < allList.Count; i++)
            {
                string other = allList[i];
                if (other == tag) continue;
                GameplayTag otherTag = new GameplayTag(other);
                if (otherTag.IsParentOf(selected))
                    parentList.Add(other);
                if (selected.IsParentOf(otherTag))
                    childList.Add(other);
            }

            EditorGUILayout.LabelField("父标签", parentList.Count == 0 ? "(无)" : string.Join("  ", parentList));
            EditorGUILayout.LabelField("子标签", childList.Count == 0 ? "(无)" : string.Join("  ", childList));
        }

        /// <summary>
        /// 刷新数据库引用
        /// </summary>
        private void RefreshDatabase()
        {
            GameplayTagDatabase.ClearCache();
            mDatabase = GasTagEditorUtility.FindDatabase();
        }
    }
}
