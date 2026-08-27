using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public struct BlendingLayerElement
    {
        public KRigElement Element;
        [Range(0f, 1f)] public float Weight;
    }

    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Blending Layer", fileName = "BlendingLayerSettings")]
    public sealed class BlendingLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private AnimationClip desiredPose;
        [SerializeField] private List<BlendingLayerElement> blendingElements = new List<BlendingLayerElement>();
        [SerializeField] private bool blendPosition;

        public AnimationClip DesiredPose => desiredPose;
        public IReadOnlyList<BlendingLayerElement> BlendingElements => blendingElements;
        public bool BlendPosition => blendPosition;

        public override IAnimationLayerJob CreateAnimationJob() => new BlendingLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            if (blendingElements == null)
            {
                throw new InvalidOperationException(name + " blending elements are missing.");
            }

            if (blendingElements.Count > 0 && desiredPose == null)
            {
                throw new InvalidOperationException(name + " desired pose is required when bones are configured.");
            }

            HashSet<int> resolvedIndices = new HashSet<int>();
            for (int index = 0; index < blendingElements.Count; index++)
            {
                BlendingLayerElement entry = blendingElements[index];
                KRigElement resolved = RigHandleUtility.ResolveElement(
                    expectedRig,
                    entry.Element,
                    name + " element " + index);
                if (!resolvedIndices.Add(resolved.Index))
                {
                    throw new InvalidOperationException(name + " duplicates blending bone " + resolved.Name + ".");
                }

                if (entry.Weight < 0f || entry.Weight > 1f)
                {
                    throw new InvalidOperationException(name + " element weight must be between zero and one.");
                }
            }
        }

        protected override void OnRigUpdated()
        {
            for (int index = 0; index < blendingElements.Count; index++)
            {
                BlendingLayerElement entry = blendingElements[index];
                SynchronizeRigElement(ref entry.Element);
                blendingElements[index] = entry;
            }
        }
    }
}
