namespace GAS.StateSystem
{
    /// <summary>
    /// 修饰符类型
    /// </summary>
    public enum E_ModifierType
    {
        /// <summary>
        /// 加减
        /// </summary>
        FlatAdd = 0,
        /// <summary>
        /// 百分比
        /// </summary>
        PercentageAdd = 1,
        /// <summary>
        /// 最终加减
        /// </summary>
        FinalAdd = 2,
        /// <summary>
        /// 最终百分比
        /// </summary>
        FinalPercentage = 3,
    }

    /// <summary>
    /// 修饰符基类
    /// </summary>
    [System.Serializable]
    public class StatModifier
    {
        public string Id;
        public object Source;
        public float Value;
        public int Priority;
        public E_ModifierType eModifierType; 

        public StatModifier(){}

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">修饰符唯一标识</param>
        /// <param name="type">修饰符类型</param>
        /// <param name="value">修饰值</param>
        /// <param name="source">来源</param>
        /// <param name="priority">排序优先级</param>
        public StatModifier(string id,
                            E_ModifierType type,
                            float value,
                            object source = null,
                            int priority = 0)
        {
            Id = id;
            eModifierType = type;
            Value = value;
            Source = source;
            Priority = priority;
        }
    }

    // /// <summary>
    // /// 泛型修饰符
    // /// </summary>
    // [System.Serializable]
    // public class StatModifier<T> : StatModifier
    // {
    //     public new T Source { get; set; }

    //     /// <summary>
    //     /// 构造函数
    //     /// </summary>
    //     public StatModifier(T source, E_ModifierType type, float value, int priority = 0)
    //     {
    //         Id = System.Guid.NewGuid().ToString();
    //         eModifierType = type;
    //         Value = value;
    //         this.Source = source;
    //         Priority = priority;
    //     }

    //     public StatModifier(string id, T source, E_ModifierType type, float value, int priority = 0)
    //     {
    //         Id = id;
    //         eModifierType = type;
    //         Value = value;
    //         this.Source = source;
    //         Priority = priority;
    //     }

    //     public override string ToString() => Value.ToString();
    // }
}
