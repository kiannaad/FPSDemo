using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterAnimInstance : IDisposable
    {
        private readonly AnimationUpdateContext updateContext;
        private readonly CharacterAnimatorController animatorController;
        private readonly CharacterPlayablesController playablesController;
        private readonly CharacterBoneController boneController;
        private bool isDisposed;

        public CharacterAnimInstance(
            Pawn pawn,
            IAnimationCharacterSource source,
            Animator animator,
            KRigComponent rigComponent,
            AvatarMask upperBodyMask = null)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (rigComponent == null) throw new ArgumentNullException(nameof(rigComponent));

            updateContext = new AnimationUpdateContext(source);
            animatorController = new CharacterAnimatorController(animator, updateContext);
            playablesController = new CharacterPlayablesController(
                pawn,
                animator,
                upperBodyMask);
            boneController = new CharacterBoneController(
                animator,
                rigComponent,
                updateContext);
        }

        public AnimationUpdateContext UpdateContext => updateContext;
        public CharacterBoneController BoneController => boneController;
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
                boneController.ReleaseOutput();
                playablesController.RestoreNativeOutput();
                return;
            }

            animatorController.UpdateParameters(deltaTime);
            if (!playablesController.IsValid())
            {
                boneController.ReleaseOutput();
                if (!playablesController.TryRebuild())
                {
                    return;
                }
            }

            playablesController.Update(deltaTime);
            if (!boneController.IsValid() && !boneController.TryRebuild())
            {
                return;
            }

            boneController.Update(deltaTime);
        }

        public void DispatchAnimationNotifies()
        {
            if (!isDisposed)
            {
                boneController.PostAnimationUpdate();
                playablesController.DispatchNotifies();
            }
        }

        public void MarkDiscontinuity()
        {
            updateContext.MarkDiscontinuity();
        }

        public bool TryRebuildGraph()
        {
            if (isDisposed)
            {
                return false;
            }

            boneController.ReleaseOutput();
            return playablesController.TryRebuild()
                && boneController.TryRebuild();
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
            playablesController.Dispose();
            updateContext.Reset();
            isDisposed = true;
        }

    }
}
