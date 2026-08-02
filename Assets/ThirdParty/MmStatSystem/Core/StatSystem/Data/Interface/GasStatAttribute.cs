using System;
using UnityEngine;

namespace GAS.StateSystem
{
    /// <summary>
    /// 仅编辑器标记 告诉 Inspector 这个 string 是 StatData 的名字(StatId)
    /// 运行时无逻辑 仍是普通字符串 下拉列表由 Editor/Stat 的 Drawer 绘制
    /// ImmediateOnly 为 true 时只显示即时属性 例如技能消耗只能选 HP/MP
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GasStatAttribute : PropertyAttribute
    {
        /// <summary> 是否只列出即时属性 </summary>
        public bool ImmediateOnly;

        /// <summary> 是否只列出被动属性 </summary>
        public bool PassiveOnly;
    }
}
