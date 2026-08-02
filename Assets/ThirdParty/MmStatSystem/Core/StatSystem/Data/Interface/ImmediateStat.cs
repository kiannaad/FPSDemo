using System;
using UnityEngine;

namespace GAS.StateSystem
{
    /// <summary>
    /// 即时属性
    /// HP、MP 等直接变化的资源属性
    /// </summary>
    [System.Serializable]
    public class ImmediateStat : IImmediateStat
    {
        //控制器
        private readonly StatController controller;
        public StatController Controller => controller;

        //原值
        private readonly float baseValue;
        public float BaseValue => baseValue;

        //最小/最大值
        private readonly float minValue;
        private readonly float maxValue;

        //当前值
        private float currentValue;
        public float CurrentValue => currentValue;
        public float MaxValue => maxValue;

        //当前值变化事件
        public event Action CurValueChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ImmediateStat(StatData definition, StatController controller)
        {
            this.controller = controller;
            this.baseValue = definition.BaseValue;
            this.minValue = definition.MinValue;
            this.maxValue = definition.MaxValue;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Initialize()
        {
            currentValue = baseValue;
            CurValueChanged?.Invoke();
        }

        /// <summary>
        /// 瞬时变化（直接修改当前值）
        /// </summary>
        public virtual void ChangeValue(float magnitude, E_ModifierType modifierType)
        {
            float newValue = currentValue;

            switch (modifierType)
            {
                case E_ModifierType.FlatAdd:
                    newValue += magnitude;
                    break;
                case E_ModifierType.PercentageAdd:
                    newValue *= (1 + magnitude / 100f);
                    break;
                case E_ModifierType.FinalAdd:
                    newValue += magnitude;
                    break;
                case E_ModifierType.FinalPercentage:
                    newValue *= (1 + magnitude / 100f);
                    break;
            }
            currentValue = Mathf.Clamp(newValue, minValue, maxValue);
            CurValueChanged?.Invoke();
        }

        /// <summary>
        /// 将当前值恢复到基础值
        /// </summary>
        public virtual void Restore()
        {
            currentValue = baseValue;
            CurValueChanged?.Invoke();
        }
    }
}
