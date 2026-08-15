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
        public KTransform DefaultWeaponPose;
        public KTransform WeaponBoneOffset;
        public float WeaponBoneWeight;
        public float StabilizationWeight;
        public float Weight;
        public bool OverwriteRoot;
        public bool OverwriteWeaponBone;
        public bool HasValidRoot;

        public void ProcessAnimation(AnimationStream stream)
        {
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

            if (WeaponBoneWeight > 0f)
            {
                KTransform componentPose = root.GetWorldTransform(WeaponBoneComponentPose, false);
                KTransform spinePose = spine.GetWorldTransform(WeaponBoneSpinePose, false);
                spinePose.Position -= componentPose.Position;
                spinePose.Rotation = Quaternion.Inverse(componentPose.Rotation) * spinePose.Rotation;
                WeaponBone.SetPosition(stream, WeaponBone.GetPosition(stream) + spinePose.Position);
                WeaponBone.SetRotation(stream, WeaponBone.GetRotation(stream) * spinePose.Rotation);
            }

            KTransform pose = AnimationLayerJobUtility.GetTransform(stream, WeaponBoneRight);
            KTransform right = AnimationLayerJobUtility.GetTransform(stream, WeaponBone);
            KTransform left = AnimationLayerJobUtility.GetTransform(stream, WeaponBoneLeft);
            pose = WeaponBoneWeight >= 0f
                ? KTransform.Lerp(pose, right, WeaponBoneWeight)
                : KTransform.Lerp(pose, left, -WeaponBoneWeight);

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
        }
    }
}
