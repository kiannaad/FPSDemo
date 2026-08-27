using System;
using System.Collections.Generic;
using GAS.Component;
using GAS.Core.GameplayEffect;
using GAS.StateSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GAS.AbilitySystem
{
    /// <summary>
    /// 游戏技能基类
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(fileName = "NewAbility", menuName = "GAS/GameplayAbility")]
    public class GameplayAbilityData : ScriptableObject
    {
        [LabelText("技能图标")]
        public Sprite icon;

        [LabelText("技能名称")]
        public string abilityName;

        [TextArea]
        [LabelText("技能描述")]
        public string description;

        [LabelText("冷却时间")]
        public float cooldownTime = 1f;

        [Header("消耗")]
        [LabelText("消耗属性ID")]
        //GasStat 只负责让 Inspector 弹出 Stat 下拉 ImmediateOnly 限制只能选即时属性
        [GasStat(ImmediateOnly = true)]
        public string costStatId;

        [LabelText("消耗类型")]
        public E_CostType costType;

        [LabelText("消耗数值")]
        public float costValue;

        [Header("激活效果")]
        [LabelText("激活时应用的GE")]
        [SerializeField]
        private List<GameplayEffectData> applyEffectList = new List<GameplayEffectData>();

        [Header("标签条件")]
        [HideInInspector]
        [SerializeField]
        private string[] activationRequiredTags = Array.Empty<string>();

        [HideInInspector]
        [SerializeField]
        private string[] activationBlockedTags = Array.Empty<string>();

        /// <summary>
        /// 激活需要标签
        /// </summary>
        public string[] ActivationRequiredTags => activationRequiredTags;

        /// <summary>
        /// 激活禁止标签
        /// </summary>
        public string[] ActivationBlockedTags => activationBlockedTags;

        /// <summary>
        /// 激活时应用的 GE 列表
        /// </summary>
        public List<GameplayEffectData> ApplyEffectList => applyEffectList;

        /// <summary>
        /// 激活技能
        /// </summary>
        public virtual void Activate(StatController statController)
        {
            Debug.Log($"激活技能: {abilityName}");
        }

        /// <summary>
        /// 激活技能 默认把配置的 GE 挂到 Owner
        /// </summary>
        public virtual void Activate(AbilityContext context)
        {
            Debug.Log($"激活技能: {abilityName}");
            ApplyConfiguredEffects(context);
        }

        /// <summary>
        /// 应用配置的 GE 列表
        /// </summary>
        protected void ApplyConfiguredEffects(AbilityContext context)
        {
            if (context?.Owner == null) return;
            if (applyEffectList == null || applyEffectList.Count == 0) return;

            for (int i = 0; i < applyEffectList.Count; i++)
            {
                GameplayEffectData effectData = applyEffectList[i];
                if (effectData == null) continue;
                context.Owner.ApplyGE(effectData, context);
            }
        }


        /// <summary>
        /// 中断技能
        /// </summary>
        public virtual void InterruptTask()
        {
            Debug.Log($"中断技能: {abilityName}");
        }

        /// <summary>
        /// 中断技能
        /// </summary>
        public virtual void InterruptTask(AbilityContext context)
        {
            context?.Cancel();
            InterruptTask();
        }

        public AbilityRuntime CreateAbilitySpec(){
            
            AbilityRuntime abilitySpec = new AbilityRuntime(){
                ability = this,
                cooldown = new AbilityCooldown { cooldownTime = this.cooldownTime },
                cost = new AbilityCost(){
                    statId = this.costStatId,
                    costType = this.costType,
                    value = this.costValue
                }
            };
            return abilitySpec;
        }
    }
}
