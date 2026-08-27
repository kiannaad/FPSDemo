using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/View Layer", fileName = "ViewLayerSettings")]
    public sealed class ViewLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private KPose ikWeaponBone = CreatePose("IK WeaponBone");
        [SerializeField] private KPose ikRightHand = CreatePose("IK RightHand");
        [SerializeField] private KPose ikLeftHand = CreatePose("IK LeftHand");

        public KPose IkWeaponBone => ikWeaponBone;
        public KPose IkRightHand => ikRightHand;
        public KPose IkLeftHand => ikLeftHand;

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new ViewLayerJob();
        }

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, ikWeaponBone.Element, name + " weapon pose");
            RigHandleUtility.ResolveElement(expectedRig, ikRightHand.Element, name + " right-hand pose");
            RigHandleUtility.ResolveElement(expectedRig, ikLeftHand.Element, name + " left-hand pose");
        }

        protected override void OnRigUpdated()
        {
            Synchronize(ref ikWeaponBone);
            Synchronize(ref ikRightHand);
            Synchronize(ref ikLeftHand);
        }

        private void Synchronize(ref KPose pose)
        {
            SynchronizeRigElement(ref pose.Element);
        }

        private static KPose CreatePose(string elementName)
        {
            return new KPose
            {
                Element = new KRigElement(-1, elementName, 0),
                Pose = KTransform.Identity,
                Space = TransformSpace.ComponentSpace,
                ModifyMode = TransformModifyMode.Add
            };
        }
    }
}
