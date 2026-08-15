using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct AttachHandPoseData
    {
        public TransformStreamHandle Handle;
        public Quaternion LocalRotation;
    }

    public struct AttachHandJob : IAnimationJob
    {
        public TransformStreamHandle IkHand;
        public TransformStreamHandle IkWeapon;
        public KTransform RelativeHandPose;
        public KTransform HandPoseOffset;
        public NativeArray<AttachHandPoseData> Chain;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            KTransform weapon = AnimationLayerJobUtility.GetTransform(stream, IkWeapon);
            KTransform attached = new KTransform(
                weapon.TransformPoint(RelativeHandPose.Position + HandPoseOffset.Position, false),
                weapon.Rotation * (RelativeHandPose.Rotation * HandPoseOffset.Rotation));
            KTransform hand = AnimationLayerJobUtility.GetTransform(stream, IkHand);
            hand = KTransform.Lerp(hand, attached, Weight);
            IkHand.SetPosition(stream, hand.Position);
            IkHand.SetRotation(stream, hand.Rotation);

            for (int index = 0; index < Chain.Length; index++)
            {
                AttachHandPoseData item = Chain[index];
                Quaternion current = item.Handle.GetLocalRotation(stream);
                item.Handle.SetLocalRotation(stream, Quaternion.Slerp(current, item.LocalRotation, Weight));
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
