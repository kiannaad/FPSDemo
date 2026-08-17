using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEngine;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.IkLayer
{
    /// <summary>
    /// 双骨骼 IK 求解层的静态配置。
    /// 前面的 Layer 负责移动 IK target/hint，本层位于链路后段，把目标转换为真实肢体骨骼旋转。
    /// </summary>
    public class IkLayerSettings : FPSAnimatorLayerSettings
    {
        [Header("Hands IK")]
        // tip：左右手真实骨骼。其父节点和祖父节点会分别作为 mid 与 root 参与双骨骼求解。
        public KRigElement rightHand;
        public KRigElement leftHand;
        
        // target：手腕应抵达的目标，通常由 PoseSampler、AttachHand、View、ADS 等层共同生成。
        public KRigElement rightHandIk = new KRigElement(-1, FPSANames.IkRightHand);
        public KRigElement leftHandIk = new KRigElement(-1, FPSANames.IkLeftHand);
        
        // hint：控制手肘弯曲平面的提示目标，避免肘部朝向不稳定。
        public KRigElement rightHandHint = new KRigElement(-1, FPSANames.IkRightElbow);
        public KRigElement leftHandHint = new KRigElement(-1, FPSANames.IkLeftElbow);
        
        [Header("Foot IK")]
        // 足部同样按 root-mid-tip-target-hint 组成双骨骼链；本层本身不负责地面射线检测。
        public KRigElement rightFoot;
        public KRigElement leftFoot;
        
        public KRigElement rightFootIk = new KRigElement(-1, FPSANames.IkRightFoot);
        public KRigElement leftFootIk = new KRigElement(-1, FPSANames.IkLeftFoot);
        
        public KRigElement rightFootHint = new KRigElement(-1, FPSANames.IkRightKnee);
        public KRigElement leftFootHint = new KRigElement(-1, FPSANames.IkLeftKnee);

        [Header("Humanoid IK")]
        [Tooltip("If true, the layer will rotate feet around the root to compensate the turn rotation.")]
        // Humanoid 路径中根据 TurnOffset 对脚目标做根节点旋转补偿。
        public bool offsetFeetTargets = true;

        [Header("Control Weight")]
        // 单条肢体权重还会乘以 FPSAnimatorLayerSettings 的总 Layer weight。
        [Range(0f, 1f)] public float rightHandWeight = 1f;
        [Range(0f, 1f)] public float leftHandWeight = 1f;
        [Range(0f, 1f)] public float rightFootWeight = 1f;
        [Range(0f, 1f)] public float leftFootWeight = 1f;

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new IkLayerJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            UpdateRigElement(ref rightHand);
            UpdateRigElement(ref leftHand);

            UpdateRigElement(ref rightHandIk);
            UpdateRigElement(ref leftHandIk);

            UpdateRigElement(ref rightHandHint);
            UpdateRigElement(ref leftHandHint);

            UpdateRigElement(ref rightFoot);
            UpdateRigElement(ref leftFoot);

            UpdateRigElement(ref rightFootIk);
            UpdateRigElement(ref leftFootIk);
            
            UpdateRigElement(ref rightFootHint);
            UpdateRigElement(ref leftFootHint);
        }
#endif
    }
}