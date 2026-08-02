using System.Collections.Generic;
using GAS.Core.GameplayEffect;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Effect
{
    /// <summary>
    /// GE 编辑器工具 扫描创建删除与预览
    /// </summary>
    public static class GasEffectEditorUtility
    {
        /// <summary> 默认新建目录 </summary>
        public const string DefaultFolderPath = "Assets/_Scripts/GAS/Core/Effect/Data/So";

        /// <summary>
        /// 扫描项目内全部 GameplayEffectData
        /// </summary>
        public static List<GameplayEffectData> FindAllEffectDataList()
        {
            List<GameplayEffectData> resultList = new List<GameplayEffectData>();
            string[] guidList = AssetDatabase.FindAssets("t:GameplayEffectData");
            for (int i = 0; i < guidList.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guidList[i]);
                GameplayEffectData data = AssetDatabase.LoadAssetAtPath<GameplayEffectData>(path);
                if (data != null)
                    resultList.Add(data);
            }

            resultList.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return resultList;
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
        /// 创建 GE 资产
        /// </summary>
        public static GameplayEffectData CreateEffectData(
            string assetName,
            E_EffectDuration durationPolicy,
            bool isPeriodic = false)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                assetName = "GE_New";

            assetName = assetName.Trim();
            EnsureDefaultFolder();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolderPath}/{assetName}.asset");
            GameplayEffectData data = ScriptableObject.CreateInstance<GameplayEffectData>();

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("durationPolicy").enumValueIndex = (int)durationPolicy;
            so.FindProperty("isPeriodic").boolValue = isPeriodic;
            if (durationPolicy == E_EffectDuration.HasDuration)
                so.FindProperty("duration").floatValue = 5f;
            if (isPeriodic)
                so.FindProperty("period").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        /// <summary>
        /// 删除 GE 资产
        /// </summary>
        public static bool TryDeleteEffectData(GameplayEffectData data)
        {
            if (data == null) return false;
            string path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return false;
            return AssetDatabase.DeleteAsset(path);
        }

        /// <summary>
        /// 时间策略显示名
        /// </summary>
        public static string GetDurationLabel(E_EffectDuration duration)
        {
            switch (duration)
            {
                case E_EffectDuration.Instant: return "即时";
                case E_EffectDuration.HasDuration: return "持续";
                case E_EffectDuration.Infinite: return "永久";
                default: return duration.ToString();
            }
        }

        /// <summary>
        /// 层数存法显示名
        /// </summary>
        public static string GetAggregationLabel(E_EffectStackAggregation aggregation)
        {
            return aggregation == E_EffectStackAggregation.IndependentInstances ? "独立" : "共享";
        }

        /// <summary>
        /// 列表预览一行
        /// </summary>
        public static string BuildPreviewText(GameplayEffectData data)
        {
            if (data == null) return string.Empty;

            string durationPart = GetDurationLabel(data.Duration);
            if (data.HasDuration)
                durationPart += $" {data.DurationValue:0.##}s";

            string periodPart = data.IsPeriodic ? $"周期{data.Period:0.##}s" : "无周期";
            string stackPart =
                $"{GetAggregationLabel(data.StackAggregation)} 上限{data.StackLimit} 每次+{data.StacksPerApplication}";

            int modCount = data.StatModifierConfig != null ? data.StatModifierConfig.Count : 0;
            return $"{durationPart}  |  {periodPart}  |  {stackPart}  |  修饰符×{modCount}";
        }
    }
}
