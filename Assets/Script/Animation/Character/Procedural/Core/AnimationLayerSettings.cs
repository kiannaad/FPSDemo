using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    public abstract class AnimationLayerSettings : ScriptableObject
    {
        [SerializeField] private KRig rig;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField] private AnimationCurveBlend[] curveBlending = Array.Empty<AnimationCurveBlend>();
        [SerializeField] private bool linkDynamically;

        public KRig Rig => rig;
        public float Alpha => alpha;
        public AnimationCurveBlend[] CurveBlending => curveBlending;
        public bool LinkDynamically => linkDynamically;

        public float EvaluateWeight(CharacterAnimInstance owner)
        {
            if (curveBlending == null)
            {
                throw new InvalidOperationException($"{name} curve blending collection is missing.");
            }

            float weight = 1f;
            for (int index = 0; index < curveBlending.Length; index++)
            {
                AnimationCurveBlend blend = curveBlending[index]
                    ?? throw new InvalidOperationException($"{name} curve blend at index {index} is missing.");
                weight *= blend.Evaluate(owner);
            }

            return Mathf.Clamp01(weight * alpha);
        }

        public void Configure(KRig profileRig)
        {
            rig = profileRig ?? throw new ArgumentNullException(nameof(profileRig));
            if (rig.Hierarchy.Count > 0)
            {
                OnRigUpdated();
            }
        }

        protected virtual void OnRigUpdated()
        {
        }

        protected void SynchronizeRigElement(ref KRigElement element)
        {
            element = RigHandleUtility.ResolveElement(rig, element, name);
        }

        public virtual void Validate(KRig expectedRig)
        {
            if (expectedRig == null)
            {
                throw new ArgumentNullException(nameof(expectedRig));
            }

            if (rig != expectedRig)
            {
                throw new InvalidOperationException($"{name} must reference the Pawn KRig.");
            }

            if (alpha < 0f || alpha > 1f)
            {
                throw new InvalidOperationException($"{name} alpha must be between zero and one.");
            }

            if (curveBlending == null)
            {
                throw new InvalidOperationException($"{name} curve blending collection is missing.");
            }

            for (int index = 0; index < curveBlending.Length; index++)
            {
                if (curveBlending[index] == null)
                {
                    throw new InvalidOperationException($"{name} curve blend at index {index} is missing.");
                }
            }

            IAnimationLayerJob job = null;
            try
            {
                job = CreateAnimationJob();
                if (job == null)
                {
                    throw new InvalidOperationException($"{name} did not create an animation layer job.");
                }

                if (job.SettingsType != GetType())
                {
                    throw new InvalidOperationException(
                        $"{name} creates {job.GetType().Name}, which expects {job.SettingsType.Name} instead of {GetType().Name}.");
                }
            }
            finally
            {
                job?.Dispose();
            }
        }

        public abstract IAnimationLayerJob CreateAnimationJob();
    }
}
