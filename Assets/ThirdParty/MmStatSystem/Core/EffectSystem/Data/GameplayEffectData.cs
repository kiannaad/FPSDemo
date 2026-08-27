using System;
using System.Collections.Generic;
using GAS.StateSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GAS.Core.GameplayEffect
{
    /// <summary>
    /// GameplayEffect 配置蓝图
    /// </summary>
    [CreateAssetMenu(fileName = "GE_Data", menuName = "GAS/GameplayEffectData")]
    public class GameplayEffectData : SerializedScriptableObject
    {
        [HideInInspector]
        [SerializeField] private string[] gameplayTags = Array.Empty<string>();

        [HideInInspector]
        [SerializeField] private string[] requiredNeedTags = Array.Empty<string>();

        [HideInInspector]
        [SerializeField] private string[] requiredBanTags = Array.Empty<string>();

        [TitleGroup("时间")]
        [SerializeField, LabelText("时间策略")]
        private E_EffectDuration durationPolicy = E_EffectDuration.Instant;

        [TitleGroup("时间")]
        [ShowIf("@durationPolicy == E_EffectDuration.HasDuration")]
        [SerializeField, LabelText("持续时间")]
        private float duration = 5f;

        [TitleGroup("周期")]
        [SerializeField, LabelText("启用周期跳伤")]
        private bool isPeriodic = false;

        [TitleGroup("周期")]
        [ShowIf("isPeriodic")]
        [SerializeField, LabelText("周期间隔")]
        private float period = 1f;

        [TitleGroup("周期")]
        [ShowIf("isPeriodic")]
        [SerializeField, LabelText("应用时立即跳一次")]
        private bool executePeriodicEffectOnApplication = true;

        [TitleGroup("堆叠")]
        [InfoBox("共享层数 = 一份Runtime 层数乘伤害\n各层独立 = 每层一份Runtime 各自计时跳伤\n每次应用增加层数 = 共享时StackCount+N 独立时一次建N份")]
        [SerializeField, LabelText("层数存法")]
        private E_EffectStackAggregation stackAggregation = E_EffectStackAggregation.SharedCount;

        [TitleGroup("堆叠")]
        [SerializeField, LabelText("最大堆叠层数"), MinValue(1)]
        private int stackLimit = 1;

        [TitleGroup("堆叠")]
        [SerializeField, LabelText("每次应用增加层数"), MinValue(1)]
        private int stacksPerApplication = 1;

        [TitleGroup("再应用")]
        [InfoBox("刷新时长 = Buff还活多久 续命\n重置周期 = 下次跳伤倒计时 重排拍子")]
        [ShowIf("@durationPolicy == E_EffectDuration.HasDuration")]
        [SerializeField, LabelText("再应用时刷新时长")]
        private E_EffectDurationRefresh durationRefreshPolicy = E_EffectDurationRefresh.RefreshOnSuccessfulApplication;

        [TitleGroup("再应用")]
        [ShowIf("isPeriodic")]
        [SerializeField, LabelText("再应用时重置周期")]
        private E_EffectPeriodReset periodResetPolicy = E_EffectPeriodReset.ResetOnSuccessfulApplication;

        [TitleGroup("再应用")]
        [SerializeField, LabelText("到期处理")]
        private E_EffectExpiration expirationPolicy = E_EffectExpiration.ClearEntireStack;

        [TitleGroup("效果")]
        [SerializeField, LabelText("属性修饰符列表")]
        private List<StatModifierConfig> statModifierConfig = new();

        public string[] GameplayTags => gameplayTags;
        public string[] RequiredNeedTags => requiredNeedTags;
        public string[] RequiredBanTags => requiredBanTags;
        public E_EffectDuration Duration => durationPolicy;
        public E_EffectDurationRefresh DurationRefresh => durationRefreshPolicy;
        public E_EffectPeriodReset PeriodReset => periodResetPolicy;
        public E_EffectExpiration Expiration => expirationPolicy;
        public float DurationValue => duration;
        public float Period => period;
        public bool IsPeriodic => isPeriodic;
        public bool ExecutePeriodicEffectOnApplication => executePeriodicEffectOnApplication;
        public E_EffectStackAggregation StackAggregation => stackAggregation;
        public int StackLimit => stackLimit < 1 ? 1 : stackLimit;
        public int StacksPerApplication => stacksPerApplication < 1 ? 1 : stacksPerApplication;
        public List<StatModifierConfig> StatModifierConfig => statModifierConfig;
        public bool IsIndependentStack => stackAggregation == E_EffectStackAggregation.IndependentInstances;

        public bool IsInstant => durationPolicy == E_EffectDuration.Instant;
        public bool HasDuration => durationPolicy == E_EffectDuration.HasDuration;
        public bool IsInfinite => durationPolicy == E_EffectDuration.Infinite;
        public bool HasPeriod => isPeriodic;
    }

    /// <summary>
    /// 修饰符配置 与运行时StatModifier区分
    /// </summary>
    [Serializable]
    public class StatModifierConfig
    {
        [GasStat]
        public string statId;
        public E_ModifierType type;
        public float value;
        public int priority = 0;
    }
}
