using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public struct PoseOffsetEntry
    {
        public KPose Pose;
        public AnimationCurveBlend Blend;
        public bool KeepChildrenPose;
    }

    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Pose Offset Layer", fileName = "PoseOffsetLayerSettings")]
    public sealed class PoseOffsetLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private List<PoseOffsetEntry> poseOffsets = new List<PoseOffsetEntry>();

        public IReadOnlyList<PoseOffsetEntry> PoseOffsets => poseOffsets;

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new PoseOffsetLayerJob();
        }

        public override void Validate(CGame.Animation.Rig.KRig expectedRig)
        {
            base.Validate(expectedRig);
            if (poseOffsets == null) throw new InvalidOperationException(name + " pose offsets are missing.");
            for (int index = 0; index < poseOffsets.Count; index++)
            {
                RigHandleUtility.ResolveElement(expectedRig, poseOffsets[index].Pose.Element, name + " offset " + index);
            }
        }

        protected override void OnRigUpdated()
        {
            for (int index = 0; index < poseOffsets.Count; index++)
            {
                PoseOffsetEntry entry = poseOffsets[index];
                KPose pose = entry.Pose;
                SynchronizeRigElement(ref pose.Element);
                entry.Pose = pose;
                poseOffsets[index] = entry;
            }
        }
    }
}
