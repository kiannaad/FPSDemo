using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Attach Hand Layer", fileName = "AttachHandLayerSettings")]
    public sealed class AttachHandLayerSettings : AnimationLayerSettings
    {
        [SerializeField, Tooltip("Optional sampled finger pose for a grip attachment.")]
        private AnimationClip customHandPose;
        [SerializeField] private KRigElement handBone = Element("Left_Hand");
        [SerializeField] private KRigElement ikHandBone = Element("IK LeftHand");
        [SerializeField] private KRigElement ikWeaponBone = Element("IK WeaponBone");
        [SerializeField] private KRigElement weaponBone = Element("WeaponBone");
        [SerializeField] private string elementChainName = "LeftHand";
        [SerializeField] private KTransform handPoseOffset = default;
        [SerializeField, Range(0f, 1f)] private float overridePoseWeight;

        public AnimationClip CustomHandPose => customHandPose;
        public KRigElement HandBone => handBone;
        public KRigElement IkHandBone => ikHandBone;
        public KRigElement IkWeaponBone => ikWeaponBone;
        public KRigElement WeaponBone => weaponBone;
        public string ElementChainName => elementChainName;
        public KTransform HandPoseOffset => IsDefault(handPoseOffset) ? KTransform.Identity : handPoseOffset;
        public float OverridePoseWeight => overridePoseWeight;

        public override IAnimationLayerJob CreateAnimationJob() => new AttachHandLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, handBone, name + " hand bone");
            RigHandleUtility.ResolveElement(expectedRig, ikHandBone, name + " IK hand bone");
            RigHandleUtility.ResolveElement(expectedRig, ikWeaponBone, name + " IK weapon bone");
            RigHandleUtility.ResolveElement(expectedRig, weaponBone, name + " weapon bone");
            RigHandleUtility.ResolveChain(expectedRig, elementChainName, name);
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref handBone);
            SynchronizeRigElement(ref ikHandBone);
            SynchronizeRigElement(ref ikWeaponBone);
            SynchronizeRigElement(ref weaponBone);
        }

        private static KRigElement Element(string name) => new KRigElement(-1, name, 0);

        private static bool IsDefault(KTransform value)
        {
            return value.Rotation.x == 0f && value.Rotation.y == 0f
                && value.Rotation.z == 0f && value.Rotation.w == 0f;
        }
    }
}
