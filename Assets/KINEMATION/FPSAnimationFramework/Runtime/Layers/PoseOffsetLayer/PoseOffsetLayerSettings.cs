// Designed by KINEMATION, 2024.

using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Attributes;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;

using System;
using System.Collections.Generic;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseOffsetLayer
{
    /// <summary>
    /// 描述一次针对单个 Rig 元素的姿势修改。
    /// KPose 同时保存目标骨骼、位移/旋转、坐标空间以及 Add/Override 修改模式。
    /// </summary>
    [Serializable]
    public struct PoseOffset
    {
        [Unfold] public KPose pose;
        // 注意：当前 PoseOffsetJob 尚未读取 blend，实际只使用 Layer 的总 weight。
        [CurveSelector] public CurveBlend blend;
        // 注意：当前 PoseOffsetJob 尚未读取 keepChildrenPose，不要仅凭字段名推断子骨骼会被补偿。
        public bool keepChildrenPose;
    }
    
    /// <summary>
    /// 通用姿势偏移层的静态配置。
    /// 它按列表顺序把多个 KPose 应用到 AnimationStream，适合做固定校准或简单的局部姿势修正。
    /// </summary>
    public class PoseOffsetLayerSettings : FPSAnimatorLayerSettings
    {
        // 列表顺序可能影响结果：后一个偏移读取的是前一个偏移已经修改过的姿势。
        public List<PoseOffset> poseOffsets = new List<PoseOffset>();

        public override IAnimationLayerJob CreateAnimationJob()
        {
            return new PoseOffsetJob();
        }

#if UNITY_EDITOR
        public override void OnRigUpdated()
        {
            int count = poseOffsets.Count;

            for (int i = 0; i < count; i++)
            {
                PoseOffset item = poseOffsets[i];
                UpdateRigElement(ref item.pose.element);
                poseOffsets[i] = item;
            }
        }
#endif
    }
}