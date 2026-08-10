using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    public class Pawn : ICharacterIntentSink, ICharacterMovementCommandSource
    {
        private readonly List<IComponent> components = new List<IComponent>();
        private IController controller;
        private Vector3 movementInput;
        private Vector3 pendingForce;
        private Vector3 pendingImpulse;
        private bool pendingJump;
        private bool sprintRequested;

        public Controller Controller => controller as Controller;
        public IController OwningController => controller;
        public AbilitySystemComponent AbilitySystem { get; private set; }
        public PawnHost Host { get; private set; }
        public Quaternion ControlRotation { get; private set; } = Quaternion.identity;

        /// <summary>
        /// 绑定 Unity 宿主对象。
        /// </summary>
        public virtual void BindingHost(PawnHost host)
        {
            Host = host;
        }

        /// <summary>
        /// 解除 Unity 宿主对象绑定。
        /// </summary>
        public virtual void UnbindingHost(PawnHost host)
        {
            if (Host == host)
            {
                Host = null;
            }
        }

        /// <summary>
        /// 设置当前控制器。
        /// </summary>
        public virtual void SettingController(IController controller)
        {
            this.controller = controller;
        }

        /// <summary>
        /// 清理当前控制器。
        /// </summary>
        public virtual void ClearingController(IController controller)
        {
            if (this.controller == controller)
            {
                this.controller = null;
            }
        }

        public void BindingAbilitySystem(AbilitySystemComponent abilitySystem)
        {
            if (abilitySystem == null)
            {
                throw new System.ArgumentNullException(nameof(abilitySystem));
            }

            if (AbilitySystem != null && !ReferenceEquals(AbilitySystem, abilitySystem))
            {
                throw new System.InvalidOperationException("Pawn is already bound to another AbilitySystemComponent.");
            }

            AbilitySystem = abilitySystem;
        }

        public void ClearingAbilitySystem(AbilitySystemComponent abilitySystem)
        {
            if (ReferenceEquals(AbilitySystem, abilitySystem))
            {
                AbilitySystem = null;
            }
        }

        /// <summary>
        /// 应用控制器传入的控制旋转。
        /// </summary>
        public virtual void ApplyingControlRotation(Quaternion controlRotation)
        {
            ControlRotation = controlRotation;
        }

        /// <summary>
        /// 提交一次控制意图。移动是最新持续值，跳跃是一次性请求。
        /// </summary>
        public void SubmitControlIntent(in CharacterControlIntent intent)
        {
            movementInput = intent.MovementInput;
            pendingJump |= intent.JumpRequested;
            sprintRequested = intent.SprintRequested;
        }

        /// <summary>
        /// 消费当前物理步命令。连续移动保留到下一物理步，跳跃只交付一次。
        /// </summary>
        public CharacterMovementCommand ConsumeMovementCommand()
        {
            bool jumpRequested = pendingJump;
            pendingJump = false;
            return new CharacterMovementCommand(movementInput, jumpRequested, sprintRequested);
        }

        /// <summary>
        /// 查看当前持续移动意图，不会消费它。
        /// </summary>
        public Vector3 PeekingMovementInput()
        {
            return movementInput;
        }

        /// <summary>
        /// 清空控制意图，供换控或销毁时消除残留命令。
        /// </summary>
        public void ClearingControlIntent()
        {
            movementInput = Vector3.zero;
            pendingJump = false;
            sprintRequested = false;
        }

        public void AddingForce(Vector3 force)
        {
            pendingForce += force;
        }

        public Vector3 ConsumingForce()
        {
            Vector3 force = pendingForce;
            pendingForce = Vector3.zero;
            return force;
        }

        public void AddingImpulse(Vector3 impulse)
        {
            pendingImpulse += impulse;
        }

        public Vector3 ConsumingImpulse()
        {
            Vector3 impulse = pendingImpulse;
            pendingImpulse = Vector3.zero;
            return impulse;
        }

        public void AddingJumpInput()
        {
            SubmitControlIntent(new CharacterControlIntent(movementInput, true, sprintRequested));
        }

        /// <summary>
        /// 注册 Pawn 自管理组件。
        /// </summary>
        public void RegisteringComponent(IComponent component)
        {
            if (component == null || components.Contains(component))
            {
                return;
            }

            components.Add(component);
            SortingComponents();
            component.InitializingComponent(this);
        }

        /// <summary>
        /// 注销 Pawn 自管理组件。
        /// </summary>
        public void UnregisteringComponent(IComponent component)
        {
            if (!components.Remove(component))
            {
                return;
            }

            component.ShuttingDownComponent();
        }

        /// <summary>
        /// 更新 Pawn 普通帧逻辑。
        /// </summary>
        public virtual void UpdatingPawn(float elapseSeconds)
        {
            for (int i = 0; i < components.Count; i++)
            {
                components[i].UpdatingComponent(elapseSeconds);
            }
        }

        /// <summary>
        /// 更新 Pawn 固定帧逻辑。
        /// </summary>
        public virtual void FixedUpdatingPawn(float elapseSeconds)
        {
            for (int i = 0; i < components.Count; i++)
            {
                components[i].FixedUpdatingComponent(elapseSeconds);
            }
        }

        /// <summary>
        /// 更新 Pawn 渲染后逻辑。
        /// </summary>
        public virtual void LateUpdatingPawn(float elapseSeconds)
        {
            for (int i = 0; i < components.Count; i++)
            {
                components[i].LateUpdatingComponent(elapseSeconds);
            }
        }

        /// <summary>
        /// 关闭 Pawn 并清理全部组件。
        /// </summary>
        public virtual void ShuttingDownPawn()
        {
            for (int i = 0; i < components.Count; i++)
            {
                components[i].ShuttingDownComponent();
            }

            components.Clear();
            controller = null;
            AbilitySystem = null;
            Host = null;
            movementInput = Vector3.zero;
            pendingForce = Vector3.zero;
            pendingImpulse = Vector3.zero;
            pendingJump = false;
        }

        /// <summary>
        /// 按组件优先级排序。
        /// </summary>
        private void SortingComponents()
        {
            components.Sort((left, right) => right.Priority.CompareTo(left.Priority));
        }
    }
}
