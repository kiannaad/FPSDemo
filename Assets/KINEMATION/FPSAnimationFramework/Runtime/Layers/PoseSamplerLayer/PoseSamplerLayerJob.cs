// Designed by KINEMATION, 2024.

using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.PoseSamplerLayer
{
    /// <summary>
    /// 程序化动画链的基准姿势采样层。
    /// 初始化时从动画资源提取持枪基准并缓存骨盆、脊柱和 WeaponBone 的空间关系；
    /// 每帧稳定脊柱、混合武器参考姿势，并生成后续 AttachHand、View、ADS、IK 消费的目标。
    /// </summary>
    public struct PoseSamplerJob : IAnimationJob, IAnimationLayerJob
    {
        private PoseSamplerLayerSettings _settings;

        private TransformStreamHandle _ikRightHand;
        private TransformStreamHandle _ikLeftHand;
        private TransformStreamHandle _ikRightHandHint;
        private TransformStreamHandle _ikLeftHandHint;

        private TransformStreamHandle _weaponBone;
        private TransformStreamHandle _weaponBoneRight;
        private TransformStreamHandle _weaponBoneLeft;
        private TransformStreamHandle _ikWeaponBone;

        private TransformStreamHandle _spineRootHandle;
        private TransformStreamHandle _hipHandle;
        private TransformStreamHandle _hipParentHandle;

        private Quaternion _cachedPelvisPose;

        private KTransform _weaponBoneComponentPose;
        private KTransform _weaponBoneSpinePose;

        private Transform _weaponBoneTransform;

        private int _stabilizationWeightIndex;
        private bool _overwriteRoot;

        private float _weaponBoneWeight;
        private float _stabilizationWeight;

        public LayerJobData _jobData;
        private bool _hasValidRoot;

        private void StabilizeSpine(in AnimationStream stream)
        {
            // Root 当前旋转 × 初始化时骨盆相对 Root 的旋转 = 不受局部摆动干扰的骨盆世界朝向。
            Quaternion rootRotation = _jobData.rootHandle.GetRotation(stream);
            Quaternion pelvisWorldRotation = rootRotation * _cachedPelvisPose;
            // 保留 SpineRoot 当前局部动画，再把它挂到稳定后的骨盆参考上。
            Quaternion stabilizedSpineRotation = pelvisWorldRotation * _spineRootHandle.GetLocalRotation(stream);

            Quaternion finalRotation = _spineRootHandle.GetRotation(stream);
            // 输入稳定权重与 Layer 总权重共同决定本层介入程度。
            finalRotation = Quaternion.Slerp(finalRotation, stabilizedSpineRotation,
                _stabilizationWeight * _jobData.weight);

            _spineRootHandle.SetRotation(stream, finalRotation);
        }

        private void PositionWeaponBone(in AnimationStream stream)
        {
            // Root 与 SpineRoot 是两套参考空间：前者随角色整体运动，后者还包含上半身动画。
            KTransform rootTransform = AnimLayerJobUtility.GetTransformFromHandle(stream, _jobData.rootHandle);
            KTransform spineRootTransform = AnimLayerJobUtility.GetTransformFromHandle(stream, _spineRootHandle);

            if (_weaponBoneWeight > 0f)
            {
                // 将同一缓存姿势分别还原到 Root/Spine 世界空间，两者之差即脊柱应追加给武器的增量。
                KTransform componentPose = rootTransform.GetWorldTransform(_weaponBoneComponentPose, false);
                KTransform spinePose = spineRootTransform.GetWorldTransform(_weaponBoneSpinePose, false);

                spinePose.position -= componentPose.position;
                spinePose.rotation = Quaternion.Inverse(componentPose.rotation) * spinePose.rotation;

                _weaponBone.SetPosition(stream, _weaponBone.GetPosition(stream) + spinePose.position);
                _weaponBone.SetRotation(stream, _weaponBone.GetRotation(stream) * spinePose.rotation);
            }

            // weaponBoneWeight 约定为 -1..1：负值混向 Left，正值混向 Center，0 使用 Right 参考。
            KTransform pose = AnimLayerJobUtility.GetTransformFromHandle(stream, _weaponBoneRight);
            KTransform poseRight = AnimLayerJobUtility.GetTransformFromHandle(stream, _weaponBone);
            KTransform poseLeft = AnimLayerJobUtility.GetTransformFromHandle(stream, _weaponBoneLeft);

            pose = _weaponBoneWeight >= 0f
                ? KTransform.Lerp(pose, poseRight, _weaponBoneWeight)
                : KTransform.Lerp(pose, poseLeft, -_weaponBoneWeight);

            // IK 手/肘目标可能随 IK WeaponBone 的父子关系移动；改武器目标前先缓存它们的世界姿势。
            KTransform cachedIkRightHand = AnimLayerJobUtility.GetTransformFromHandle(stream, _ikRightHand);
            KTransform cachedIkLeftHand = AnimLayerJobUtility.GetTransformFromHandle(stream, _ikLeftHand);
            KTransform cachedIkRightHandHint = AnimLayerJobUtility.GetTransformFromHandle(stream, _ikRightHandHint);
            KTransform cachedIkLeftHandHint = AnimLayerJobUtility.GetTransformFromHandle(stream, _ikLeftHandHint);

            _ikWeaponBone.SetPosition(stream, pose.position);
            _ikWeaponBone.SetRotation(stream, pose.rotation * _settings.weaponBoneOffset.rotation);

            // 恢复缓存的世界姿势，让本层只改变武器参考点，不隐式拖动下游的手/肘目标。
            _ikRightHand.SetPosition(stream, cachedIkRightHand.position);
            _ikRightHand.SetRotation(stream, cachedIkRightHand.rotation);

            _ikLeftHand.SetPosition(stream, cachedIkLeftHand.position);
            _ikLeftHand.SetRotation(stream, cachedIkLeftHand.rotation);

            _ikRightHandHint.SetPosition(stream, cachedIkRightHandHint.position);
            _ikRightHandHint.SetRotation(stream, cachedIkRightHandHint.rotation);

            _ikLeftHandHint.SetPosition(stream, cachedIkLeftHandHint.position);
            _ikLeftHandHint.SetRotation(stream, cachedIkLeftHandHint.rotation);
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            // Root 覆盖期间始终保存骨盆世界姿势，避免父节点变化把角色主体一起带走。
            KTransform localRoot = KTransform.Identity;
            KTransform worldPelvis = AnimLayerJobUtility.GetTransformFromHandle(stream, _hipHandle);

            if (_overwriteRoot && _hasValidRoot)
            {
                // 临时清空骨盆父节点局部姿势，使稳定和武器计算基于干净的 Root 参考。
                localRoot = AnimLayerJobUtility.GetTransformFromHandle(stream, _hipParentHandle, false);
                _hipParentHandle.SetLocalPosition(stream, Vector3.zero);
                _hipParentHandle.SetLocalRotation(stream, Quaternion.identity);
            }

            _hipHandle.SetPosition(stream, worldPelvis.position);
            _hipHandle.SetRotation(stream, worldPelvis.rotation);

            // 先稳定身体参考，再定位武器；反过来会让武器使用尚未稳定的 Spine 空间。
            StabilizeSpine(stream);
            PositionWeaponBone(stream);

            if (_overwriteRoot && _hasValidRoot)
            {
                // 恢复父节点局部姿势，并再次写回骨盆世界姿势以抵消层级连带变换。
                _hipParentHandle.SetLocalPosition(stream, localRoot.position);
                _hipParentHandle.SetLocalRotation(stream, localRoot.rotation);

                _hipHandle.SetPosition(stream, worldPelvis.position);
                _hipHandle.SetRotation(stream, worldPelvis.rotation);
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void Initialize(LayerJobData jobData, FPSAnimatorLayerSettings settings)
        {
            // Initialize 运行在主线程，只在 Profile 建链/重建时执行，不属于每帧动画求值。
            _settings = (PoseSamplerLayerSettings)settings;
            _jobData = jobData;

            Transform spineRoot = jobData.rigComponent.GetRigTransform(_settings.spineRoot);
            Transform pelvis = jobData.rigComponent.GetRigTransform(_settings.pelvis);
            Transform root = jobData.Owner;

            // 只有骨盆与 Animator 之间还有一层父节点时，overwriteRoot 才存在可覆盖对象。
            _hasValidRoot = pelvis.parent != jobData.animator.transform;

            _weaponBoneTransform = jobData.rigComponent.GetRigTransform(_settings.weaponBone);
            Transform weaponBoneRight = jobData.rigComponent.GetRigTransform(_settings.weaponBoneRight);
            Transform weaponBoneLeft = jobData.rigComponent.GetRigTransform(_settings.weaponBoneLeft);
            Transform ikWeaponBone = jobData.rigComponent.GetRigTransform(_settings.ikWeaponBone);

            Transform ikHandRight = jobData.rigComponent.GetRigTransform(_settings.ikHandRight);
            Transform ikHandLeft = jobData.rigComponent.GetRigTransform(_settings.ikHandLeft);
            Transform ikRightHandHint = jobData.rigComponent.GetRigTransform(_settings.ikHandRightHint);
            Transform ikLeftHandHint = jobData.rigComponent.GetRigTransform(_settings.ikHandLeftHint);

            // 一次性绑定求值所需的 StreamHandle，避免动画 Job 中遍历场景 Transform 层级。
            _spineRootHandle = jobData.animator.BindStreamTransform(spineRoot);
            _hipHandle = jobData.animator.BindStreamTransform(pelvis);

            if (_settings.overwriteRoot && _hasValidRoot)
            {
                _hipParentHandle = jobData.animator.BindStreamTransform(pelvis.parent);
            }

            _weaponBone = jobData.animator.BindStreamTransform(_weaponBoneTransform);
            _weaponBoneRight = jobData.animator.BindStreamTransform(weaponBoneRight);
            _weaponBoneLeft = jobData.animator.BindStreamTransform(weaponBoneLeft);

            _ikWeaponBone = jobData.animator.BindStreamTransform(ikWeaponBone);

            _ikRightHand = jobData.animator.BindStreamTransform(ikHandRight);
            _ikLeftHand = jobData.animator.BindStreamTransform(ikHandLeft);

            _ikRightHandHint = jobData.animator.BindStreamTransform(ikRightHandHint);
            _ikLeftHandHint = jobData.animator.BindStreamTransform(ikLeftHandHint);

            if (_settings.overwriteRoot && _hasValidRoot)
            {
                // 为采样基准姿势准备不带额外父级旋转的参考空间。
                pelvis.parent.localRotation = Quaternion.identity;
            }

            // 先写默认姿势，保证 Clip 没有 WeaponBone 轨道时仍有确定基准。
            _weaponBoneTransform.position = root.TransformPoint(_settings.defaultWeaponPose.position);
            _weaponBoneTransform.rotation = root.rotation * _settings.defaultWeaponPose.rotation;

            // 在场景对象上采样 Clip 第 0 帧，从中提取作者制作的骨盆、脊柱、手和武器姿势。
            _settings.poseToSample.clip.SampleAnimation(jobData.Owner.gameObject, 0f);

            // Clip 可能改动 Root 层级；保持骨盆世界姿势，避免只为取持枪 Pose 却移动整个角色。
            if (_settings.overwriteRoot && _hasValidRoot)
            {
                KTransform pelvisCache = new KTransform(pelvis);
                pelvis.parent.localRotation = Quaternion.identity;
                pelvis.position = pelvisCache.position;
                pelvis.rotation = pelvisCache.rotation;
            }

            if (_settings.overwriteWeaponBone)
            {
                // 明确要求忽略 Clip 的 WeaponBone 时，再次写回默认组件空间姿势。
                _weaponBoneTransform.position = root.TransformPoint(_settings.defaultWeaponPose.position);
                _weaponBoneTransform.rotation = root.rotation * _settings.defaultWeaponPose.rotation;
            }

            // 初始化左右参考点；后续动画可继续驱动它们，再由曲线选择最终武器参考姿势。
            weaponBoneRight.position = _weaponBoneTransform.position;
            weaponBoneRight.rotation = _weaponBoneTransform.rotation;

            weaponBoneLeft.position = weaponBoneRight.position;
            weaponBoneLeft.rotation = weaponBoneRight.rotation;

            // ReSharper disable all
            // 缓存相对关系而非绝对世界坐标，角色整体移动/旋转后仍可复用。
            _cachedPelvisPose = Quaternion.Inverse(root.rotation) * pelvis.rotation;

            _weaponBoneComponentPose =
                new KTransform(root).GetRelativeTransform(new KTransform(_weaponBoneTransform), false);

            _weaponBoneSpinePose =
                new KTransform(spineRoot).GetRelativeTransform(new KTransform(_weaponBoneTransform), false);

            // 模型轴向校正追加到最终武器参考，不改变前面缓存的原始空间关系。
            _weaponBoneTransform.rotation *= _settings.weaponBoneOffset.rotation;

            ikWeaponBone.position = _weaponBoneTransform.position;
            ikWeaponBone.rotation = _weaponBoneTransform.rotation;

            // 静态采样用于建立缓存；PlayPose 和 AvatarMask 则把同一 Pose 接入正常 Playable 链。
            _jobData.playablesController.PlayPose(_settings.poseToSample);
            _jobData.playablesController.UpdateAvatarMask(_settings.poseToSample.mask);
            _stabilizationWeightIndex = _jobData.inputController.GetPropertyIndex(_settings.stabilizationWeight);
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

        public void OnGameThreadUpdate()
        {
            _weaponBoneTransform.position = _jobData.Owner.TransformPoint(_settings.defaultWeaponPose.position);
            _weaponBoneTransform.rotation = _jobData.Owner.transform.rotation * _settings.defaultWeaponPose.rotation;

            _overwriteRoot = _settings.overwriteRoot;
            _weaponBoneWeight = _jobData.playablesController.GetCurveValue(_settings.weaponBoneWeight);
            _stabilizationWeight = _jobData.inputController.GetValue<float>(_stabilizationWeightIndex);
            Debug.Log($"weaponBoneWeight : {_weaponBoneWeight}, stabilizationWeight : {_stabilizationWeight}");
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            // 动画求值线程不能在这里直接访问主线程对象；先读取动态值，再复制进 Job struct。
            _jobData.weight = weight;

            _weaponBoneTransform.position = _jobData.Owner.TransformPoint(_settings.defaultWeaponPose.position);
            _weaponBoneTransform.rotation = _jobData.Owner.transform.rotation * _settings.defaultWeaponPose.rotation;

            _overwriteRoot = _settings.overwriteRoot;
            // 动画曲线选择武器参考姿势，输入属性控制脊柱稳定程度。
            _weaponBoneWeight = _jobData.playablesController.GetCurveValue(_settings.weaponBoneWeight);
            _stabilizationWeight = _jobData.inputController.GetValue<float>(_stabilizationWeightIndex);

            // AnimationScriptPlayable 持有 struct 副本，SetJobData 是动态数据进入求值阶段的同步点。
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
