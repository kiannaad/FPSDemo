using System.Collections.Generic;
using GAS.AbilitySystem;
using UnityEditor;
using UnityEngine;

namespace GAS.Editor.Ability
{
    /// <summary>
    /// Ability 编辑器工具 扫描创建删除与预览
    /// </summary>
    public static class GasAbilityEditorUtility
    {
        /// <summary> 默认新建目录 </summary>
        public const string DefaultFolderPath = "Assets/_Scripts/GAS/Core/AbilitySystem/Data/So";

        /// <summary>
        /// 扫描项目内全部 GameplayAbilityData
        /// </summary>
        public static List<GameplayAbilityData> FindAllAbilityDataList()
        {
            List<GameplayAbilityData> resultList = new List<GameplayAbilityData>();
            string[] guidList = AssetDatabase.FindAssets("t:GameplayAbilityData");
            for (int i = 0; i < guidList.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guidList[i]);
                GameplayAbilityData data = AssetDatabase.LoadAssetAtPath<GameplayAbilityData>(path);
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
        /// 创建 Ability 资产
        /// </summary>
        public static GameplayAbilityData CreateAbilityData(
            string assetName,
            float cooldownTime = 1f,
            bool withMpCost = false)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                assetName = "NewAbility";

            assetName = assetName.Trim();
            EnsureDefaultFolder();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolderPath}/{assetName}.asset");
            GameplayAbilityData data = ScriptableObject.CreateInstance<GameplayAbilityData>();

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("abilityName").stringValue = assetName;
            so.FindProperty("cooldownTime").floatValue = Mathf.Max(0f, cooldownTime);
            if (withMpCost)
            {
                so.FindProperty("costStatId").stringValue = "MP";
                so.FindProperty("costType").enumValueIndex = (int)E_CostType.Fixed;
                so.FindProperty("costValue").floatValue = 40f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        /// <summary>
        /// 删除 Ability 资产
        /// </summary>
        public static bool TryDeleteAbilityData(GameplayAbilityData data)
        {
            if (data == null) return false;
            string path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return false;
            return AssetDatabase.DeleteAsset(path);
        }

        /// <summary>
        /// 列表预览一行
        /// </summary>
        public static string BuildPreviewText(GameplayAbilityData data)
        {
            if (data == null) return string.Empty;

            string cdPart = data.cooldownTime > 0f ? $"CD {data.cooldownTime:0.##}s" : "无CD";
            string costPart = "无消耗";
            if (!string.IsNullOrEmpty(data.costStatId) && data.costValue > 0f)
                costPart = $"{data.costStatId} {data.costValue:0.##}";

            return $"{cdPart}  ·  {costPart}";
        }
    }
}
