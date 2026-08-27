using System.Collections.Generic;
using GAS.AbilitySystem;
using GAS.Editor;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Ability
{
    /// <summary>
    /// Ability Browser 页签 浏览创建删除 GameplayAbilityData
    /// </summary>
    public sealed class GasAbilityBrowserPage : GasEditorPage
    {
        public override string Title => "Ability";

        /// <summary> 缓存列表 </summary>
        private List<GameplayAbilityData> mAbilityList = new List<GameplayAbilityData>();

        /// <summary> 搜索 </summary>
        private string mSearchText = string.Empty;

        /// <summary> 新建名 </summary>
        private string mNewName = "NewAbility";

        /// <summary> 选中资产 </summary>
        private GameplayAbilityData mSelected;

        /// <summary> 选中 Inspector 编辑器 </summary>
        private UnityEditor.Editor mSelectedEditor;

        /// <summary> 列表滚动 </summary>
        private Vector2 mListScroll;

        /// <summary> 详情滚动 </summary>
        private Vector2 mDetailScroll;

        /// <summary> 状态 </summary>
        private string mStatusMessage = string.Empty;

        /// <summary> 左栏宽度 </summary>
        private float mListWidth = 360f;

        /// <summary> 是否正在拖分割条 </summary>
        private bool mDraggingSplit;

        /// <summary> 左栏宽度偏好键 </summary>
        private const string ListWidthPrefsKey = "MmGAS.Ability.ListWidth";

        /// <summary>
        /// 启用时刷新
        /// </summary>
        public override void OnEnable()
        {
            mListWidth = EditorPrefs.GetFloat(ListWidthPrefsKey, 360f);
            mListWidth = Mathf.Clamp(mListWidth, 220f, 700f);
            RefreshList();
        }

        /// <summary>
        /// 关闭时释放编辑器
        /// </summary>
        public override void OnDisable()
        {
            EditorPrefs.SetFloat(ListWidthPrefsKey, mListWidth);
            ClearSelectedEditor();
        }

        /// <summary>
        /// 绘制页签
        /// </summary>
        public override void OnGUI()
        {
            DrawToolbar();
            DrawCreateRow();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawAbilityList();
            DrawVerticalSplitLine();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(mStatusMessage))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.HelpBox(mStatusMessage, MessageType.Info);
            }
        }

        /// <summary>
        /// 工具条 刷新 + 搜索
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                RefreshList();
                mStatusMessage = $"已刷新 共 {mAbilityList.Count} 个 Ability";
            }

            GUILayout.FlexibleSpace();
            mSearchText = GUILayout.TextField(
                mSearchText ?? string.Empty,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(120f),
                GUILayout.MaxWidth(220f));

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 新建行
        /// </summary>
        private void DrawCreateRow()
        {
            EditorGUILayout.BeginHorizontal("box");
            mNewName = EditorGUILayout.TextField(mNewName);
            if (GUILayout.Button("创建", GUILayout.Width(52f)))
                CreateAndSelect();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 左侧列表
        /// </summary>
        private void DrawAbilityList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(mListWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Ability 列表", EditorStyles.boldLabel);

            mListScroll = EditorGUILayout.BeginScrollView(
                mListScroll,
                GUILayout.ExpandHeight(true));

            for (int i = 0; i < mAbilityList.Count; i++)
            {
                GameplayAbilityData data = mAbilityList[i];
                if (data == null) continue;
                if (!string.IsNullOrEmpty(mSearchText) && !MatchSearch(data, mSearchText))
                    continue;

                DrawAbilityRow(data);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 搜索资产名或 abilityName
        /// </summary>
        private static bool MatchSearch(GameplayAbilityData data, string search)
        {
            if (data.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(data.abilityName) &&
                data.abilityName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        /// <summary>
        /// 左右栏竖向分割线
        /// </summary>
        private void DrawVerticalSplitLine()
        {
            const float hitWidth = 6f;
            Rect rect = GUILayoutUtility.GetRect(hitWidth, 1f, GUILayout.Width(hitWidth), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.Repaint)
            {
                float lineX = rect.x + (rect.width - 1f) * 0.5f;
                EditorGUI.DrawRect(new Rect(lineX, rect.y, 1f, rect.height), new Color(0.45f, 0.45f, 0.45f, 0.85f));
            }

            Event eEvent = Event.current;
            switch (eEvent.type)
            {
                case EventType.MouseDown:
                    if (eEvent.button == 0 && rect.Contains(eEvent.mousePosition))
                    {
                        mDraggingSplit = true;
                        eEvent.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (!mDraggingSplit) break;
                    mListWidth = Mathf.Clamp(mListWidth + eEvent.delta.x, 220f, 700f);
                    eEvent.Use();
                    RepaintHost();
                    break;

                case EventType.MouseUp:
                    if (!mDraggingSplit) break;
                    mDraggingSplit = false;
                    EditorPrefs.SetFloat(ListWidthPrefsKey, mListWidth);
                    eEvent.Use();
                    break;
            }
        }

        /// <summary>
        /// 刷新宿主窗口
        /// </summary>
        private static void RepaintHost()
        {
            EditorWindow[] windowList = Resources.FindObjectsOfTypeAll<GasEditorWindow>();
            for (int i = 0; i < windowList.Length; i++)
            {
                if (windowList[i] != null)
                    windowList[i].Repaint();
            }
        }

        /// <summary>
        /// 单条 Ability 行
        /// </summary>
        private void DrawAbilityRow(GameplayAbilityData data)
        {
            bool selected = mSelected == data;
            Rect rowRect = GasEditorListGUI.BeginRow(40f, selected);

            const float buttonWidth = 40f;
            const float buttonGap = 2f;
            float rightReserved = buttonWidth * 2f + buttonGap * 3f;

            Rect deleteRect = new Rect(rowRect.xMax - buttonWidth - buttonGap, rowRect.y + 4f, buttonWidth, 18f);
            Rect pingRect = new Rect(deleteRect.x - buttonWidth - buttonGap, rowRect.y + 4f, buttonWidth, 18f);
            Rect labelRect = new Rect(rowRect.x + 4f, rowRect.y + 2f, pingRect.x - rowRect.x - 8f, 18f);
            Rect previewRect = new Rect(rowRect.x + 4f, rowRect.y + 20f, rowRect.width - rightReserved - 8f, 16f);

            string displayName = string.IsNullOrEmpty(data.abilityName) ? data.name : data.abilityName;

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.label.Draw(labelRect, displayName, false, false, false, false);
                EditorStyles.miniLabel.Draw(
                    previewRect,
                    GasAbilityEditorUtility.BuildPreviewText(data),
                    false,
                    false,
                    false,
                    false);
            }

            if (GasEditorListGUI.SelectableContent(rowRect, rightReserved))
                SelectAbility(data);

            if (GUI.Button(pingRect, "定位"))
            {
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
            }

            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUI.Button(deleteRect, "删除"))
                TryDeleteAbility(data);
            GUI.backgroundColor = old;
        }

        /// <summary>
        /// 右侧详情
        /// </summary>
        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("选中详情", EditorStyles.boldLabel);

            if (mSelected == null)
            {
                EditorGUILayout.HelpBox("点击左侧条目选中一个 Ability", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(mSelected), EditorStyles.miniLabel);

            if (mSelectedEditor == null || mSelectedEditor.target != mSelected)
            {
                EditorGUILayout.HelpBox("加载详情…", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            mDetailScroll = EditorGUILayout.BeginScrollView(
                mDetailScroll,
                GUILayout.ExpandHeight(true));
            mSelectedEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("在 Project 中选中", GUILayout.Height(22f)))
            {
                Selection.activeObject = mSelected;
                EditorGUIUtility.PingObject(mSelected);
            }

            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("删除资产", GUILayout.Height(22f), GUILayout.Width(80f)))
                TryDeleteAbility(mSelected);
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 选中
        /// </summary>
        private void SelectAbility(GameplayAbilityData data)
        {
            if (mSelected == data) return;
            mSelected = data;
            mDetailScroll = Vector2.zero;
            GasEditorListGUI.DelayRebuild(RebuildSelectedEditor);
        }

        /// <summary>
        /// 按当前选中重建详情编辑器
        /// </summary>
        private void RebuildSelectedEditor()
        {
            ClearSelectedEditor();
            if (mSelected != null)
                mSelectedEditor = UnityEditor.Editor.CreateEditor(mSelected);
        }

        /// <summary>
        /// 释放选中编辑器
        /// </summary>
        private void ClearSelectedEditor()
        {
            if (mSelectedEditor != null)
            {
                Object.DestroyImmediate(mSelectedEditor);
                mSelectedEditor = null;
            }
        }

        /// <summary>
        /// 删除
        /// </summary>
        private void TryDeleteAbility(GameplayAbilityData data)
        {
            if (data == null) return;

            string name = data.name;
            if (!EditorUtility.DisplayDialog("删除 Ability 资产", $"确认删除 {name} ?\n此操作不可撤销", "删除", "取消"))
                return;

            bool deleted = GasAbilityEditorUtility.TryDeleteAbilityData(data);
            if (!deleted)
            {
                mStatusMessage = $"删除失败 {name}";
                return;
            }

            if (mSelected == data)
                SelectAbility(null);

            RefreshList();
            mStatusMessage = $"已删除 {name}";
        }

        /// <summary>
        /// 创建并选中
        /// </summary>
        private void CreateAndSelect()
        {
            string name = string.IsNullOrWhiteSpace(mNewName) ? "NewAbility" : mNewName;
            GameplayAbilityData data = GasAbilityEditorUtility.CreateAbilityData(name);
            RefreshList();
            SelectAbility(data);
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            mStatusMessage = $"已创建 {data.name}";
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshList()
        {
            mAbilityList = GasAbilityEditorUtility.FindAllAbilityDataList();
            if (mSelected != null && !mAbilityList.Contains(mSelected))
                SelectAbility(null);
        }
    }
}
