using KINEMATION.FPSAnimationFramework.Runtime.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseOffsetLayer
{
    /// <summary>
    /// 通用的多骨骼姿势偏移 Job。
    /// Initialize 只做一次骨骼解析和 Handle 绑定；ProcessAnimation 每帧按配置顺序应用 KPose。
    /// 它不拥有独立状态机，也不求解 IK，职责只是对上一层的 AnimationStream 做确定性的姿势修改。
    /// </summary>
    public struct PoseOffsetJob : IAnimationJob, IAnimationLayerJob
    {
        private PoseOffsetLayerSettings _settings;
        
        // Animation Job
        private LayerJobData _jobData;
        private NativeArray<TransformStreamHandle> _offsetBones;
        
        public void ProcessAnimation(AnimationStream stream)
        {
            // 总 Layer 权重为零时，本层等价于透明传递。
            if (Mathf.Approximately(_jobData.weight, 0f))
            {
                return;
            }
            
            int count = _offsetBones.Length;
            for (int i = 0; i < count; i++)
            {
                // poseOffsets 与 _offsetBones 使用相同索引，必须保持数量和顺序一致。
                // 后一个 KPose 会读到前一个 KPose 已经修改过的流中姿势。
                var poseOffset = _settings.poseOffsets[i];
                AnimLayerJobUtility.ModifyTransform(stream, _jobData.rootHandle, _offsetBones[i], 
                    poseOffset.pose, _jobData.weight);
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void Initialize(LayerJobData jobData, FPSAnimatorLayerSettings settings)
        {
            _settings = (PoseOffsetLayerSettings) settings;
            _jobData = jobData;

            // NativeArray 在 Job 生命周期内长期复用，避免每帧查找 Transform 或产生托管分配。
            int count = _settings.poseOffsets.Count;
            _offsetBones = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);

            for (int i = 0; i < count; i++)
            {
                var transform = _jobData.rigComponent.GetRigTransform(_settings.poseOffsets[i].pose.element);
                // TransformStreamHandle 是当前 Animator 动画流中的骨骼句柄，不是直接操作场景 Transform。
                _offsetBones[i] = _jobData.animator.BindStreamTransform(transform);
            }
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            return AnimationScriptPlayable.Create(graph, this);
        }

        public FPSAnimatorLayerSettings GetSettingAsset()
        {
            return _settings;
        }

        public void OnLayerLinked(FPSAnimatorLayerSettings newSettings)
        {
        }

        public void UpdateEntity(FPSAnimatorEntity newEntity)
        {
        }
        
        public void OnPreGameThreadUpdate()
        {
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            // 当前实现只使用 Layer 总权重；PoseOffset.blend 与 keepChildrenPose 尚未在本 Job 中消费。
            _jobData.weight = weight;
            playable.SetJobData(this);
        }
        
        public void LateUpdate()
        {
        }

        public void Destroy()
        {
            // Persistent NativeArray 不会自动释放，Layer 被重建或 Controller 销毁时必须 Dispose。
            if (_offsetBones.IsCreated) _offsetBones.Dispose();
        }
    }
}