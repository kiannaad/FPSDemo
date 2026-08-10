using CGame.Animation;
using CGame.Ability.Animation;
using System;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterAnimationComponent :
        IComponent,
        IAbilityAnimationPlayer
    {
        private readonly Animator animator;
        private readonly CharacterPhysicsMotor motor;
        private readonly CharacterAnimationConfig animationConfig;
        private CharacterAnimInstance animInstance;
        private TickFunctionHandle preAnimationTick;
        private TickFunctionHandle postAnimationTick;

        public CharacterAnimationComponent(
            Animator animator,
            CharacterPhysicsMotor motor,
            CharacterAnimationConfig animationConfig)
        {
            this.animator =
                animator
                ?? throw new System.ArgumentNullException(nameof(animator));
            this.motor =
                motor
                ?? throw new System.ArgumentNullException(nameof(motor));
            this.animationConfig =
                animationConfig
                ?? throw new System.ArgumentNullException(
                    nameof(animationConfig));
        }

        public int Priority => 10;
        public event Action Updated;

        public void InitializingComponent(Pawn pawn)
        {
            animInstance?.Dispose();
            animInstance = new CharacterAnimInstance(
                pawn,
                new AnimationCharacterSource(motor),
                animator,
                animationConfig.UpperBodyMask);
            TickScheduler scheduler = World.Current?.TickScheduler;
            if (scheduler != null)
            {
                preAnimationTick = scheduler.Register(
                    $"CharacterAnimation.Pre:{animator.GetInstanceID()}",
                    TickGroup.TG_PreAnimation,
                    UpdatePreAnimation);
                postAnimationTick = scheduler.Register(
                    $"CharacterAnimation.Post:{animator.GetInstanceID()}",
                    TickGroup.TG_PostAnimation,
                    DispatchPostAnimation);
            }
        }

        public void UpdatingComponent(float elapseSeconds)
        {
            if (preAnimationTick == null)
            {
                UpdatePreAnimation(elapseSeconds);
            }
        }

        public void FixedUpdatingComponent(float elapseSeconds)
        {
        }

        public void LateUpdatingComponent(float elapseSeconds)
        {
            if (postAnimationTick == null)
            {
                DispatchPostAnimation(elapseSeconds);
            }
        }

        public void ShuttingDownComponent()
        {
            postAnimationTick?.Dispose();
            postAnimationTick = null;
            preAnimationTick?.Dispose();
            preAnimationTick = null;
            animInstance?.Dispose();
            animInstance = null;
            Updated = null;
        }

        private void UpdatePreAnimation(float deltaTime)
        {
            animInstance?.UpdateAnimation(deltaTime);
            Updated?.Invoke();
        }

        private void DispatchPostAnimation(float deltaTime)
        {
            animInstance?.DispatchAnimationNotifies();
        }

        public IAbilityAnimationPlayback PlayAnimation(
            AnimationClipAsset asset,
            long requestId)
        {
            AnimationPlaybackHandle handle =
                animInstance?.PlayAbilityAnimation(asset, requestId);
            return new AbilityPlayback(handle);
        }

        public bool StopAnimation(IAbilityAnimationPlayback playback)
        {
            return playback is AbilityPlayback adapter
                && adapter.Handle != null
                && animInstance != null
                && animInstance.StopAbilityAnimation(adapter.Handle);
        }

        private sealed class AbilityPlayback : IAbilityAnimationPlayback
        {
            public AbilityPlayback(AnimationPlaybackHandle handle)
            {
                Handle = handle;
            }

            public AnimationPlaybackHandle Handle { get; }
            public AbilityAnimationPlaybackState State => MapState(Handle);
            public bool IsTerminal => Handle == null || Handle.IsTerminal;

            private static AbilityAnimationPlaybackState MapState(
                AnimationPlaybackHandle handle)
            {
                if (handle == null || handle.State == AnimationPlaybackState.Failed)
                {
                    return AbilityAnimationPlaybackState.Failed;
                }

                switch (handle.State)
                {
                    case AnimationPlaybackState.Completed:
                        return AbilityAnimationPlaybackState.Completed;
                    case AnimationPlaybackState.Interrupted:
                        return AbilityAnimationPlaybackState.Interrupted;
                    case AnimationPlaybackState.Cancelled:
                        return AbilityAnimationPlaybackState.Cancelled;
                    case AnimationPlaybackState.Pending:
                    case AnimationPlaybackState.BlendingIn:
                        return AbilityAnimationPlaybackState.Pending;
                    default:
                        return AbilityAnimationPlaybackState.Playing;
                }
            }
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
