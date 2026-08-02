using System;
using System.Collections.Generic;
using GAS.Component;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// GE管理器 负责应用 堆叠 周期 持续时间 移除
    /// </summary>
    public class GEManager : MonoBehaviour
    {
        /// <summary> 压测时置 true 关掉热路径 Log </summary>
        public static bool QuietMode;

        /// <summary> 当前已应用的GE列表 </summary>
        private readonly List<GameplayEffectRuntime> appliedEffectList = new();

        /// <summary> GE授予标签引用计数 </summary>
        private readonly Dictionary<string, int> gameplayTagCountDict = new(StringComparer.OrdinalIgnoreCase);

        /// <summary> 外部属性控制器 </summary>
        private StatController statController;

        /// <summary> 所属ASC </summary>
        private AbilitySystemMgr owner;

        /// <summary>
        /// 设置属性控制器
        /// </summary>
        public void SetStatController(StatController sc) => statController = sc;

        /// <summary>
        /// 设置所属ASC
        /// </summary>
        public void SetOwner(AbilitySystemMgr asc) => owner = asc;

        /// <summary>
        /// 当前GE数量 测试用
        /// </summary>
        public int AppliedCount => appliedEffectList.Count;

        /// <summary>
        /// 当前已应用 GE 列表
        /// </summary>
        public IReadOnlyList<GameplayEffectRuntime> AppliedEffects => appliedEffectList;

        /// <summary>
        /// 按数据查找已存在的第一份 测试用
        /// </summary>
        public GameplayEffectRuntime FindSpec(GameplayEffectData effectData) => FindMatchingEffect(effectData);

        /// <summary>
        /// 同数据已应用份数 测试用
        /// </summary>
        public int CountEffect(GameplayEffectData effectData) => CountMatchingEffect(effectData);

        /// <summary>
        /// 取同数据第 ordinal 份 从0起 测试用
        /// </summary>
        public GameplayEffectRuntime GetEffect(GameplayEffectData effectData, int ordinal)
        {
            int matched = 0;
            for (int i = 0; i < appliedEffectList.Count; i++)
            {
                if (appliedEffectList[i].GEData != effectData) continue;
                if (matched == ordinal)
                    return appliedEffectList[i];
                matched++;
            }
            return null;
        }

        #region 公开接口

        /// <summary>
        /// 应用GE
        /// </summary>
        public GameplayEffectRuntime ApplyGE(GameplayEffectData effectData, object source)
        {
            if (effectData is null) { Debug.Log("GE为空"); return null; }
            if (statController is null) { Debug.Log("StatController 未设置"); return null; }
            if (!CanApplyGE(effectData)) { Debug.Log($"[GEManager] 标签条件不满足: {effectData.name}"); return null; }

            if (effectData.IsInstant)
            {
                GameplayEffectRuntime instantRuntime = new GameplayEffectRuntime(effectData, source);
                ApplyModifiersToStat(instantRuntime);
                return instantRuntime;
            }

            //各层独立 未满则新建一份Runtime
            if (effectData.IsIndependentStack)
                return ApplyIndependent(effectData, source);

            //共享层数 已有则走再应用流水线
            GameplayEffectRuntime existGe = FindMatchingEffect(effectData);
            if (existGe is not null)
                return HandleStacking(existGe, effectData);

            return AddNewEffect(effectData, source);
        }

        /// <summary>
        /// 移除所有GE
        /// </summary>
        public void RemoveAllGE()
        {
            for (int i = appliedEffectList.Count - 1; i >= 0; i--)
                RemoveGameplayEffect(appliedEffectList[i], i);
        }

        /// <summary>
        /// 每帧更新 先扣寿命再扣周期
        /// </summary>
        public void UpdateGE(float deltaTime)
        {
            // 倒序遍历 确保先处理完所有GE再处理周期
            for (int i = appliedEffectList.Count - 1; i >= 0; i--)
            {
                GameplayEffectRuntime spec = appliedEffectList[i];

                //1 扣寿命 到期则处理并跳过本帧周期
                if (TickDuration(spec, deltaTime, i))
                    continue;

                //2 扣周期 到点跳伤
                TickPeriod(spec, deltaTime);
            }
        }

        #endregion

        #region 每帧计时

        /// <summary>
        /// 扣剩余时长 若已到期返回true
        /// </summary>
        /// <param name="runtime">GE运行时实例</param>
        /// <param name="deltaTime">时间增量</param>
        /// <param name="index">索引</param>
        /// <returns>是否到期</returns>
        private bool TickDuration(GameplayEffectRuntime runtime, float deltaTime, int index)
        {
            if (!runtime.GEData.HasDuration) return false;

            runtime.RemainingDuration -= deltaTime;
            if (!runtime.IsExpired) return false;

            HandleExpiration(runtime, index);
            return true;
        }

        /// <summary>
        /// 扣周期 到点则跳伤并重置周期倒计时
        /// </summary>
        /// <param name="spec">GE运行时实例</param>
        /// <param name="dt">时间增量</param>
        private void TickPeriod(GameplayEffectRuntime spec, float dt)
        {
            if (!spec.GEData.IsPeriodic) return;

            spec.RemainingPeriod -= dt;
            if (!spec.ShouldExecutePeriod) return;

            ApplyPeriodicEffect(spec);
            spec.ResetPeriod();
        }

        #endregion

        #region 再应用流水线

        /// <summary>
        /// 各层独立 按每次增加层数新建多份 满层则尽可能填剩余空位
        /// </summary>
        private GameplayEffectRuntime ApplyIndependent(GameplayEffectData effectData, object source)
        {
            int count = CountMatchingEffect(effectData);
            int remaining = effectData.StackLimit - count;
            if (remaining <= 0)
            {
                if (!QuietMode)
                    Debug.Log($"[GE] {effectData.name} 独立层已满 {effectData.StackLimit}");
                return null;
            }

            int toAdd = effectData.StacksPerApplication;
            if (toAdd > remaining)
                toAdd = remaining;

            GameplayEffectRuntime lastRuntime = null;
            for (int i = 0; i < toAdd; i++)
                lastRuntime = AddNewEffect(effectData, source);

            if (!QuietMode)
                Debug.Log($"[GE] {effectData.name} 新建独立层×{toAdd} 当前份数: {count + toAdd}");
            return lastRuntime;
        }

        /// <summary>
        /// 新建并挂上列表
        /// </summary>
        private GameplayEffectRuntime AddNewEffect(GameplayEffectData effectData, object source)
        {
            GameplayEffectRuntime runtime = new GameplayEffectRuntime(effectData, source);

            //共享层数 首次应用就按每次增加层数写入 StackCount
            if (!effectData.IsIndependentStack)
            {
                int initialStack = effectData.StacksPerApplication;
                if (initialStack > effectData.StackLimit)
                    initialStack = effectData.StackLimit;
                runtime.StackCount = initialStack;
            }

            appliedEffectList.Add(runtime);
            AddGrantedTags(effectData);
            ApplyModifiersToStat(runtime);
            return runtime;
        }

        /// <summary>
        /// 查找同数据的已有GE 共享层数用
        /// </summary>
        private GameplayEffectRuntime FindMatchingEffect(GameplayEffectData effectData)
        {
            for (int i = 0; i < appliedEffectList.Count; i++)
            {
                if (appliedEffectList[i].GEData == effectData)
                    return appliedEffectList[i];
            }
            return null;
        }

        /// <summary>
        /// 同数据已有份数
        /// </summary>
        private int CountMatchingEffect(GameplayEffectData effectData)
        {
            int count = 0;
            for (int i = 0; i < appliedEffectList.Count; i++)
            {
                if (appliedEffectList[i].GEData == effectData)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 再应用同名GE 固定四步 层数→刷新时长→重置周期→按需重挂修饰符
        /// </summary>
        private GameplayEffectRuntime HandleStacking(GameplayEffectRuntime existingRuntime,
                                                     GameplayEffectData effectData)
        {
            int oldStackCount = existingRuntime.StackCount;

            //1 层数怎么变
            ResolveStackCount(existingRuntime, effectData);

            //2 要不要刷新时长 续命
            TryRefreshDuration(existingRuntime, effectData);

            //3 要不要重置周期 重排跳伤
            TryResetPeriod(existingRuntime, effectData);

            //4 层数变了才重挂修饰符 避免满层仅续命时重复改即时属性
            if (existingRuntime.StackCount != oldStackCount)
                ReapplyModifiers(existingRuntime);

            if (!QuietMode)
                Debug.Log($"[GE] {effectData.name} 再应用后层数: {existingRuntime.StackCount}");
            return existingRuntime;
        }

        /// <summary>
        /// 共享层数 每次应用按 StacksPerApplication 增加 受上限卡住
        /// </summary>
        private void ResolveStackCount(GameplayEffectRuntime existingSpec, GameplayEffectData effectData)
        {
            if (existingSpec.StackCount >= effectData.StackLimit)
            {
                if (!QuietMode)
                    Debug.Log($"GE{effectData.name}已达最大堆叠层数{effectData.StackLimit}");
                return;
            }

            int newCount = existingSpec.StackCount + effectData.StacksPerApplication;
            if (newCount > effectData.StackLimit)
                newCount = effectData.StackLimit;
            existingSpec.StackCount = newCount;
        }

        /// <summary>
        /// 再应用时是否续命
        /// </summary>
        private void TryRefreshDuration(GameplayEffectRuntime existingSpec, GameplayEffectData effectData)
        {
            if (effectData.DurationRefresh != E_EffectDurationRefresh.RefreshOnSuccessfulApplication)
                return;
            existingSpec.RefreshDuration();
        }

        /// <summary>
        /// 再应用时是否重排跳伤拍子
        /// </summary>
        private void TryResetPeriod(GameplayEffectRuntime existingSpec, GameplayEffectData effectData)
        {
            if (effectData.PeriodReset != E_EffectPeriodReset.ResetOnSuccessfulApplication)
                return;
            existingSpec.ResetPeriod();
        }

        /// <summary>
        /// 按当前层数重挂修饰符
        /// </summary>
        private void ReapplyModifiers(GameplayEffectRuntime existingSpec)
        {
            ApplyModifiersToStat(existingSpec, isRemove: true);
            ApplyModifiersToStat(existingSpec, isRemove: false);
        }

        #endregion

        #region 周期与到期

        /// <summary>
        /// 周期跳伤 直接读配置 不每次 new 修饰符
        /// </summary>
        private void ApplyPeriodicEffect(GameplayEffectRuntime spec)
        {
            var configList = spec.GEData.StatModifierConfig;
            int stackCount = spec.StackCount;

            for (int i = 0; i < configList.Count; i++)
            {
                var config = configList[i];
                float finalValue = config.value * stackCount;
                statController.ChangeAttributeValue(config.statId, finalValue, config.type, spec.Source);
            }
        }

        /// <summary>
        /// 处理GE到期
        /// </summary>
        private void HandleExpiration(GameplayEffectRuntime spec, int i)
        {
            switch (spec.GEData.Expiration)
            {
                case E_EffectExpiration.ClearEntireStack:
                    RemoveGameplayEffect(spec, i);
                    break;

                case E_EffectExpiration.RemoveSingleStackAndRefreshDuration:
                    spec.StackCount--;
                    if (spec.StackCount > 0)
                    {
                        spec.RefreshDuration();
                        ReapplyModifiers(spec);
                    }
                    else
                    {
                        RemoveGameplayEffect(spec, i);
                    }
                    break;

                case E_EffectExpiration.RefreshDuration:
                    spec.RefreshDuration();
                    break;
            }
        }

        /// <summary>
        /// 移除GE
        /// </summary>
        private void RemoveGameplayEffect(GameplayEffectRuntime spec, int index)
        {
            appliedEffectList.RemoveAt(index);
            ApplyModifiersToStat(spec, isRemove: true);
            RemoveGrantedTags(spec.GEData);
        }

        #endregion

        #region 标签

        /// <summary>
        /// 是否满足标签条件
        /// </summary>
        private bool CanApplyGE(GameplayEffectData effectData)
        {
            if (owner is null) return true;
            return owner.SatisfiesTagRequirements(effectData.RequiredNeedTags, effectData.RequiredBanTags);
        }

        /// <summary>
        /// 添加授予标签
        /// </summary>
        private void AddGrantedTags(GameplayEffectData effectData)
        {
            if (owner is null) return;

            foreach (string tagName in effectData.GameplayTags)
            {
                if (string.IsNullOrWhiteSpace(tagName)) continue;
                gameplayTagCountDict.TryGetValue(tagName, out int count);
                gameplayTagCountDict[tagName] = count + 1;
                if (count == 0)
                    owner.AddGameplayTag(tagName);
            }
        }

        /// <summary>
        /// 移除授予标签
        /// </summary>
        private void RemoveGrantedTags(GameplayEffectData effectData)
        {
            if (owner is null) return;

            foreach (string tagName in effectData.GameplayTags)
            {
                if (string.IsNullOrWhiteSpace(tagName)) continue;
                if (!gameplayTagCountDict.TryGetValue(tagName, out int count)) continue;

                count--;
                if (count <= 0)
                {
                    gameplayTagCountDict.Remove(tagName);
                    owner.RemoveGameplayTag(tagName);
                }
                else
                {
                    gameplayTagCountDict[tagName] = count;
                }
            }
        }

        #endregion

        #region 修饰符

        /// <summary>
        /// 应用或移除修饰符
        /// 修饰符来源用 Runtime 实例本身 避免各层独立同 Source 时互删
        /// </summary>
        private void ApplyModifiersToStat(GameplayEffectRuntime spec, bool isRemove = false)
        {
            var configList = spec.GEData.StatModifierConfig;

            for (int i = 0; i < configList.Count; i++)
            {
                var config = configList[i];

                if (isRemove)
                {
                    if (statController.GetImStat(config.statId) is not null)
                        continue;
                    statController.RemoveModifiersFromSource(config.statId, spec);
                    continue;
                }

                float finalValue = config.value * spec.StackCount;
                var imStat = statController.GetImStat(config.statId);
                if (imStat != null)
                {
                    //周期GE的即时属性改值交给周期跳伤
                    if (spec.GEData.IsPeriodic) continue;
                    imStat.ChangeValue(finalValue, config.type);
                }
                else
                {
                    var modifier = new StatModifier(
                        string.Empty,
                        config.type,
                        finalValue,
                        spec,
                        config.priority);
                    statController.AddModifier(config.statId, modifier);
                }
            }
        }

        #endregion
    }
}
