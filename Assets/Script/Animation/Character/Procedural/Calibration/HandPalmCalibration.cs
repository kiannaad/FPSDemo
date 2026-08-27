using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(
        menuName = "CGame/Animation/Hand Palm Calibration",
        fileName = "HandPalmCalibration")]
    public sealed class HandPalmCalibration : ScriptableObject
    {
        [SerializeField] private KRigElement rightHand = new KRigElement(-1, "Right_Hand", 0);
        [SerializeField] private KTransform rightPalmLocalPose = default;
        [SerializeField] private KRigElement leftHand = new KRigElement(-1, "Left_Hand", 0);
        [SerializeField] private KTransform leftPalmLocalPose = default;

        public KRigElement RightHand => rightHand;
        public KTransform RightPalmLocalPose => Normalize(rightPalmLocalPose);
        public KRigElement LeftHand => leftHand;
        public KTransform LeftPalmLocalPose => Normalize(leftPalmLocalPose);

        public KTransform GetRightPalmWorld(Transform hand)
        {
            return new KTransform(hand).GetWorldTransform(RightPalmLocalPose, false);
        }

        public KTransform GetLeftPalmWorld(Transform hand)
        {
            return new KTransform(hand).GetWorldTransform(LeftPalmLocalPose, false);
        }

        private static KTransform Normalize(KTransform value)
        {
            return value.Rotation.x == 0f && value.Rotation.y == 0f
                && value.Rotation.z == 0f && value.Rotation.w == 0f
                ? KTransform.Identity
                : value;
        }
    }
}
