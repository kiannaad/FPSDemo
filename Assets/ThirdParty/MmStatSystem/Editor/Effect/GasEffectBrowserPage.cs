using System.Collections.Generic;
using GAS.Core.GameplayEffect;
using GAS.Editor;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Effect
{
    /// <summary>
    /// GE Browser 页签 浏览创建删除与预览 GameplayEffectData
    /// </summary>
    public sealed class GasEffectBrowserPage : GasEditorPage
    {
        public override string Title => "GE";

        /// <summary> 列表筛选 </summary>
        private enum E_ListFilter
        {
            All = 0,
            Instant = 1,
            HasDuration = 2,
            Infinite = 3,
            Independent = 4,
            Shared = 5,
            Periodic = 6,
        }

        /// <summary> 缓存列表 </summary>
        private List<GameplayEffectData> mEffectList = new List<GameplayEffectData>();

        /// <summary> 搜索 </summary>
        private string mSearchText = string.Empty;

        /// <summary> 列表筛选 </summary>
        private E_ListFilter mListFilter = E_ListFilter.All;

        /// <summary> 新建名 </summary>
        private string mNewName = "GE_New";

        /// <summary> 新建时间策略 </summary>
        private E_EffectDuration mNewDuration = E_EffectDuration.HasDuration;

        /// <summary> 新建是否周期 </summary>
        private bool mNewPeriodic;

        /// <summary> 选中资产 </summary>
        private GameplayEffectData mSelected;

        /// <summary> 选中 Inspector 编辑器 </summary>
        private UnityEditor.Editor mSelectedEditor;

        /// <summary> 列表滚动 </summary>
        private Vector2 mListScroll;

        /// <summary> 详情滚动 </summary>
        private Vector2 mDetailScroll;

        /// <summary> 状态 </summary>
        private string mStatusMessage = string.Empty;

        /// <summary> 是否展开说明 </summary>
        private bool mShowHelp = true;

        /// <summary> 左栏宽度 </summary>
        private float mListWidth = 360f;

        /// <summary> 是否正在拖分割条 </summary>
        private bool mDraggingSplit;

        /// <summary> 左栏宽度偏好键 </summary>
        private const string ListWidthPrefsKey = "MmGAS.GE.ListWidth";

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
            EditorGUILayout.Space(2f);
            DrawHelp();
            DrawCreateRow();
            EditorGUILayout.Space(4f);

            //中间区域吃掉剩余高度 避免把底部说明顶出窗口
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawEffectList();
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
        /// 工具条
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                RefreshList();
                mStatusMessage = $"已刷新 共 {mEffectList.Count} 个 GE";
            }

            mListFilter = (E_ListFilter)GUILayout.Toolbar(
                (int)mListFilter,
                new[] { "全部", "即时", "持续", "永久", "独立层数", "共享层数", "存在周期" },
                EditorStyles.toolbarButton,
                GUILayout.Width(500f));

            mShowHelp = GUILayout.Toggle(
                mShowHelp,
                new GUIContent("说明", "显示/隐藏 GE 配置速查"),
                EditorStyles.toolbarButton,
                GUILayout.Width(40f));

            GUILayout.FlexibleSpace();
            mSearchText = GUILayout.TextField(
                mSearchText ?? string.Empty,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(120f),
                GUILayout.MaxWidth(220f));

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 配置速查 放在顶部保证任意分辨率都能看到
        /// </summary>
        private void DrawHelp()
        {
            if (!mShowHelp) return;

            EditorGUILayout.HelpBox(
                "层数分类：共享=一份Runtime乘层数 | 各层独立=每层一份各自计时\n" +
                "每次应用增加层数：共享 StackCount+N | 独立一次新建N份\n" +
                "刷新时长=重置buff总时长 | 重置周期=重排跳伤拍子",
                MessageType.Info);
            EditorGUILayout.Space(2f);
        }

        /// <summary>
        /// 新建行
        /// </summary>
        private void DrawCreateRow()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            mNewName = EditorGUILayout.TextField(mNewName);
            mNewDuration = (E_EffectDuration)EditorGUILayout.Popup(
                (int)mNewDuration,
                new[] { "即时", "持续", "永久" },
                GUILayout.Width(72f));
            mNewPeriodic = GUILayout.Toggle(
                mNewPeriodic,
                new GUIContent("周期", "创建时勾选「启用周期跳伤」"),
                GUILayout.Width(48f));

            if (GUILayout.Button("创建", GUILayout.Width(52f)))
                CreateAndSelect();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"保存到 {GasEffectEditorUtility.DefaultFolderPath}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 左侧列表
        /// </summary>
        private void DrawEffectList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(mListWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("GE 列表", EditorStyles.boldLabel);

            mListScroll = EditorGUILayout.BeginScrollView(
                mListScroll,
                GUILayout.ExpandHeight(true));

            for (int i = 0; i < mEffectList.Count; i++)
            {
                GameplayEffectData data = mEffectList[i];
                if (data == null) continue;
                if (!PassFilter(data)) continue;
                if (!string.IsNullOrEmpty(mSearchText) &&
                    data.name.IndexOf(mSearchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                DrawEffectRow(data);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 左右栏竖向分割线 可拖拽改左栏宽度
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
        /// 单条 GE 行 整行左侧可点选
        /// </summary>
        private void DrawEffectRow(GameplayEffectData data)
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

            string badge = GasEffectEditorUtility.GetDurationLabel(data.Duration);
            if (data.IsPeriodic) badge += "+周期";
            if (data.IsIndependentStack) badge += "+独立";

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.label.Draw(labelRect, $"[{badge}]  {data.name}", false, false, false, false);
                EditorStyles.miniLabel.Draw(
                    previewRect,
                    GasEffectEditorUtility.BuildPreviewText(data),
                    false,
                    false,
                    false,
                    false);
            }

            if (GasEditorListGUI.SelectableContent(rowRect, rightReserved))
                SelectEffect(data);

            if (GUI.Button(pingRect, "定位"))
            {
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
            }

            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUI.Button(deleteRect, "删除"))
                TryDeleteEffect(data);
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
                EditorGUILayout.HelpBox("点击左侧条目选中一个 GE", MessageType.None);
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
                TryDeleteEffect(mSelected);
            GUI.backgroundColor = old;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 筛选
        /// </summary>
        private bool PassFilter(GameplayEffectData data)
        {
            switch (mListFilter)
            {
                case E_ListFilter.Instant:
                    return data.IsInstant;
                case E_ListFilter.HasDuration:
                    return data.HasDuration;
                case E_ListFilter.Infinite:
                    return data.IsInfinite;
                case E_ListFilter.Independent:
                    return data.IsIndependentStack;
                case E_ListFilter.Shared:
                    return !data.IsIndependentStack;
                case E_ListFilter.Periodic:
                    return data.IsPeriodic;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 选中
        /// </summary>
        private void SelectEffect(GameplayEffectData data)
        {
            if (mSelected == data) return;
            mSelected = data;
            mDetailScroll = Vector2.zero;
            //延后重建详情 Editor 避免当帧 DestroyImmediate 导致点击偶发失效
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
        private void TryDeleteEffect(GameplayEffectData data)
        {
            if (data == null) return;

            string name = data.name;
            if (!EditorUtility.DisplayDialog("删除 GE 资产", $"确认删除 {name} ?\n此操作不可撤销", "删除", "取消"))
                return;

            bool deleted = GasEffectEditorUtility.TryDeleteEffectData(data);
            if (!deleted)
            {
                mStatusMessage = $"删除失败 {name}";
                return;
            }

            if (mSelected == data)
                SelectEffect(null);

            RefreshList();
            mStatusMessage = $"已删除 {name}";
        }

        /// <summary>
        /// 创建并选中
        /// </summary>
        private void CreateAndSelect()
        {
            string name = string.IsNullOrWhiteSpace(mNewName) ? "GE_New" : mNewName;
            GameplayEffectData data = GasEffectEditorUtility.CreateEffectData(name, mNewDuration, mNewPeriodic);
            RefreshList();
            SelectEffect(data);
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            mStatusMessage =
                $"已创建 {data.name} ({GasEffectEditorUtility.GetDurationLabel(mNewDuration)}" +
                (mNewPeriodic ? "+周期" : string.Empty) + ")";
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshList()
        {
            mEffectList = GasEffectEditorUtility.FindAllEffectDataList();
            if (mSelected != null && !mEffectList.Contains(mSelected))
                SelectEffect(null);
        }
    }
}
