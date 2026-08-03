using CGame.Animation;
using CGame.Ability.Animation;
using System;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterAnimationComponent :
        IComponent,
        IWeaponSwitchPresentation
    {
        private readonly Animator animator;
        private readonly CharacterPhysicsMotor motor;
        private readonly CharacterAnimationConfig animationConfig;
        private CharacterAnimInstance animInstance;
        private AnimationPlaybackHandle initialWeaponPoseHandle;

        public CharacterAnimationComponent(
            Animator animator,
            CharacterPhysicsMotor motor,
            CharacterAnimationConfig animationConfig,
            WeaponAnimationDefinition initialWeaponDefinition)
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
            if (initialWeaponDefinition == null)
            {
                throw new System.ArgumentNullException(
                    nameof(initialWeaponDefinition));
            }
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
        }

        public void UpdatingComponent(float elapseSeconds)
        {
            animInstance?.UpdateAnimation(elapseSeconds);
            Updated?.Invoke();
        }

        public void FixedUpdatingComponent(float elapseSeconds)
        {
        }

        public void LateUpdatingComponent(float elapseSeconds)
        {
        }

        public void ShuttingDownComponent()
        {
            animInstance?.Dispose();
            animInstance = null;
            initialWeaponPoseHandle = null;
            Updated = null;
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

        public IAbilityAnimationPlayback PlayPose(
            AnimationClipAsset asset,
            long requestId)
        {
            AnimationPlaybackHandle handle =
                animInstance?.PrepareInitialPose(asset, requestId);
            return new AbilityPlayback(handle);
        }

        public IWeaponSwitchPresentationReplacement PrepareReplacement(
            WeaponAnimationDefinition definition,
            uint generation)
        {
            CharacterWeaponPresentationReplacement replacement =
                animInstance?.PrepareWeaponPresentationReplacement(
                    definition,
                    generation);
            return replacement == null
                ? null
                : new SwitchPresentationReplacement(replacement);
        }

        public void PromotePose(IAbilityAnimationPlayback playback)
        {
            if (!(playback is AbilityPlayback adapter)
                || adapter.Handle == null
                || adapter.IsTerminal)
            {
                return;
            }

            if (initialWeaponPoseHandle != null
                && !ReferenceEquals(initialWeaponPoseHandle, adapter.Handle)
                && !initialWeaponPoseHandle.IsTerminal)
            {
                animInstance?.StopAbilityAnimation(initialWeaponPoseHandle);
            }

            initialWeaponPoseHandle = adapter.Handle;
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

        private sealed class SwitchPresentationReplacement :
            IWeaponSwitchPresentationReplacement
        {
            private CharacterWeaponPresentationReplacement replacement;

            public SwitchPresentationReplacement(
                CharacterWeaponPresentationReplacement replacement)
            {
                this.replacement = replacement;
            }

            public bool IsValid => replacement != null && replacement.IsValid;

            public void Commit()
            {
                CharacterWeaponPresentationReplacement current = replacement;
                if (current == null)
                {
                    throw new InvalidOperationException(
                        "The weapon presentation replacement has already ended.");
                }

                current.Commit();
                replacement = null;
            }

            public void Dispose()
            {
                CharacterWeaponPresentationReplacement current = replacement;
                replacement = null;
                current?.Dispose();
            }
        }

        public AnimationPlaybackHandle PrepareInitialWeaponPose(
            AnimationClipAsset overlayPose,
            long requestId = 0)
        {
            initialWeaponPoseHandle =
                animInstance?.PrepareInitialPose(
                    overlayPose,
                    requestId);
            return initialWeaponPoseHandle;
        }

        internal bool ConfigureWeaponPresentation(
            WeaponAnimationDefinition definition,
            WeaponRuntime runtime,
            uint generation)
        {
            return animInstance != null
                && animInstance.ConfigureWeaponPresentation(
                    definition,
                    runtime,
                    generation);
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
