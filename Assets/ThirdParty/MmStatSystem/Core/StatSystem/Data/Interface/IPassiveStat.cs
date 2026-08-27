using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GAS.StateSystem
{
    /// <summary>
    /// 被动属性接口 攻击力防御力速度等面板属性
    /// </summary>
    public interface IPassiveStat
    {
        /// <summary> 基础值 </summary>
        float BaseValue { get; }

        /// <summary> 最终值 </summary>
        float FinalValue { get; }

        /// <summary> 所属控制器 </summary>
        StatController Controller { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize();

        /// <summary>
        /// 强制重算
        /// </summary>
        void ForceRecalculate();

        /// <summary>
        /// 添加修饰符
        /// </summary>
        void AddModifier(StatModifier modifier);

        /// <summary>
        /// 移除指定Id的修饰符
        /// </summary>
        bool RemoveModifier(string modifierId);

        /// <summary>
        /// 移除指定修饰符
        /// </summary>
        void RemoveModifier(StatModifier modifier);

        /// <summary>
        /// 移除所有来源的修饰符
        /// </summary>
        void RemoveModifiersFromSource(object source);

        /// <summary>
        /// 清空所有修饰符
        /// </summary>
        void ClearModifiers();

        /// <summary> 值变化事件 </summary>
        event Action OnValueChanged;
    }
}