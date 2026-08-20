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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct AttachHandDebugSample
    {
        public Vector3 WeaponPosition;
        public Vector3 HandPosition;
        public Vector3 IkWeaponPosition;
        public Vector3 IkHandPosition;
        public Vector3 RelativeHandPosition;
        public float RelativeHandRotationAngle;
        public byte Captured;
    }
#endif

    public struct AttachHandJob : IAnimationJob
    {
        public TransformStreamHandle Hand;
        public TransformStreamHandle Weapon;
        public TransformStreamHandle IkHand;
        public TransformStreamHandle IkWeapon;
        public KTransform RelativeHandPose;
        public KTransform HandPoseOffset;
        public NativeArray<AttachHandPoseData> Chain;
        public float Weight;
        public bool ReferenceInitialized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public NativeArray<AttachHandDebugSample> DebugSamples;
#endif

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!ReferenceInitialized)
            {
                KTransform weaponReference = AnimationLayerJobUtility.GetTransform(stream, Weapon);
                KTransform handReference = AnimationLayerJobUtility.GetTransform(stream, Hand);
                RelativeHandPose = weaponReference.GetRelativeTransform(handReference, false);
                for (int index = 0; index < Chain.Length; index++)
                {
                    AttachHandPoseData item = Chain[index];
                    item.LocalRotation = item.Handle.GetLocalRotation(stream);
                    Chain[index] = item;
                }

                ReferenceInitialized = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugSamples.IsCreated)
                {
                    DebugSamples[0] = new AttachHandDebugSample
                    {
                        WeaponPosition = weaponReference.Position,
                        HandPosition = handReference.Position,
                        IkWeaponPosition = AnimationLayerJobUtility.GetTransform(stream, IkWeapon).Position,
                        IkHandPosition = AnimationLayerJobUtility.GetTransform(stream, IkHand).Position,
                        RelativeHandPosition = RelativeHandPose.Position,
                        RelativeHandRotationAngle = Quaternion.Angle(Quaternion.identity, RelativeHandPose.Rotation),
                        Captured = 1
                    };
                }
#endif
            }

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
