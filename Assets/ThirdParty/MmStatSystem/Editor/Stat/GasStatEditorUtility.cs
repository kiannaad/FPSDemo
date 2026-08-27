using System.Collections.Generic;
using GAS.StateSystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Stat
{
    /// <summary>
    /// Stat 编辑器工具 扫描创建与列表查询
    /// </summary>
    public static class GasStatEditorUtility
    {
        /// <summary> 默认新建目录 </summary>
        public const string DefaultFolderPath = "Assets/_Scripts/GAS/Core/StatSystem/Data";

        /// <summary> 序列化字段名 </summary>
        public const string StatTypePropertyName = "statType";
        public const string BaseValuePropertyName = "baseValue";
        public const string MinValuePropertyName = "minValue";
        public const string MaxValuePropertyName = "maxValue";
        public const string ResetOnPlayPropertyName = "resetCurrentValueOnPlay";

        /// <summary>
        /// 扫描项目内全部 StatData
        /// </summary>
        public static List<StatData> FindAllStatDataList()
        {
            List<StatData> resultList = new List<StatData>();
            string[] guidList = AssetDatabase.FindAssets("t:StatData");
            for (int i = 0; i < guidList.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guidList[i]);
                StatData data = AssetDatabase.LoadAssetAtPath<StatData>(path);
                if (data != null)
                    resultList.Add(data);
            }

            resultList.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return resultList;
        }

        /// <summary>
        /// 获取 StatId 名列表
        /// </summary>
        public static List<string> GetStatIdList(bool passiveOnly = false, bool immediateOnly = false)
        {
            List<StatData> dataList = FindAllStatDataList();
            List<string> idList = new List<string>(dataList.Count);
            for (int i = 0; i < dataList.Count; i++)
            {
                StatData data = dataList[i];
                if (passiveOnly && data.StatType != E_StatType.Passive) continue;
                if (immediateOnly && data.StatType != E_StatType.Immediate) continue;
                idList.Add(data.name);
            }
            return idList;
        }

        /// <summary>
        /// 确保默认目录存在
        /// </summary>
        public static void EnsureDefaultFolder()
        {
            if (AssetDatabase.IsValidFolder(DefaultFolderPath)) return;

            string[] partList = DefaultFolderPath.Split('/');
            string current = partList[0];
            for (int i = 1; i < partList.Length; i++)
            {
                string next = current + "/" + partList[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, partList[i]);
                current = next;
            }
        }

        /// <summary>
        /// 创建 StatData 资产
        /// </summary>
        public static StatData CreateStatData(string assetName, E_StatType statType, float baseValue = 100f)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                assetName = statType == E_StatType.Immediate ? "NewImmediateStat" : "NewPassiveStat";

            assetName = assetName.Trim();
            EnsureDefaultFolder();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolderPath}/{assetName}.asset");
            StatData data = ScriptableObject.CreateInstance<StatData>();

            SerializedObject so = new SerializedObject(data);
            so.FindProperty(StatTypePropertyName).enumValueIndex = (int)statType;
            so.FindProperty(BaseValuePropertyName).floatValue = baseValue;
            if (statType == E_StatType.Immediate)
            {
                so.FindProperty(MinValuePropertyName).floatValue = 0f;
                so.FindProperty(MaxValuePropertyName).floatValue = baseValue;
                so.FindProperty(ResetOnPlayPropertyName).boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        /// <summary>
        /// 删除 StatData 资产
        /// </summary>
        public static bool TryDeleteStatData(StatData data)
        {
            if (data == null) return false;
            string path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return false;
            return AssetDatabase.DeleteAsset(path);
        }

        /// <summary>
        /// 类型显示名
        /// </summary>
        public static string GetTypeLabel(E_StatType statType)
        {
            return statType == E_StatType.Immediate ? "即时" : "被动";
        }

        /// <summary>
        /// 数值预览文本
        /// </summary>
        public static string BuildPreviewText(StatData data)
        {
            if (data == null) return string.Empty;
            return $"Base={data.BaseValue}  Min={FormatBound(data.MinValue)}  Max={FormatBound(data.MaxValue)}";
        }

        /// <summary>
        /// 格式化边界值
        /// </summary>
        private static string FormatBound(float value)
        {
            if (Mathf.Approximately(value, float.MinValue)) return "-∞";
            if (Mathf.Approximately(value, float.MaxValue)) return "+∞";
            return value.ToString("0.##");
        }
    }
}
