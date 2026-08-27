using System.Threading;
using GAS.Component;
using GAS.StateSystem;

namespace GAS.AbilitySystem
{
    /// <summary>
    /// 技能运行时上下文
    /// </summary>
    public class AbilityContext
    {
        /// <summary>
        /// 技能实例
        /// </summary>
        public AbilityRuntime Spec { get; }

        /// <summary>
        /// 技能配置
        /// </summary>
        public GameplayAbilityData Ability => Spec?.ability;

        /// <summary>
        /// 施法者能力组件
        /// </summary>
        public AbilitySystemMgr Owner { get; }

        /// <summary>
        /// 施法者属性组件
        /// </summary>
        public StatController StatController { get; }

        /// <summary>
        /// 取消令牌
        /// </summary>
        public CancellationToken CancellationToken => Owner?.GetCancellationTokenOnDestroy() ?? default;

        /// <summary>
        /// 是否已经取消
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// 技能运行时上下文
        /// </summary>
        public AbilityContext(AbilityRuntime spec, AbilitySystemMgr owner, StatController statController)
        {
            Spec = spec;
            Owner = owner;
            StatController = statController;
        }

        /// <summary>
        /// 取消当前施法上下文
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
        }
    }
}
