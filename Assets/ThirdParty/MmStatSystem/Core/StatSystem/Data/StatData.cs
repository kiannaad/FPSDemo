using Sirenix.OdinInspector;
using UnityEngine;

namespace GAS.StateSystem
{
    /// <summary>
    /// 属性类型
    /// </summary>
    public enum E_StatType
    {
        Passive,   
        Immediate, 
    }

    [CreateAssetMenu(fileName = "StatData", menuName = "GAS/StatDefinition", order = 0)]
    public class StatData : SerializedScriptableObject
    {
        [Header("属性类型")]
        [SerializeField] 
        private E_StatType statType = E_StatType.Passive;

        [Header("基础值")]
        [SerializeField] 
        private float baseValue = 100f;

        [SerializeField] 
        private float minValue = float.MinValue;
        [SerializeField] 
        private float maxValue = float.MaxValue;

        [SerializeField] 
        private bool resetCurrentValueOnPlay = true;

        public E_StatType StatType => statType;
        public float BaseValue => baseValue;
        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public bool ResetCurrentValueOnPlay => resetCurrentValueOnPlay;
    }
}
