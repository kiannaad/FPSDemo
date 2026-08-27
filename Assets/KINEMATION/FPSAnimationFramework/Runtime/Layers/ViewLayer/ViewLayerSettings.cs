// Designed by KINEMATION, 2024.

using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.ViewLayer
{
    /// <summary>
    /// 第一人称视图构图层的静态配置。
    /// 该层只偏移武器与左右手 IK 目标，不直接旋转真实手臂；后续 IkLayer 会消费这些目标。
    /// 每个 KPose 自己决定目标元素、坐标空间以及 Add/Override 模式。
    /// </summary>
    public class ViewLayerSettings : FPSAnimatorLayerSettings
    {
        // 武器 IK 目标的视图偏移，常用于调整武器在屏幕中的整体位置和朝向。
        public KPose ikHandGun = new KPose()
        {
            element = new KRigElement(-1, FPSANames.IkWeaponBone),
            pose = KTransform.Identity
        };
        
        // 右手 IK 目标的独立校正；Identity 表示不额外改变。
        public KPose ikHandRight = new KPose()
        {
            element = new KRigElement(-1, FPSANames.IkRightHand),
            pose = KTransform.Identity
        };
        
        // 左手 IK 目标的独立校正；通常用于配合武器构图修正握持关系。
        public KPose ikHandLeft = new KPose()
        {
            element = new KRigElement(-1, FPSANames.IkLeftHand),
            pose = KTransform.Identity
        };

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new ViewLayerJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            UpdateRigElement(ref ikHandGun.element);
            UpdateRigElement(ref ikHandRight.element);
            UpdateRigElement(ref ikHandLeft.element);
        }
#endif
    }
}