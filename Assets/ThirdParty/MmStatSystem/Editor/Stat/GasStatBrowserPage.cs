using System.Collections.Generic;
using GAS.Editor;
using GAS.StateSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Stat
{
    /// <summary>
    /// Stat Browser 页签 浏览创建删除与预览属性资产
    /// </summary>
    public sealed class GasStatBrowserPage : GasEditorPage
    {
        public override string Title => "Stat";

        /// <summary> 筛选模式 </summary>
        private enum E_FilterMode
        {
            All = 0,
            Passive = 1,
            Immediate = 2,
        }

        /// <summary> 缓存列表 </summary>
        private List<StatData> mStatDataList = new List<StatData>();

        /// <summary> 搜索 </summary>
        private string mSearchText = string.Empty;

        /// <summary> 筛选 </summary>
        private E_FilterMode mFilterMode = E_FilterMode.All;

        /// <summary> 新建名 </summary>
        private string mNewName = "NewStat";

        /// <summary> 新建类型 </summary>
        private E_StatType mNewType = E_StatType.Passive;

        /// <summary> 新建基础值 </summary>
        private float mNewBaseValue = 100f;

        /// <summary> 选中资产 </summary>
        private StatData mSelected;

        /// <summary> 列表滚动 </summary>
        private Vector2 mListScroll;

        /// <summary> 详情序列化 </summary>
        private SerializedObject mSelectedSo;

        /// <summary> 状态 </summary>
        private string mStatusMessage = string.Empty;

        /// <summary> 是否展开修饰符说明 </summary>
        private bool mShowModifierHelp;

        /// <summary>
        /// 启用时刷新
        /// </summary>
        public override void OnEnable()
        {
            RefreshList();
        }

        /// <summary>
        /// 绘制页签
        /// </summary>
        public override void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawCreateRow();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            DrawStatList();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            DrawModifierHelp();

            if (!string.IsNullOrEmpty(mStatusMessage))
                EditorGUILayout.HelpBox(mStatusMessage, MessageType.Info);
        }

        /// <summary>
        /// 工具条
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshList();
                mStatusMessage = $"已刷新 共 {mStatDataList.Count} 个 StatData";
            }

            mFilterMode = (E_FilterMode)GUILayout.Toolbar(
                (int)mFilterMode,
                new[] { "全部", "被动", "即时" },
                EditorStyles.toolbarButton,
                GUILayout.Width(180f));

            GUILayout.FlexibleSpace();
            mSearchText = GUILayout.TextField(mSearchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 新建行
        /// </summary>
        private void DrawCreateRow()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("快速创建", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            mNewName = EditorGUILayout.TextField("资产名(即StatId)", mNewName);
            mNewType = (E_StatType)EditorGUILayout.EnumPopup(mNewType, GUILayout.Width(100f));
            mNewBaseValue = EditorGUILayout.FloatField(mNewBaseValue, GUILayout.Width(80f));

            if (GUILayout.Button("创建被动", GUILayout.Width(70f)))
                CreateAndSelect(E_StatType.Passive);

            if (GUILayout.Button("创建即时", GUILayout.Width(70f)))
                CreateAndSelect(E_StatType.Immediate);

            if (GUILayout.Button("按上方类型创建", GUILayout.Width(100f)))
                CreateAndSelect(mNewType);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"保存到: {GasStatEditorUtility.DefaultFolderPath}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 左侧列表
        /// </summary>
        private void DrawStatList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(340f));
            EditorGUILayout.LabelField("属性列表", EditorStyles.boldLabel);
            mListScroll = EditorGUILayout.BeginScrollView(mListScroll, GUILayout.MinHeight(280f));

            for (int i = 0; i < mStatDataList.Count; i++)
            {
                StatData data = mStatDataList[i];
                if (data == null) continue;
                if (!PassFilter(data)) continue;
                if (!string.IsNullOrEmpty(mSearchText) &&
                    data.name.IndexOf(mSearchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                DrawStatRow(data);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制单条属性行
        /// </summary>
        private void DrawStatRow(StatData data)
        {
            bool selected = mSelected == data;
            Rect rowRect = GasEditorListGUI.BeginRow(40f, selected);

            const float buttonWidth = 40f;
            const float buttonGap = 2f;
            float rightReserved = buttonWidth + 44f + buttonGap * 3f;

            Rect deleteRect = new Rect(rowRect.xMax - 44f - buttonGap, rowRect.y + 4f, 44f, 18f);
            Rect pingRect = new Rect(deleteRect.x - buttonWidth - buttonGap, rowRect.y + 4f, buttonWidth, 18f);
            Rect labelRect = new Rect(rowRect.x + 4f, rowRect.y + 2f, pingRect.x - rowRect.x - 8f, 18f);
            Rect previewRect = new Rect(rowRect.x + 4f, rowRect.y + 20f, rowRect.width - rightReserved - 8f, 16f);

            string typeLabel = GasStatEditorUtility.GetTypeLabel(data.StatType);
            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.label.Draw(labelRect, $"[{typeLabel}]  {data.name}", false, false, false, false);
                EditorStyles.miniLabel.Draw(
                    previewRect,
                    GasStatEditorUtility.BuildPreviewText(data),
                    false,
                    false,
                    false,
                    false);
            }

            if (GasEditorListGUI.SelectableContent(rowRect, rightReserved))
                SelectStat(data);

            if (GUI.Button(pingRect, "定位"))
            {
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
            }

            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUI.Button(deleteRect, "删除"))
                TryDeleteStat(data);
            GUI.backgroundColor = old;
        }

        /// <summary>
        /// 右侧详情
        /// </summary>
        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("选中详情", EditorStyles.boldLabel);

            if (mSelected == null)
            {
                EditorGUILayout.HelpBox("在左侧点选一个 StatData", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            if (mSelectedSo == null || mSelectedSo.targetObject != mSelected)
                mSelectedSo = new SerializedObject(mSelected);

            mSelectedSo.Update();
            EditorGUILayout.LabelField("StatId", mSelected.name);
            EditorGUILayout.LabelField("路径", AssetDatabase.GetAssetPath(mSelected));
            EditorGUILayout.PropertyField(mSelectedSo.FindProperty(GasStatEditorUtility.StatTypePropertyName));
            EditorGUILayout.PropertyField(mSelectedSo.FindProperty(GasStatEditorUtility.BaseValuePropertyName));
            EditorGUILayout.PropertyField(mSelectedSo.FindProperty(GasStatEditorUtility.MinValuePropertyName));
            EditorGUILayout.PropertyField(mSelectedSo.FindProperty(GasStatEditorUtility.MaxValuePropertyName));
            EditorGUILayout.PropertyField(mSelectedSo.FindProperty(GasStatEditorUtility.ResetOnPlayPropertyName));
            mSelectedSo.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("在 Project 中选中", GUILayout.Height(24f)))
            {
                Selection.activeObject = mSelected;
                EditorGUIUtility.PingObject(mSelected);
            }

            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("删除资产", GUILayout.Height(24f), GUILayout.Width(90f)))
                TryDeleteStat(mSelected);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 修饰符阶段说明
        /// </summary>
        private void DrawModifierHelp()
        {
            mShowModifierHelp = EditorGUILayout.Foldout(mShowModifierHelp, "被动属性四阶段修饰符说明", true);
            if (!mShowModifierHelp) return;

            EditorGUILayout.HelpBox(
                "计算顺序:\n" +
                "1 FlatAdd 平加\n" +
                "2 PercentageAdd 百分比乘区 (1 + sum%/100)\n" +
                "3 FinalAdd 最终平加\n" +
                "4 FinalPercentage 最终百分比\n" +
                "最后 Clamp 到 Min/Max\n\n" +
                "即时属性也会复用 E_ModifierType 但只对当前值做一次性运算 不挂列表",
                MessageType.None);
        }

        /// <summary>
        /// 筛选
        /// </summary>
        private bool PassFilter(StatData data)
        {
            if (mFilterMode == E_FilterMode.Passive) return data.StatType == E_StatType.Passive;
            if (mFilterMode == E_FilterMode.Immediate) return data.StatType == E_StatType.Immediate;
            return true;
        }

        /// <summary>
        /// 选中
        /// </summary>
        private void SelectStat(StatData data)
        {
            mSelected = data;
            mSelectedSo = data != null ? new SerializedObject(data) : null;
        }

        /// <summary>
        /// 删除选中或指定资产
        /// </summary>
        private void TryDeleteStat(StatData data)
        {
            if (data == null) return;

            string name = data.name;
            if (!EditorUtility.DisplayDialog("删除属性资产", $"确认删除 {name} ?\n此操作不可撤销", "删除", "取消"))
                return;

            bool deleted = GasStatEditorUtility.TryDeleteStatData(data);
            if (!deleted)
            {
                mStatusMessage = $"删除失败 {name}";
                return;
            }

            if (mSelected == data)
                SelectStat(null);

            RefreshList();
            mStatusMessage = $"已删除 {name}";
        }

        /// <summary>
        /// 创建并选中
        /// </summary>
        private void CreateAndSelect(E_StatType statType)
        {
            string name = string.IsNullOrWhiteSpace(mNewName)
                ? (statType == E_StatType.Immediate ? "NewImmediateStat" : "NewPassiveStat")
                : mNewName;

            StatData data = GasStatEditorUtility.CreateStatData(name, statType, mNewBaseValue);
            RefreshList();
            SelectStat(data);
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            mStatusMessage = $"已创建 {data.name} ({GasStatEditorUtility.GetTypeLabel(statType)})";
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshList()
        {
            mStatDataList = GasStatEditorUtility.FindAllStatDataList();
            if (mSelected != null && !mStatDataList.Contains(mSelected))
                SelectStat(null);
        }
    }
}
