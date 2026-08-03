using System;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterAnimInstance : IDisposable
    {
        private readonly AnimationUpdateContext updateContext;
        private readonly CharacterAnimatorController animatorController;
        private readonly CharacterPlayablesController playablesController;
        private readonly CharacterBoneController boneController;
        private CharacterWeaponPresentationController weaponPresentationController;
        private bool isDisposed;

        public CharacterAnimInstance(
            Pawn pawn,
            IAnimationCharacterSource source,
            Animator animator,
            AvatarMask upperBodyMask = null)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (animator == null) throw new ArgumentNullException(nameof(animator));

            updateContext = new AnimationUpdateContext(source);
            animatorController = new CharacterAnimatorController(animator, updateContext);
            playablesController = new CharacterPlayablesController(
                pawn,
                animator,
                upperBodyMask);
            boneController = new CharacterBoneController(animator);
        }

        public AnimationUpdateContext UpdateContext => updateContext;
        internal CharacterAnimatorController AnimatorController => animatorController;
        internal CharacterPlayablesController PlayablesController => playablesController;
        public void UpdateAnimation(float deltaTime)
        {
            if (isDisposed || deltaTime <= 0f)
            {
                return;
            }

            updateContext.Update(deltaTime);
            if (!animatorController.IsValid())
            {
                animatorController.TryBind();
            }

            if (!animatorController.IsValid())
            {
                playablesController.RestoreNativeOutput();
                return;
            }

            animatorController.UpdateParameters(deltaTime);
            if (!playablesController.IsValid() && !playablesController.TryRebuild())
            {
                return;
            }

            playablesController.Update(deltaTime);
            weaponPresentationController?.Update(deltaTime);
            if (boneController.IsValid())
            {
                boneController.Update(deltaTime);
            }
        }

        public void MarkDiscontinuity()
        {
            updateContext.MarkDiscontinuity();
        }

        public AnimationPlaybackHandle PlayAbilityAnimation(
            AnimationClipAsset asset,
            long requestId)
        {
            return isDisposed
                ? null
                : playablesController.PlayAnimation(asset, requestId);
        }

        public bool StopAbilityAnimation(AnimationPlaybackHandle handle)
        {
            return !isDisposed && playablesController.Stop(handle);
        }

        public bool ConfigureWeaponPresentation(
            WeaponAnimationDefinition definition,
            WeaponRuntime runtime,
            uint generation)
        {
            if (isDisposed || definition == null || runtime == null)
            {
                return false;
            }

            weaponPresentationController ??=
                new CharacterWeaponPresentationController(animatorController.Animator);
            weaponPresentationController.BindRuntime(runtime);
            return weaponPresentationController.TryEquip(definition, generation);
        }

        public CharacterWeaponPresentationReplacement
            PrepareWeaponPresentationReplacement(
                WeaponAnimationDefinition definition,
                uint generation)
        {
            return isDisposed
                ? null
                : weaponPresentationController?.PrepareReplacement(
                    definition,
                    generation);
        }

        public AnimationPlaybackHandle PrepareInitialPose(
            AnimationClipAsset overlayPose,
            long requestId = 0)
        {
            if (isDisposed)
            {
                return null;
            }

            return playablesController.PlayPoseImmediate(
                overlayPose,
                requestId);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            boneController.Dispose();
            weaponPresentationController?.Dispose();
            weaponPresentationController = null;
            playablesController.Dispose();
            updateContext.Reset();
            isDisposed = true;
        }

    }
}
