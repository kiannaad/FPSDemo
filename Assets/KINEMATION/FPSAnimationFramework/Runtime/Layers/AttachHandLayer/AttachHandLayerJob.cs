using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;

using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.AttachHandLayer
{
    /// <summary>
    /// 缓存手部骨骼链中一个元素的流句柄和目标局部姿势。
    /// handle 用来访问每帧 AnimationStream，pose 则是在初始化阶段从基础/自定义握姿中提取的快照。
    /// </summary>
    public struct LeftHandPose
    {
        public TransformStreamHandle handle;
        public KTransform pose;
    }
    
    /// <summary>
    /// 根据武器姿势生成手部 IK 目标，并把手掌/手指链混合到缓存握姿。
    ///
    /// 初始化时缓存“真实 HandBone 相对于真实 WeaponBone”的变换；运行时把该相对变换
    /// 挂到当前 IK WeaponBone 上，得到新的 IK HandBone 世界姿势。后续 IkLayer 再让真实手臂抵达该目标。
    /// </summary>
    public struct AttachHandLayerJob : IAnimationJob, IAnimationLayerJob
    {
        private AttachHandLayerSettings _settings;
        
        private Transform _handBone;
        
        private Transform _weaponBone;
        private KTransform _handPose;
        
        //Animation Job
        private TransformStreamHandle _ikHandBoneHandle;
        private TransformStreamHandle _weaponBoneHandle;
        
        private LayerJobData _jobData;
        private NativeArray<LeftHandPose> _leftHandChain;

        private void OnInitialized(FPSAnimatorLayerSettings settings)
        {
            _settings = (AttachHandLayerSettings) settings;
            
            _handBone = _jobData.rigComponent.GetRigTransform(_settings.handBone);
            _weaponBone = _jobData.rigComponent.GetRigTransform(_settings.weaponBone);

            var ikWeaponBone = _jobData.rigComponent.GetRigTransform(_settings.ikWeaponBone);
            var ikHandBone = _jobData.rigComponent.GetRigTransform(_settings.ikHandBone);

            _weaponBoneHandle = _jobData.animator.BindStreamTransform(ikWeaponBone);
            _ikHandBoneHandle = _jobData.animator.BindStreamTransform(ikHandBone);

            // 如果配置了附件专用握姿，先临时采样第 0 帧，再从采样结果提取相对姿势和手指局部旋转。
            bool hasValidCustomPose = _settings.customHandPose != null;
            
            if (hasValidCustomPose)
            {
                _jobData.rigComponent.CacheHierarchyPose();
                _settings.customHandPose.SampleAnimation(_jobData.Owner.gameObject, 0f);
            }
            
            // 保存 HandBone 在 WeaponBone 空间中的相对姿势。
            // 运行时 WeaponBone 无论怎样移动，都能用这份局部关系重建手部目标。
            _handPose = new KTransform(_weaponBone).GetRelativeTransform(new KTransform(_handBone), false);
            
            var chain = _settings.rigAsset.GetPopulatedChain(_settings.elementChainName, _jobData.rigComponent);

            if (_leftHandChain.IsCreated) _leftHandChain.Dispose();
            
            // 缓存整条手部链，避免在动画求值线程中访问 Transform 层级或分配集合。
            _leftHandChain = new NativeArray<LeftHandPose>(chain.transformChain.Count, Allocator.Persistent);

            for (int i = 0; i < chain.transformChain.Count; i++)
            {
                _leftHandChain[i] = new LeftHandPose()
                {
                    handle = _jobData.animator.BindStreamTransform(chain.transformChain[i]),
                    pose = new KTransform(chain.transformChain[i], false)
                };
            }

            if (hasValidCustomPose)
            {
                // 把采样后的自定义握姿重新写入 KRigComponent 缓存。
                // 注意：这里调用的是 CacheHierarchyPose 而不是 ApplyHierarchyCachedPose，因此不会恢复采样前姿势。
                _jobData.rigComponent.CacheHierarchyPose();
            }
        }
        
        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KAnimationMath.IsWeightRelevant(_jobData.weight))
            {
                return;
            }

            // 读取的是前面所有 Layer 已经处理过的 IK WeaponBone，而不是初始化时的场景 Transform。
            KTransform weaponBone = AnimLayerJobUtility.GetTransformFromHandle(stream, _weaponBoneHandle);
            
            KTransform attachedPose = new KTransform();
            
            // 当前武器世界姿势 × 缓存的手部相对姿势 × 作者配置偏移 = 手部 IK 目标。
            attachedPose.position 
                = weaponBone.TransformPoint(_handPose.position + _settings.handPoseOffset.position, false);
            attachedPose.rotation = weaponBone.rotation * (_handPose.rotation * _settings.handPoseOffset.rotation);

            // 按 Layer 总权重混合目标。权重小于 1 时保留部分上一层的 IK 手部姿势。
            KTransform handBone = AnimLayerJobUtility.GetTransformFromHandle(stream, _ikHandBoneHandle);
            handBone.position = Vector3.Lerp(handBone.position, attachedPose.position, _jobData.weight);
            handBone.rotation = Quaternion.Slerp(handBone.rotation, attachedPose.rotation, _jobData.weight);
            
            _ikHandBoneHandle.SetPosition(stream, handBone.position);
            _ikHandBoneHandle.SetRotation(stream, handBone.rotation);

            // IK 只能决定手腕位置/朝向；手掌和手指的握姿需要在这里单独混合局部旋转。
            foreach (var item in _leftHandChain)
            {
                Quaternion rotation = item.handle.GetLocalRotation(stream);
                rotation = Quaternion.Slerp(rotation, item.pose.rotation, _jobData.weight);
                item.handle.SetLocalRotation(stream, rotation);
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void Initialize(LayerJobData newJobData, FPSAnimatorLayerSettings settings)
        {
            this._jobData = newJobData;
            OnInitialized(settings);
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
            // 动态链接同类型 Settings 时重新采样相对姿势并重建 NativeArray。
            OnInitialized(newSettings);
        }

        public void UpdateEntity(FPSAnimatorEntity newEntity)
        {
        }
        
        public void OnPreGameThreadUpdate()
        {
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            // 将主线程算出的曲线/Alpha 合成权重复制到动画 Job。
            _jobData.weight = weight;
            playable.SetJobData(this);
        }
        
        public void LateUpdate()
        {
        }

        public void Destroy()
        {
            // OnInitialized 可能多次重建数组，因此重建和最终销毁路径都必须释放旧数据。
            if (_leftHandChain.IsCreated) _leftHandChain.Dispose();
        }
    }
}