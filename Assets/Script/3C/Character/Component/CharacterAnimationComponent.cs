using CGame.Animation;
using UnityEngine;
using YooAsset;

namespace CGame
{
    public sealed class CharacterAnimationComponent : IComponent
    {
        private readonly Animator animator;
        private readonly CharacterPhysicsMotor motor;
        private readonly CharacterAnimationConfig animationConfig;
        private readonly WeaponAnimationDefinition initialWeaponDefinition;
        private Pawn pawn;
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
            this.initialWeaponDefinition =
                initialWeaponDefinition
                ?? throw new System.ArgumentNullException(
                    nameof(initialWeaponDefinition));
        }

        public int Priority => 10;

        public void InitializingComponent(Pawn pawn)
        {
            this.pawn = pawn;
            animInstance?.Dispose();
            animInstance = new CharacterAnimInstance(
                pawn,
                new AnimationCharacterSource(motor),
                animator,
                animationConfig.UpperBodyMask);
        }

        public void UpdatingComponent(float elapseSeconds)
        {
            animInstance?.UpdateAnimation(
                elapseSeconds,
                pawn?.Controller?.WeaponRuntime);
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
            pawn = null;
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

        public void ConfigureWeaponRuntimeResources(
            IWeaponAnimationDefinitionLocationResolver locationResolver,
            AssetHandle initialDefinitionHandle)
        {
            if (animInstance == null)
            {
                throw new System.InvalidOperationException(
                    "Character animation must be initialized before weapon runtime resources are transferred.");
            }

            animInstance.ConfigureWeaponRuntimeResources(
                locationResolver,
                initialDefinitionHandle,
                initialWeaponPoseHandle);
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
