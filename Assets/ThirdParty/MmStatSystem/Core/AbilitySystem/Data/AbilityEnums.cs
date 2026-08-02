using Sirenix.OdinInspector;

namespace GAS.AbilitySystem
{
    /// <summary>
    /// 技能消耗类型
    /// </summary>
    public enum E_CostType
    {
        [LabelText("固定值")] Fixed = 0,
        [LabelText("最大属性百分比")] PercentMax = 1,
        [LabelText("当前属性百分比")] PercentCurrent = 2
    }
}
