using GAS.StateSystem;

namespace GAS.Core.GameplayEffect
{
    /// <summary>
    /// GameplayEffect 运行时实例
    /// </summary>
    public class GameplayEffectRuntime
    {
        /// <summary> GE配置 </summary>
        public GameplayEffectData GEData { get; }

        /// <summary> 来源 </summary>
        public object Source { get; }

        /// <summary> 剩余持续时间 </summary>
        public float RemainingDuration { get; set; }

        /// <summary> 剩余周期时间 </summary>
        public float RemainingPeriod { get; set; }

        /// <summary> 堆叠层数 </summary>
        public int StackCount { get; set; }

        /// <summary>
        /// 构造运行时实例
        /// </summary>
        public GameplayEffectRuntime(GameplayEffectData definition, object source)
        {
            GEData = definition;
            Source = source;

            if (definition.Duration == E_EffectDuration.HasDuration)
                RemainingDuration = definition.DurationValue;
            else if (definition.Duration == E_EffectDuration.Infinite)
                RemainingDuration = float.MaxValue;

            if (definition.IsPeriodic)
            {
                //应用时立即跳一次则把周期倒计时置0 等首帧Update触发
                if (definition.ExecutePeriodicEffectOnApplication)
                    RemainingPeriod = 0f;
                else
                    RemainingPeriod = definition.Period;
            }

            StackCount = 1;
        }

        /// <summary> 是否已经过期 </summary>
        public bool IsExpired =>
            GEData.Duration != E_EffectDuration.Infinite && RemainingDuration <= 0f;

        /// <summary> 是否到了触发周期效果的时间 </summary>
        public bool ShouldExecutePeriod =>
            GEData.IsPeriodic && RemainingPeriod <= 0f;

        /// <summary>
        /// 重置周期时间
        /// </summary>
        public void ResetPeriod()
        {
            if (GEData.IsPeriodic)
                RemainingPeriod = GEData.Period;
        }

        /// <summary>
        /// 刷新持续时间
        /// </summary>
        public void RefreshDuration()
        {
            if (GEData.HasDuration)
                RemainingDuration = GEData.DurationValue;
        }
    }
}
