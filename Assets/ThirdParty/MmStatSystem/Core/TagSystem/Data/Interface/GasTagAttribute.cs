using System;
using UnityEngine;

namespace GAS.TagSystem
{
    /// <summary>
    /// 标记该字符串字段为 GAS 标签 具体下拉绘制由 Editor 负责
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GasTagAttribute : PropertyAttribute
    {
    }
}
