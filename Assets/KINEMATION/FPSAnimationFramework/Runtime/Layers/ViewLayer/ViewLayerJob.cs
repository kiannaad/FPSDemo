using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.ViewLayer
{
    /// <summary>
    /// 在 AnimationStream 中修改武器及左右手 IK 目标的视图构图层。
    ///
    /// 生命周期：
    /// 1. Initialize 在主线程把 Rig 元素绑定为 TransformStreamHandle；
    /// 2. UpdatePlayableJobData 每帧把最新 Layer 权重复制进 Job；
    /// 3. Unity 求值 PlayableGraph 时调用 ProcessAnimation，串行修改上一层产出的姿势。
    ///
    /// 本层只生成/校正 IK 目标，不求解真实手臂，通常需要放在 IkLayer 之前。
    /// </summary>
    public struct ViewLayerJob : IAnimationJob, IAnimationLayerJob
    {
        private ViewLayerSettings _settings;
        private LayerJobData _jobData;
        private TransformStreamHandle _weaponHandle;
        private TransformStreamHandle _rightHandIkHandle;
        private TransformStreamHandle _leftHandIkHandle;
        
        public void ProcessAnimation(AnimationStream stream)
        {
            // 权重为零时必须保持上一层姿势不变，也避免无意义的 Handle 读写。
            if (!KAnimationMath.IsWeightRelevant(_jobData.weight))
            {
                return;
            }
            
            // 三个修改按顺序作用在同一个 AnimationStream 上。
            // ModifyTransform 会根据 KPose.space 和 modifyMode 选择局部/组件/世界空间及 Add/Override 行为。
            AnimLayerJobUtility.ModifyTransform(stream, _jobData.rootHandle, _weaponHandle, _settings.ikHandGun, 
                _jobData.weight);
            AnimLayerJobUtility.ModifyTransform(stream, _jobData.rootHandle, _rightHandIkHandle, _settings.ikHandRight, 
                _jobData.weight);
            AnimLayerJobUtility.ModifyTransform(stream, _jobData.rootHandle, _leftHandIkHandle, _settings.ikHandLeft, 
                _jobData.weight);
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void Initialize(LayerJobData jobData, FPSAnimatorLayerSettings settings)
        {
            // Settings 是 ScriptableObject 配置；Job 保存强类型引用以便求值时读取 KPose。
            _settings = (ViewLayerSettings) settings;
            _jobData = jobData;

            // RigElement 先通过 KRigComponent 解析为实际 Transform，再绑定成可供 Animation Job 使用的 Handle。
            var transform = jobData.rigComponent.GetRigTransform(_settings.ikHandGun.element);
            _weaponHandle = jobData.animator.BindStreamTransform(transform);
            
            // 初始化阶段也把武器视图偏移应用到场景 Transform。
            // ADS 的初始对齐计算依赖这个已经校正过的武器参考姿势。
            KAnimationMath.ModifyTransform(jobData.Owner, transform, _settings.ikHandGun);
            
            transform = jobData.rigComponent.GetRigTransform(_settings.ikHandRight.element);
            _rightHandIkHandle = jobData.animator.BindStreamTransform(transform);
            
            transform = jobData.rigComponent.GetRigTransform(_settings.ikHandLeft.element);
            _leftHandIkHandle = jobData.animator.BindStreamTransform(transform);
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
            // AnimationScriptPlayable 中保存的是 Job struct 的副本。
            // 主线程修改本地 struct 后，必须 SetJobData 才能让下一次动画求值看到新权重。
            _jobData.weight = weight;
            playable.SetJobData(this);
        }
        
        public void LateUpdate()
        {
        }

        public void Destroy()
        {
        }
    }
}