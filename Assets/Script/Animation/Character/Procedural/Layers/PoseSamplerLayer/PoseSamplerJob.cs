using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct PoseSamplerJob : IAnimationJob
    {
        public TransformStreamHandle CharacterRoot;
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
        public KTransform WeaponBoneComponentPose;
        public KTransform WeaponBoneSpinePose;
        public KTransform WeaponBoneRightLocalPose;
        public KTransform WeaponBoneLeftLocalPose;
        public KTransform DefaultWeaponPose;
        public KTransform WeaponBoneOffset;
        public KTransform RightHandReferencePose;
        public KTransform LeftHandReferencePose;
        public KTransform RightHintReferencePose;
        public KTransform LeftHintReferencePose;
        public float WeaponBoneWeight;
        public float StabilizationWeight;
        public float Weight;
        public bool OverwriteRoot;
        public bool OverwriteWeaponBone;
        public bool UseReferenceHandTargets;
        public bool HasValidRoot;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;

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

            if (!UseReferenceHandTargets && WeaponBoneWeight > 0f)
            {
                KTransform componentPose = root.GetWorldTransform(WeaponBoneComponentPose, false);
                KTransform spinePose = spine.GetWorldTransform(WeaponBoneSpinePose, false);
                spinePose.Position -= componentPose.Position;
                spinePose.Rotation = Quaternion.Inverse(componentPose.Rotation) * spinePose.Rotation;
                WeaponBone.SetPosition(stream, WeaponBone.GetPosition(stream) + spinePose.Position);
                WeaponBone.SetRotation(stream, WeaponBone.GetRotation(stream) * spinePose.Rotation);
            }

            WeaponBoneRight.SetLocalPosition(stream, WeaponBoneRightLocalPose.Position);
            WeaponBoneRight.SetLocalRotation(stream, WeaponBoneRightLocalPose.Rotation);
            WeaponBoneLeft.SetLocalPosition(stream, WeaponBoneLeftLocalPose.Position);
            WeaponBoneLeft.SetLocalRotation(stream, WeaponBoneLeftLocalPose.Rotation);

            KTransform rightReference = AnimationLayerJobUtility.GetTransform(stream, WeaponBoneRight);
            KTransform leftReference = AnimationLayerJobUtility.GetTransform(stream, WeaponBoneLeft);

            KTransform right = AnimationLayerJobUtility.GetTransform(stream, WeaponBone);
            KTransform pose;
            if (UseReferenceHandTargets)
            {
                pose = right;
            }
            else
            {
                pose = WeaponBoneWeight >= 0f
                    ? KTransform.Lerp(rightReference, right, WeaponBoneWeight)
                    : KTransform.Lerp(rightReference, leftReference, -WeaponBoneWeight);
            }

            KTransform rightHand = AnimationLayerJobUtility.GetTransform(stream, IkRightHand);
            KTransform leftHand = AnimationLayerJobUtility.GetTransform(stream, IkLeftHand);
            KTransform rightHint = AnimationLayerJobUtility.GetTransform(stream, IkRightHandHint);
            KTransform leftHint = AnimationLayerJobUtility.GetTransform(stream, IkLeftHandHint);
            if (UseReferenceHandTargets)
            {
                KTransform referenceWeapon = AnimationLayerJobUtility.GetTransform(stream, WeaponBone);
                rightHand = referenceWeapon.GetWorldTransform(RightHandReferencePose, false);
                leftHand = referenceWeapon.GetWorldTransform(LeftHandReferencePose, false);
                rightHint = referenceWeapon.GetWorldTransform(RightHintReferencePose, false);
                leftHint = referenceWeapon.GetWorldTransform(LeftHintReferencePose, false);
            }
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
        }
    }
}
