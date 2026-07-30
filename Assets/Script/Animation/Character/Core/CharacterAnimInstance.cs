using System;
using UnityEngine;
using YooAsset;

namespace CGame.Animation
{
    public sealed class CharacterAnimInstance : IDisposable
    {
        private readonly AnimationUpdateContext updateContext;
        private readonly CharacterAnimatorController animatorController;
        private readonly CharacterPlayablesController playablesController;
        private readonly CharacterBoneController boneController;
        private readonly CharacterWeaponAnimationAdapter weaponAdapter;
        private WeaponAnimationSequencer weaponSequencer;
        private bool isDisposed;

        public CharacterAnimInstance(
            IAnimationCharacterSource source,
            Animator animator,
            AvatarMask upperBodyMask = null,
            WeaponAnimationDefinition weaponDefinition = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (animator == null) throw new ArgumentNullException(nameof(animator));

            updateContext = new AnimationUpdateContext(source);
            animatorController = new CharacterAnimatorController(animator, updateContext);
            playablesController = new CharacterPlayablesController(animator, upperBodyMask);
            boneController = new CharacterBoneController(animator);
            weaponAdapter = new CharacterWeaponAnimationAdapter();
            if (weaponDefinition != null)
            {
                weaponSequencer = new WeaponAnimationSequencer(
                    playablesController,
                    weaponDefinition);
            }
        }

        public AnimationUpdateContext UpdateContext => updateContext;
        internal CharacterAnimatorController AnimatorController => animatorController;
        internal CharacterPlayablesController PlayablesController => playablesController;
        internal WeaponAnimationSequencer WeaponSequencer =>
            weaponSequencer;

        public void ConfigureWeaponRuntimeResources(
            IWeaponAnimationDefinitionLocationResolver locationResolver,
            AssetHandle initialDefinitionHandle,
            AnimationPlaybackHandle initialOverlayHandle)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(CharacterAnimInstance));
            }

            if (weaponSequencer != null)
            {
                throw new InvalidOperationException(
                    "Weapon runtime resources are already configured.");
            }

            weaponSequencer = new WeaponAnimationSequencer(
                playablesController,
                locationResolver,
                initialDefinitionHandle,
                initialOverlayHandle);
        }

        public void UpdateAnimation(
            float deltaTime,
            WeaponRuntime weaponRuntime = null)
        {
            if (isDisposed || deltaTime <= 0f)
            {
                return;
            }

            SynchronizeWeaponRuntime(weaponRuntime);
            updateContext.Update(deltaTime);
            if (!animatorController.IsValid())
            {
                animatorController.TryBind();
            }

            if (!animatorController.IsValid())
            {
                playablesController.RestoreNativeOutput();
                weaponSequencer?.Update();
                return;
            }

            animatorController.UpdateParameters(deltaTime);
            if (!playablesController.IsValid() && !playablesController.TryRebuild())
            {
                weaponSequencer?.Update();
                return;
            }

            playablesController.Update(deltaTime);
            weaponSequencer?.Update();
            if (boneController.IsValid())
            {
                boneController.Update(deltaTime);
            }
        }

        public void MarkDiscontinuity()
        {
            updateContext.MarkDiscontinuity();
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
            weaponSequencer?.Dispose();
            weaponAdapter?.Dispose();
            playablesController.Dispose();
            updateContext.Reset();
            isDisposed = true;
        }

        private void SynchronizeWeaponRuntime(WeaponRuntime runtime)
        {
            if (weaponAdapter == null || weaponSequencer == null)
            {
                return;
            }

            if (weaponAdapter.BoundRuntime != runtime)
            {
                weaponSequencer.BindRuntime(runtime);
                weaponAdapter.BindRuntime(runtime);
            }

            while (weaponAdapter.TryDequeueEvent(
                       out WeaponAnimationEvent animationEvent))
            {
                if (animationEvent.Kind
                    == WeaponAnimationEventKind.Action)
                {
                    weaponSequencer.Consume(
                        animationEvent.Action);
                }
                else if (animationEvent.Kind
                         == WeaponAnimationEventKind.Switch)
                {
                    weaponSequencer.Consume(
                        animationEvent.Switch);
                }
            }
        }
    }
}
