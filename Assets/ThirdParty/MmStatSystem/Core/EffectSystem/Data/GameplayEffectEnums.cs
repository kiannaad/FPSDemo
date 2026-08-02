using Sirenix.OdinInspector;

namespace GAS.Core.GameplayEffect
{
    /// <summary>
    /// 时间策略
    /// </summary>
    public enum E_EffectDuration
    {
        [LabelText("即时")] Instant = 0,
        [LabelText("持续")] HasDuration = 1,
        [LabelText("永久")] Infinite = 2
    }

    /// <summary>
    /// 层数怎么存 共享一份Runtime还是每层一份
    /// </summary>
    public enum E_EffectStackAggregation
    {
        [LabelText("共享层数")] SharedCount = 0,
        [LabelText("各层独立")] IndependentInstances = 1
    }

    /// <summary>
    /// 再应用时是否刷新时长 管Buff还活多久
    /// </summary>
    public enum E_EffectDurationRefresh
    {
        [LabelText("不刷新时长")] NeverRefresh = 0,
        [LabelText("刷新时长")] RefreshOnSuccessfulApplication = 1
    }

    /// <summary>
    /// 再应用时是否重置周期 管下次跳伤倒计时
    /// </summary>
    public enum E_EffectPeriodReset
    {
        [LabelText("不重置周期")] NeverReset = 0,
        [LabelText("重置周期")] ResetOnSuccessfulApplication = 1
    }

    /// <summary>
    /// 到期处理
    /// </summary>
    public enum E_EffectExpiration
    {
        [LabelText("清除全部层数")] ClearEntireStack = 0,
        [LabelText("减一层并刷新时长")] RemoveSingleStackAndRefreshDuration = 1,
        [LabelText("只刷新时长")] RefreshDuration = 2
    }
}
