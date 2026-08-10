using System;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    public sealed class PawnAnimationComponent : PawnFeatureComponent
    {
        private readonly Pawn pawn;
        private readonly Animator animator;
        private readonly CharacterPhysicsMotor motor;
        private readonly CharacterAnimationConfig animationConfig;
        private CharacterAnimInstance animInstance;
        private TickFunctionHandle preAnimationTick;
        private TickFunctionHandle postAnimationTick;

        public PawnAnimationComponent() : base("Animation")
        {
        }

        public PawnAnimationComponent(
            Pawn pawn,
            Animator animator,
            CharacterPhysicsMotor motor,
            CharacterAnimationConfig animationConfig) : base("Animation")
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.motor = motor ?? throw new ArgumentNullException(nameof(motor));
            this.animationConfig = animationConfig
                ?? throw new ArgumentNullException(nameof(animationConfig));
        }

        public Animator Animator => animator;

        public CharacterAnimInstance AnimInstance => animInstance;

        public override void EnterState(PawnInitState nextState, PawnInitContext context)
        {
            base.EnterState(nextState, context);
            if (nextState != PawnInitState.DataInitialized || animator == null)
            {
                return;
            }

            animInstance = new CharacterAnimInstance(
                pawn,
                new AnimationCharacterSource(motor),
                animator,
                animationConfig.UpperBodyMask);
            TickScheduler scheduler = World.Current?.TickScheduler;
            if (scheduler == null)
            {
                return;
            }

            preAnimationTick = scheduler.Register(
                $"PawnAnimation.Pre:{animator.GetInstanceID()}",
                TickGroup.TG_PreAnimation,
                UpdatePreAnimation);
            postAnimationTick = scheduler.Register(
                $"PawnAnimation.Post:{animator.GetInstanceID()}",
                TickGroup.TG_PostAnimation,
                DispatchPostAnimation);
        }

        public override void Shutdown()
        {
            postAnimationTick?.Dispose();
            postAnimationTick = null;
            preAnimationTick?.Dispose();
            preAnimationTick = null;
            animInstance?.Dispose();
            animInstance = null;
            base.Shutdown();
        }

        private void UpdatePreAnimation(float deltaTime)
        {
            animInstance?.UpdateAnimation(deltaTime);
        }

        private void DispatchPostAnimation(float deltaTime)
        {
            animInstance?.DispatchAnimationNotifies();
        }

        private sealed class AnimationCharacterSource : IAnimationCharacterSource
        {
            private readonly CharacterPhysicsMotor motor;

            public AnimationCharacterSource(CharacterPhysicsMotor motor)
            {
                this.motor = motor;
            }

            public Transform Transform => motor != null ? motor.transform : null;

            public Vector3 Velocity => motor != null ? motor.Velocity : Vector3.zero;

            public bool IsGrounded => motor != null && motor.GroundingStatus.IsStableOnGround;
        }
    }
}
