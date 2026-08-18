using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Pose Sampler Layer", fileName = "PoseSamplerLayerSettings")]
    public sealed class PoseSamplerLayerSettings : AnimationLayerSettings
    {
        [Header("General")]
        [SerializeField] private AnimationClip referencePose;
        [SerializeField] private KTransform defaultWeaponPose = default;
        [SerializeField] private KTransform weaponBoneOffset = default;
        [SerializeField] private bool overwriteRoot;
        [SerializeField] private bool overwriteWeaponBone;

        [Header("IK Targets")]
        [SerializeField] private KRigElement ikWeaponBone = Element("IK WeaponBone");
        [SerializeField] private KRigElement ikRightHand = Element("IK RightHand");
        [SerializeField] private KRigElement ikLeftHand = Element("IK LeftHand");
        [SerializeField] private KRigElement ikRightHandHint = Element("IK RightElbow");
        [SerializeField] private KRigElement ikLeftHandHint = Element("IK LeftElbow");

        [Header("Weapon Bone")]
        [SerializeField] private KRigElement weaponBoneRight = Element("IK WeaponBoneRight");
        [SerializeField] private KRigElement weaponBoneLeft = Element("IK WeaponBoneLeft");
        [SerializeField] private KRigElement weaponBone = Element("WeaponBone");
        [SerializeField] private KRigElement cameraBone = Element("Camera");

        [Header("Spine")]
        [SerializeField] private KRigElement pelvis = Element("Hips");
        [SerializeField] private KRigElement spineRoot = Element("Spine");

        [Header("Weights")]
        [SerializeField, Range(0f, 1f)] private float stabilizationWeight = 1f;
        [SerializeField] private string weaponBoneWeightCurve = "WeaponBoneWeight";
        [SerializeField, Range(-1f, 1f)] private float defaultWeaponBoneWeight;
        [SerializeField] private bool useReferenceHandTargets;

        public AnimationClip ReferencePose => referencePose;
        public KTransform DefaultWeaponPose => Normalize(defaultWeaponPose);
        public KTransform WeaponBoneOffset => Normalize(weaponBoneOffset);
        public bool OverwriteRoot => overwriteRoot;
        public bool OverwriteWeaponBone => overwriteWeaponBone;
        public KRigElement IkWeaponBone => ikWeaponBone;
        public KRigElement IkRightHand => ikRightHand;
        public KRigElement IkLeftHand => ikLeftHand;
        public KRigElement IkRightHandHint => ikRightHandHint;
        public KRigElement IkLeftHandHint => ikLeftHandHint;
        public KRigElement WeaponBoneRight => weaponBoneRight;
        public KRigElement WeaponBoneLeft => weaponBoneLeft;
        public KRigElement WeaponBone => weaponBone;
        public KRigElement CameraBone => cameraBone;
        public KRigElement Pelvis => pelvis;
        public KRigElement SpineRoot => spineRoot;
        public float StabilizationWeight => stabilizationWeight;
        public string WeaponBoneWeightCurve => weaponBoneWeightCurve;
        public float DefaultWeaponBoneWeight => defaultWeaponBoneWeight;
        public bool UseReferenceHandTargets => useReferenceHandTargets;

        public override IAnimationLayerJob CreateAnimationJob() => new PoseSamplerLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            ValidateElement(expectedRig, ikWeaponBone, "IK weapon bone");
            ValidateElement(expectedRig, ikRightHand, "IK right hand");
            ValidateElement(expectedRig, ikLeftHand, "IK left hand");
            ValidateElement(expectedRig, ikRightHandHint, "IK right hint");
            ValidateElement(expectedRig, ikLeftHandHint, "IK left hint");
            ValidateElement(expectedRig, weaponBoneRight, "right weapon bone");
            ValidateElement(expectedRig, weaponBoneLeft, "left weapon bone");
            ValidateElement(expectedRig, weaponBone, "weapon bone");
            if (useReferenceHandTargets)
            {
                ValidateElement(expectedRig, cameraBone, "camera bone");
            }
            ValidateElement(expectedRig, pelvis, "pelvis");
            ValidateElement(expectedRig, spineRoot, "spine root");
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref ikWeaponBone);
            SynchronizeRigElement(ref ikRightHand);
            SynchronizeRigElement(ref ikLeftHand);
            SynchronizeRigElement(ref ikRightHandHint);
            SynchronizeRigElement(ref ikLeftHandHint);
            SynchronizeRigElement(ref weaponBoneRight);
            SynchronizeRigElement(ref weaponBoneLeft);
            SynchronizeRigElement(ref weaponBone);
            if (useReferenceHandTargets)
            {
                SynchronizeRigElement(ref cameraBone);
            }
            SynchronizeRigElement(ref pelvis);
            SynchronizeRigElement(ref spineRoot);
        }

        private void ValidateElement(KRig rig, KRigElement element, string label)
        {
            RigHandleUtility.ResolveElement(rig, element, name + " " + label);
        }

        private static KRigElement Element(string name) => new KRigElement(-1, name, 0);

        private static KTransform Normalize(KTransform value)
        {
            return value.Rotation.x == 0f && value.Rotation.y == 0f
                && value.Rotation.z == 0f && value.Rotation.w == 0f
                ? KTransform.Identity
                : value;
        }
    }
}
