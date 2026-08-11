using System;
using CGame.Animation;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame
{
    public sealed class PawnAnimationComponent : ActorComponent
    {
        private readonly Animator animator;
        private readonly CharacterPhysicsMotor motor;
        private readonly CharacterAnimationConfig animationConfig;
        private readonly KRigComponent rigComponent;
        private CharacterAnimInstance animInstance;

        public PawnAnimationComponent(
            Animator animator,
            CharacterPhysicsMotor motor,
            CharacterAnimationConfig animationConfig,
            KRigComponent rigComponent)
        {
            this.animator = animator;
            this.motor = motor;
            this.animationConfig = animationConfig;
            this.rigComponent = rigComponent;
            AddDependency<PawnMovementComponent>();
        }

        public Animator Animator => animator;

        public CharacterAnimInstance AnimInstance => animInstance;

        public KRigComponent RigComponent => rigComponent;

        public int PreAnimationTickCount { get; private set; }

        public int PostAnimationTickCount { get; private set; }

        protected override void OnInitialize()
        {
            if (!(Owner is Pawn pawn))
            {
                throw new InvalidOperationException("PawnAnimationComponent requires a Pawn owner.");
            }

            if (animator != null && motor != null && animationConfig != null && rigComponent != null)
            {
                animInstance = new CharacterAnimInstance(
                    pawn,
                    new AnimationCharacterSource(motor),
                    animator,
                    rigComponent,
                    animationConfig.UpperBodyMask);
            }

            AddTickTask("Pawn.Animation.Pre", TickGroup.TG_PreAnimation, UpdatePreAnimation);
            AddTickTask("Pawn.Animation.Post", TickGroup.TG_PostAnimation, DispatchPostAnimation);
        }

        protected override void OnShutdown()
        {
            animInstance?.Dispose();
            animInstance = null;
        }

        private void UpdatePreAnimation(float deltaTime)
        {
            PreAnimationTickCount++;
            animInstance?.UpdateAnimation(deltaTime);
        }

        private void DispatchPostAnimation(float deltaTime)
        {
            PostAnimationTickCount++;
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
