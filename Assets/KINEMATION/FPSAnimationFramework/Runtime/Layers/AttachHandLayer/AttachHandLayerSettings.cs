// Designed by KINEMATION, 2024.

using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Attributes;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEngine;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.AttachHandLayer
{
    /// <summary>
    /// AttachHandLayerJob 的静态配置。
    /// 该层记录“手相对于武器”的姿势，并在运行时把这个关系重建到 IK 手部目标上。
    /// 它负责生成手部目标和握持姿势，不负责求解上臂/前臂；最终骨骼求解由 IkLayer 完成。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttachHandLayer", menuName = FPSANames.FileMenuLayers + "Attach Hand")]
    public class AttachHandLayerSettings : FPSAnimatorLayerSettings
    {
        [Tooltip("Use this for attachments, e.g. grips.")]
        // 可选的自定义握姿。初始化时采样第 0 帧，用于提取手掌/手指链的局部旋转。
        public AnimationClip customHandPose;
        // 用来计算相对姿势的真实手骨骼。
        public KRigElement handBone;
        // 运行时被移动到武器握点的 IK 手部目标。
        public KRigElement ikHandBone;
        // 已经过 PoseSampler/View/ADS 等层处理的武器 IK 参考点。
        public KRigElement ikWeaponBone = new KRigElement(-1, FPSANames.IkWeaponBone);
        // 初始化时用于计算 handBone 相对关系的真实 WeaponBone。
        public KRigElement weaponBone = new KRigElement(-1, FPSANames.WeaponBone);
        // 需要恢复自定义局部旋转的手部骨骼链，通常包含手掌和手指。
        [ElementChainSelector] public string elementChainName;
        
        // 在缓存的“手相对武器”姿势之上增加校准偏移。
        public KTransform handPoseOffset = KTransform.Identity;
        // 当前 AttachHandLayerJob 未读取该字段；不要把它当作已经生效的运行时混合权重。
        [Range(0f, 1f)] public float overridePoseWeight = 0f;

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new AttachHandLayerJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            UpdateRigElement(ref handBone);
            UpdateRigElement(ref ikHandBone);
            UpdateRigElement(ref ikWeaponBone);
            UpdateRigElement(ref weaponBone);
        }
#endif
    }
}