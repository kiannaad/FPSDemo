using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct PoseSamplerDebugSample
    {
        public Vector3 WeaponBonePosition;
        public Vector3 RightReferencePosition;
        public Vector3 LeftReferencePosition;
        public Vector3 ExpectedRightBlendPosition;
        public Vector3 IkWeaponPosition;
        public float WeaponBoneWeight;
        public byte Captured;
    }
#endif

    public struct PoseSamplerJob : IAnimationJob
    {
        public TransformStreamHandle CharacterRoot;
        public TransformStreamHandle ModelRoot;
        public TransformStreamHandle SpineRoot;
        public TransformStreamHandle Pelvis;
        public TransformStreamHandle PelvisParent;
        public TransformStreamHandle WeaponBone;
        public TransformStreamHandle WeaponBoneRight;
        public TransformStreamHandle WeaponBoneLeft;
        public TransformStreamHandle IkWeaponBone;
        public TransformStreamHandle IkRightHand;
        public TransformStreamHandle IkLeftHand;
        public TransformStreamHandle IkRightHandHint;
        public TransformStreamHandle IkLeftHandHint;
        public Quaternion CachedPelvisPose;
        public Quaternion ModelRootReferenceRotation;
        public KTransform WeaponBoneComponentPose;
        public KTransform WeaponBoneSpinePose;
        public KTransform WeaponBoneRightLocalPose;
        public KTransform WeaponBoneLeftLocalPose;
        public KTransform DefaultWeaponPose;
        public KTransform WeaponBoneOffset;
        public float WeaponBoneWeight;
        public float StabilizationWeight;
        public float Weight;
        public bool OverwriteRoot;
        public bool OverwriteWeaponBone;
        public bool HasValidRoot;
        public bool HasModelRootReferencePose;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public NativeArray<PoseSamplerDebugSample> DebugSamples;
#endif

        public void ProcessAnimation(AnimationStream stream)
        {
            // Layer weight may fade visual stabilization out, but downstream IK still requires
            // the weapon mount and hand targets to be rebuilt from the current weapon every frame.
            RestoreModelRootRotation(stream);
            KTransform savedRoot = KTransform.Identity;
            KTransform worldPelvis = AnimationLayerJobUtility.GetTransform(stream, Pelvis);
            if (OverwriteRoot && HasValidRoot)
            {
                savedRoot = AnimationLayerJobUtility.GetTransform(stream, PelvisParent, false);
                PelvisParent.SetLocalPosition(stream, Vector3.zero);
                PelvisParent.SetLocalRotation(stream, Quaternion.identity);
            }

            Pelvis.SetPosition(stream, worldPelvis.Position);
            Pelvis.SetRotation(stream, worldPelvis.Rotation);
            StabilizeSpine(stream);
            PositionWeaponBone(stream);

            if (OverwriteRoot && HasValidRoot)
            {
                PelvisParent.SetLocalPosition(stream, savedRoot.Position);
                PelvisParent.SetLocalRotation(stream, savedRoot.Rotation);
                Pelvis.SetPosition(stream, worldPelvis.Position);
                Pelvis.SetRotation(stream, worldPelvis.Rotation);
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private void RestoreModelRootRotation(AnimationStream stream)
        {
            if (!HasModelRootReferencePose)
            {
                return;
            }

            Quaternion rootRotation = CharacterRoot.GetRotation(stream);
            ModelRoot.SetRotation(stream, rootRotation * ModelRootReferenceRotation);
        }

        private void StabilizeSpine(AnimationStream stream)
        {
            Quaternion rootRotation = CharacterRoot.GetRotation(stream);
            Quaternion pelvisWorldRotation = rootRotation * CachedPelvisPose;
            Quaternion stabilized = pelvisWorldRotation * SpineRoot.GetLocalRotation(stream);
            Quaternion current = SpineRoot.GetRotation(stream);
            SpineRoot.SetRotation(stream, Quaternion.Slerp(current, stabilized, StabilizationWeight * Weight));
        }

        private void PositionWeaponBone(AnimationStream stream)
        {
            KTransform root = AnimationLayerJobUtility.GetTransform(stream, CharacterRoot);
            KTransform spine = AnimationLayerJobUtility.GetTransform(stream, SpineRoot);
            if (OverwriteWeaponBone)
            {
                KTransform desired = root.GetWorldTransform(DefaultWeaponPose, false);
                WeaponBone.SetPosition(stream, desired.Position);
                WeaponBone.SetRotation(stream, desired.Rotation);
            }

            if (WeaponBoneWeight > 0f)
            {
                KTransform componentPose = root.GetWorldTransform(WeaponBoneComponentPose, false);
                KTransform spinePose = spine.GetWorldTransform(WeaponBoneSpinePose, false);
                spinePose.Position -= componentPose.Position;
                spinePose.Rotation = Quaternion.Inverse(componentPose.Rotation) * spinePose.Rotation;
                float correctionWeight = Mathf.Clamp01(Weight);
                WeaponBone.SetPosition(
                    stream,
                    WeaponBone.GetPosition(stream) + spinePose.Position * correctionWeight);
                WeaponBone.SetRotation(
                    stream,
                    WeaponBone.GetRotation(stream)
                    * Quaternion.Slerp(Quaternion.identity, spinePose.Rotation, correctionWeight));
            }

            WeaponBoneRight.SetLocalPosition(stream, WeaponBoneRightLocalPose.Position);
            WeaponBoneRight.SetLocalRotation(stream, WeaponBoneRightLocalPose.Rotation);
            WeaponBoneLeft.SetLocalPosition(stream, WeaponBoneLeftLocalPose.Position);
            WeaponBoneLeft.SetLocalRotation(stream, WeaponBoneLeftLocalPose.Rotation);

            KTransform rightReference = AnimationLayerJobUtility.GetTransform(stream, WeaponBoneRight);
            KTransform leftReference = AnimationLayerJobUtility.GetTransform(stream, WeaponBoneLeft);

            KTransform right = AnimationLayerJobUtility.GetTransform(stream, WeaponBone);
            KTransform pose = WeaponBoneWeight >= 0f
                ? KTransform.Lerp(rightReference, right, WeaponBoneWeight)
                : KTransform.Lerp(rightReference, leftReference, -WeaponBoneWeight);

            KTransform rightHand = AnimationLayerJobUtility.GetTransform(stream, IkRightHand);
            KTransform leftHand = AnimationLayerJobUtility.GetTransform(stream, IkLeftHand);
            KTransform rightHint = AnimationLayerJobUtility.GetTransform(stream, IkRightHandHint);
            KTransform leftHint = AnimationLayerJobUtility.GetTransform(stream, IkLeftHandHint);
            IkWeaponBone.SetPosition(stream, pose.Position);
            IkWeaponBone.SetRotation(stream, pose.Rotation * WeaponBoneOffset.Rotation);
            IkRightHand.SetPosition(stream, rightHand.Position);
            IkRightHand.SetRotation(stream, rightHand.Rotation);
            IkLeftHand.SetPosition(stream, leftHand.Position);
            IkLeftHand.SetRotation(stream, leftHand.Rotation);
            IkRightHandHint.SetPosition(stream, rightHint.Position);
            IkRightHandHint.SetRotation(stream, rightHint.Rotation);
            IkLeftHandHint.SetPosition(stream, leftHint.Position);
            IkLeftHandHint.SetRotation(stream, leftHint.Rotation);
// #if UNITY_EDITOR || DEVELOPMENT_BUILD
//             if (DebugSamples.IsCreated && DebugSamples[0].Captured == 0)
//             {
//                 DebugSamples[0] = new PoseSamplerDebugSample
//                 {
//                     WeaponBonePosition = right.Position,
//                     RightReferencePosition = rightReference.Position,
//                     LeftReferencePosition = leftReference.Position,
//                     ExpectedRightBlendPosition = Vector3.Lerp(rightReference.Position, right.Position, WeaponBoneWeight),
//                     IkWeaponPosition = pose.Position,
//                     WeaponBoneWeight = WeaponBoneWeight,
//                     Captured = 1
//                 };
//             }
// #endif
        }
    }
}
