using GAS.Component;
using GAS.StateSystem;
using UnityEngine;

namespace GAS.AbilitySystem
{
    /// <summary>
    /// 技能实例
    /// </summary>
    public class AbilityRuntime
    {
        public GameplayAbilityData ability;
        public AbilityCooldown cooldown;
        public AbilityCost cost;

        /// <summary>
        /// 当前运行时上下文
        /// </summary>
        private AbilityContext activeContext;

        public AbilityRuntime(){
          
        }

        /// <summary>
        /// 检查是否能激活
        /// </summary>
        /// <param name="statController"></param>
        /// <returns></returns>
        public bool CanActivate(StatController statController){
            //1.CD
            if(cooldown is not null && cooldown.IsOncooldown){
                Debug.Log($"技能{ability.abilityName}在冷却中");
                return false;
            }
            //2.消耗 - 只有当有有效消耗时才检查
            if(cost is not null && !string.IsNullOrEmpty(cost.statId) && cost.CanApply(statController) is false){
                Debug.Log($"技能{ability.abilityName}消耗不足");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检查是否能激活
        /// </summary>
        public bool CanActivate(StatController statController, AbilitySystemMgr owner)
        {
            if (!CanActivate(statController)) return false;

            if (owner is not null && ability is not null)
            {
                bool canActivateByTags = owner.SatisfiesTagRequirements(
                    ability.ActivationRequiredTags,
                    ability.ActivationBlockedTags);

                if (!canActivateByTags)
                {
                    Debug.Log($"技能{ability.abilityName}标签条件不满足");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 激活技能
        /// </summary>
        /// <param name="statController"></param>
        public void Activate(StatController statController){
             cooldown?.StartCooldown(); //CD
             // 只有当有有效消耗时才应用
             if(cost is not null && !string.IsNullOrEmpty(cost.statId)){
                 cost.Apply(statController); //属性
             }
             ability?.Activate(statController); //效果(具体的技能效果自己写)
        }

        /// <summary>
        /// 激活技能
        /// </summary>
        public void Activate(AbilitySystemMgr owner, StatController statController)
        {
             cooldown?.StartCooldown(); //CD
             // 只有当有有效消耗时才应用
             if(cost is not null && !string.IsNullOrEmpty(cost.statId)){
                 cost.Apply(statController); //属性
             }

             activeContext = new AbilityContext(this, owner, statController);
             ability?.Activate(activeContext); //效果(具体的技能效果自己写)
        }

        /// <summary>
        /// 中断技能
        /// </summary>
        public void Interrupt()
        {
            ability?.InterruptTask(activeContext);
            activeContext = null;
        }
    }
}
