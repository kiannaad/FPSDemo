using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterAnimInstance : IDisposable
    {
        private readonly AnimationUpdateContext updateContext;
        private readonly Animator animator;
        private readonly CharacterAnimatorController animatorController;
        private readonly CharacterPlayablesController playablesController;
        private readonly CharacterBoneController boneController;
        private bool isDisposed;
        private int completedAnimationEvaluationCount;

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

            this.animator = animator;
            updateContext = new AnimationUpdateContext(pawn, source);
            animatorController = new CharacterAnimatorController(animator, updateContext);
            playablesController = new CharacterPlayablesController(
                pawn,
                animator,
                upperBodyMask);
            boneController = new CharacterBoneController(
                animator,
                rigComponent,
                this);
        }

        public AnimationUpdateContext UpdateContext => updateContext;

        public int CompletedAnimationEvaluationCount => completedAnimationEvaluationCount;


        public bool TryPlayWeaponIkMotion(IkMotionLayerSettings motion)
        {
            return !isDisposed && boneController.TryPlayWeaponIkMotion(motion);
        }

        public bool IsWeaponIkMotionComplete(IkMotionLayerSettings motion)
        {
            return !isDisposed && boneController.IsWeaponIkMotionComplete(motion);
        }

        public bool HasWeaponIkMotionReachedEnd(IkMotionLayerSettings motion)
        {
            return !isDisposed && boneController.HasWeaponIkMotionReachedEnd(motion);
        }
public CharacterBoneController BoneController => boneController;

        public bool TryGetWeaponCollisionProbe(
            out Vector3 origin,
            out Vector3 direction,
            out float distance)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            distance = 0f;
            return !isDisposed
                && boneController.TryGetWeaponCollisionProbe(out origin, out direction, out distance);
        }
        public float GetCurveValue(string curveName)
        {
            if (string.IsNullOrWhiteSpace(curveName))
            {
                return 0f;
            }

            float mixerValue = GetCurveValue(curveName, AnimationCurveBlendSource.Playables);
            if (!Mathf.Approximately(mixerValue, 0f))
            {
                return mixerValue;
            }

            return GetCurveValue(curveName, AnimationCurveBlendSource.Animator);
        }

        public float GetCurveValueOrDefault(string curveName, float defaultValue)
        {
            if (string.IsNullOrWhiteSpace(curveName))
            {
                return defaultValue;
            }

            if (playablesController.TryGetCurveValue(curveName, out float playableValue))
            {
                return playableValue;
            }

            return HasFloatParameter(curveName) ? animator.GetFloat(curveName) : defaultValue;
        }

        internal float GetCurveValueOrDefault(
            string curveName,
            AnimationCurveBlendSource source,
            float defaultValue)
        {
            if (string.IsNullOrWhiteSpace(curveName))
            {
                return defaultValue;
            }

            switch (source)
            {
                case AnimationCurveBlendSource.Playables:
                    return playablesController.TryGetCurveValue(curveName, out float playableValue)
                        ? playableValue
                        : defaultValue;
                case AnimationCurveBlendSource.Animator:
                    return HasFloatParameter(curveName) ? animator.GetFloat(curveName) : defaultValue;
                case AnimationCurveBlendSource.Context:
                    return updateContext.GetCurveValue(curveName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown curve blend source.");
            }
        }

        internal float GetCurveValue(string curveName, AnimationCurveBlendSource source)
        {
            if (string.IsNullOrWhiteSpace(curveName))
            {
                return 0f;
            }

            switch (source)
            {
                case AnimationCurveBlendSource.Animator:
                    return HasFloatParameter(curveName) ? animator.GetFloat(curveName) : 0f;
                case AnimationCurveBlendSource.Playables:
                    return playablesController.GetCurveValue(curveName);
                case AnimationCurveBlendSource.Context:
                    return updateContext.GetCurveValue(curveName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown curve blend source.");
            }
        }

        private bool HasFloatParameter(string parameterName)
        {
            int parameterHash = Animator.StringToHash(parameterName);
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                if (parameter.nameHash == parameterHash
                    && parameter.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }

            return false;
        }

        internal CharacterAnimatorController AnimatorController => animatorController;
        internal CharacterPlayablesController PlayablesController => playablesController;
        internal bool TrySetAnimatorTrigger(string triggerName)
        {
            return !isDisposed && playablesController.TrySetTrigger(triggerName);
        }

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
                if (boneController.IsValid() && playablesController.IsValid())
                {
                    completedAnimationEvaluationCount++;
                }
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
