using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/IK Layer", fileName = "IkLayerSettings")]
    public sealed class IkLayerSettings : AnimationLayerSettings
    {
        [Header("Hands IK")]
        [SerializeField] private KRigElement rightHand = Element("Right_Hand");
        [SerializeField] private KRigElement leftHand = Element("Left_Hand");
        [SerializeField] private KRigElement rightHandIk = Element("IK RightHand");
        [SerializeField] private KRigElement leftHandIk = Element("IK LeftHand");
        [SerializeField] private KRigElement rightHandHint = Element("IK RightElbow");
        [SerializeField] private KRigElement leftHandHint = Element("IK LeftElbow");

        [Header("Feet IK")]
        [SerializeField] private KRigElement rightFoot = Element("Right_Foot");
        [SerializeField] private KRigElement leftFoot = Element("Left_Foot");
        [SerializeField] private KRigElement rightFootIk = Element("IK RightFoot");
        [SerializeField] private KRigElement leftFootIk = Element("IK LeftFoot");
        [SerializeField] private KRigElement rightFootHint = Element("IK RightKnee");
        [SerializeField] private KRigElement leftFootHint = Element("IK LeftKnee");

        [Header("Humanoid IK")]
        [SerializeField] private bool offsetFeetTargets = true;

        [Header("Control Weight")]
        [SerializeField, Range(0f, 1f)] private float rightHandWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float leftHandWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rightFootWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float leftFootWeight = 1f;

        public KRigElement RightHand => rightHand;
        public KRigElement LeftHand => leftHand;
        public KRigElement RightHandIk => rightHandIk;
        public KRigElement LeftHandIk => leftHandIk;
        public KRigElement RightHandHint => rightHandHint;
        public KRigElement LeftHandHint => leftHandHint;
        public KRigElement RightFoot => rightFoot;
        public KRigElement LeftFoot => leftFoot;
        public KRigElement RightFootIk => rightFootIk;
        public KRigElement LeftFootIk => leftFootIk;
        public KRigElement RightFootHint => rightFootHint;
        public KRigElement LeftFootHint => leftFootHint;
        public bool OffsetFeetTargets => offsetFeetTargets;
        public float RightHandWeight => rightHandWeight;
        public float LeftHandWeight => leftHandWeight;
        public float RightFootWeight => rightFootWeight;
        public float LeftFootWeight => leftFootWeight;

        public override IAnimationLayerJob CreateAnimationJob() => new IkLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            ValidateElement(expectedRig, rightHand, "right hand");
            ValidateElement(expectedRig, leftHand, "left hand");
            ValidateElement(expectedRig, rightHandIk, "right hand target");
            ValidateElement(expectedRig, leftHandIk, "left hand target");
            ValidateElement(expectedRig, rightHandHint, "right hand hint");
            ValidateElement(expectedRig, leftHandHint, "left hand hint");
            ValidateElement(expectedRig, rightFoot, "right foot");
            ValidateElement(expectedRig, leftFoot, "left foot");
            ValidateElement(expectedRig, rightFootIk, "right foot target");
            ValidateElement(expectedRig, leftFootIk, "left foot target");
            ValidateElement(expectedRig, rightFootHint, "right foot hint");
            ValidateElement(expectedRig, leftFootHint, "left foot hint");
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref rightHand);
            SynchronizeRigElement(ref leftHand);
            SynchronizeRigElement(ref rightHandIk);
            SynchronizeRigElement(ref leftHandIk);
            SynchronizeRigElement(ref rightHandHint);
            SynchronizeRigElement(ref leftHandHint);
            SynchronizeRigElement(ref rightFoot);
            SynchronizeRigElement(ref leftFoot);
            SynchronizeRigElement(ref rightFootIk);
            SynchronizeRigElement(ref leftFootIk);
            SynchronizeRigElement(ref rightFootHint);
            SynchronizeRigElement(ref leftFootHint);
        }

        private void ValidateElement(KRig rig, KRigElement element, string label)
        {
            RigHandleUtility.ResolveElement(rig, element, name + " " + label);
        }

        private static KRigElement Element(string name) => new KRigElement(-1, name, 0);
    }
}
