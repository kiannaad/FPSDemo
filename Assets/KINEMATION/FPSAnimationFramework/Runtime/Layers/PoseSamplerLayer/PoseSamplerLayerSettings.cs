// Designed by KINEMATION, 2024.

using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.Shared.KAnimationCore.Runtime.Attributes;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseSamplerLayer
{
    /// <summary>
    /// PoseSamplerJob 的静态配置。
    ///
    /// 这一层位于程序化动画链的前段：先从 poseToSample 提取一套武器/骨架基准姿势，
    /// 再把它转换为后续 AttachHand、View、ADS、IK 等层可以继续修改的 IK 目标。
    /// Settings 只描述“使用什么资源、骨骼和曲线”；真正的每帧计算在 PoseSamplerJob 中完成。
    /// </summary>
    public class PoseSamplerLayerSettings : FPSAnimatorLayerSettings
    {
        [Header("General")]
        // 用于提取持枪基准姿势的动画资源。初始化时会采样第 0 帧，同时交给 Playables 播放。
        public FPSAnimationAsset poseToSample;
        // 当采样动画没有提供合适的 WeaponBone 时使用的组件空间默认姿势。
        public KTransform defaultWeaponPose = KTransform.Identity;
        // 在采样结果之上附加的武器旋转校正，常用于修正模型轴向差异。
        public KTransform weaponBoneOffset = KTransform.Identity;
        // 是否在采样时临时清除骨盆父节点姿势，避免动画 Root 影响程序化持枪基准。
        public bool overwriteRoot = false;
        // 是否强制用 defaultWeaponPose 覆盖动画中采样到的 WeaponBone。
        public bool overwriteWeaponBone = false;
        
        [Header("IK Targets")]
        // 后续层实际操作的是这些 IK 目标，而不是直接改左右手臂骨骼。
        public KRigElement ikWeaponBone = new KRigElement(-1, FPSANames.IkWeaponBone);
        public KRigElement ikHandRight = new KRigElement(-1, FPSANames.IkRightHand);
        public KRigElement ikHandLeft = new KRigElement(-1, FPSANames.IkLeftHand);
        
        public KRigElement ikHandRightHint = new KRigElement(-1, FPSANames.IkRightElbow);
        public KRigElement ikHandLeftHint = new KRigElement(-1, FPSANames.IkLeftElbow);
        
        [Header("Weapon Bone")]
        // Right/Center/Left 三个参考点由动画曲线在运行时混合，用来决定最终 IK WeaponBone。
        public KRigElement weaponBoneRight = new KRigElement(-1, FPSANames.IkWeaponBoneRight);
        public KRigElement weaponBoneLeft = new KRigElement(-1, FPSANames.IkWeaponBoneLeft);
        public KRigElement weaponBone = new KRigElement(-1, FPSANames.WeaponBone);
        
        [Header("Spine")]
        // pelvis 用于保存世界姿势和处理 Root 覆盖；spineRoot 用于上半身稳定以及武器空间换算。
        public KRigElement pelvis;
        public KRigElement spineRoot;
        
        [Header("Input Properties")]
        [CurveSelector(false, false)]
        // 来自输入属性系统：控制脊柱保持相对稳定的程度。
        public string stabilizationWeight = FPSANames.StabilizationWeight;
        [CurveSelector(false, true, false)]
        // 来自动画/Playable 曲线：通常取值 -1..1，在 Left/Center/Right 武器参考姿势间选择。
        public string weaponBoneWeight = FPSANames.Curve_WeaponBoneWeight;

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new PoseSamplerJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            UpdateRigElement(ref ikWeaponBone);
            UpdateRigElement(ref ikHandRight);
            UpdateRigElement(ref ikHandLeft);
            
            UpdateRigElement(ref ikHandRightHint);
            UpdateRigElement(ref ikHandLeftHint);
            
            UpdateRigElement(ref weaponBoneRight);
            UpdateRigElement(ref weaponBoneLeft);
            UpdateRigElement(ref weaponBone);
            
            UpdateRigElement(ref pelvis);
            UpdateRigElement(ref spineRoot);
        }
#endif
    }
}