using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS.StateSystem
{
    /// <summary>
    /// 属性控制器 分桶管理被动属性与即时属性
    /// </summary>
    public class StatController : MonoBehaviour
    {
        [SerializeField]
        private List<StatData> statDataList = new();

        /// <summary> 被动属性字典 </summary>
        private readonly Dictionary<string, IPassiveStat> passiveStatDict =
            new Dictionary<string, IPassiveStat>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 即时属性字典 </summary>
        private readonly Dictionary<string, IImmediateStat> immediateStatDict =
            new Dictionary<string, IImmediateStat>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, IPassiveStat> PassiveStatDict => passiveStatDict;
        public IReadOnlyDictionary<string, IImmediateStat> ImmediateStatDict => immediateStatDict;

        /// <summary>
        /// 初始化属性 按类型分桶
        /// </summary>
        public void Init()
        {
            passiveStatDict.Clear();
            immediateStatDict.Clear();

            foreach (var data in statDataList)
            {
                if (data == null) continue;

                string key = data.name;
                if (string.IsNullOrWhiteSpace(key)) continue;

                //同名不可跨桶重复
                if (passiveStatDict.ContainsKey(key) || immediateStatDict.ContainsKey(key))
                {
                    Debug.LogWarning($"[StatController] 属性名重复已跳过: {key}");
                    continue;
                }

                if (data.StatType == E_StatType.Immediate)
                {
                    var imStat = new ImmediateStat(data, this);
                    imStat.Initialize();
                    immediateStatDict[key] = imStat;
                }
                else
                {
                    var passiveStat = new PassiveStat(data, this);
                    passiveStat.Initialize();
                    passiveStatDict[key] = passiveStat;
                }
            }
        }

        /// <summary>
        /// 获取基础值 两类都可查
        /// </summary>
        public float GetValue(string statName)
        {
            if (immediateStatDict.TryGetValue(statName, out var imStat))
                return imStat.BaseValue;

            if (passiveStatDict.TryGetValue(statName, out var passiveStat))
                return passiveStat.BaseValue;

            return 0f;
        }

        /// <summary>
        /// 获取当前展示值 即时取Current 被动取Final
        /// </summary>
        public float GetCurrentValue(string statName)
        {
            if (immediateStatDict.TryGetValue(statName, out var imStat))
                return imStat.CurrentValue;

            if (passiveStatDict.TryGetValue(statName, out var passiveStat))
                return passiveStat.FinalValue;

            return 0f;
        }

        #region 被动属性

        /// <summary>
        /// 获取被动属性
        /// </summary>
        public IPassiveStat GetPassiveStat(string statName)
        {
            if (string.IsNullOrWhiteSpace(statName)) return null;
            passiveStatDict.TryGetValue(statName, out var passiveStat);
            return passiveStat;
        }

        /// <summary>
        /// 添加修饰符
        /// </summary>
        public void AddModifier(string statName, StatModifier modifier)
        {
            var passiveStat = GetPassiveStat(statName);
            if (passiveStat == null || modifier == null) return;
            passiveStat.AddModifier(modifier);
        }

        /// <summary>
        /// 从来源移除修饰符
        /// </summary>
        public void RemoveModifiersFromSource(string statName, object source)
        {
            var passiveStat = GetPassiveStat(statName);
            if (passiveStat is null) return;
            passiveStat.RemoveModifiersFromSource(source);
        }

        #endregion

        #region 即时属性

        /// <summary>
        /// 获取即时属性
        /// </summary>
        public IImmediateStat GetImStat(string statName)
        {
            if (string.IsNullOrWhiteSpace(statName)) return null;
            immediateStatDict.TryGetValue(statName, out var imStat);
            return imStat;
        }

        /// <summary>
        /// 对即时属性做瞬时变化
        /// </summary>
        public void ChangeAttributeValue(string statName, float magnitude, E_ModifierType modifierType, object source = null)
        {
            var imStat = GetImStat(statName);
            if (imStat is null) return;
            imStat.ChangeValue(magnitude, modifierType);
        }

        #endregion
    }
}
