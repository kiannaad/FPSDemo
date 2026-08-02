using System;

namespace GAS.StateSystem
{

    /// <summary>
    /// 即时属性接口 HP MP 等资源属性
    /// </summary>
    public interface IImmediateStat
    {
        /// <summary> 基础值 </summary>
        float BaseValue { get; }

        /// <summary> 当前值 </summary>
        float CurrentValue { get; }

        /// <summary> 最大值 </summary>
        float MaxValue { get; }

        /// <summary> 所属控制器 </summary>
        StatController Controller { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize();

        /// <summary>
        /// 瞬时变化
        /// </summary>
        void ChangeValue(float magnitude, E_ModifierType modifierType);

        /// <summary>
        /// 恢复到基础值
        /// </summary>
        void Restore();

        /// <summary> 当前值变化事件 </summary>
        event Action CurValueChanged;
    }
}
