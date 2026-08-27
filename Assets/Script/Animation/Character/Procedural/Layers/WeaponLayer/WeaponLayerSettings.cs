using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    public abstract class WeaponLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private KRigElement weaponIkBone = Element("IK Weapon");
        [SerializeField] private KRigElement rightElbow = Element("IK RightElbow");
        [SerializeField] private KRigElement leftElbow = Element("IK LeftElbow");
        [SerializeField] private Vector3 animatedPivotOffset;
        [SerializeField, Range(0f, 1f)] private float hintTargetWeight = 1f;

        public KRigElement WeaponIkBone => weaponIkBone;
        public KRigElement RightElbow => rightElbow;
        public KRigElement LeftElbow => leftElbow;
        public Vector3 AnimatedPivotOffset => animatedPivotOffset;
        public float HintTargetWeight => hintTargetWeight;

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, weaponIkBone, name + " weapon IK bone");
            RigHandleUtility.ResolveElement(expectedRig, rightElbow, name + " right elbow");
            RigHandleUtility.ResolveElement(expectedRig, leftElbow, name + " left elbow");
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref weaponIkBone);
            SynchronizeRigElement(ref rightElbow);
            SynchronizeRigElement(ref leftElbow);
        }

        private static KRigElement Element(string name)
        {
            return new KRigElement(-1, name, 0);
        }
    }
}
