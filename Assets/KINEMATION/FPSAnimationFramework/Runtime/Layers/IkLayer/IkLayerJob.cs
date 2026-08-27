using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Core;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KINEMATION.FPSAnimationFramework.Runtime.Layers.IkLayer
{
    /// <summary>
    /// 一条双骨骼 IK 链的运行时句柄集合。
    /// root-mid-tip 是被求解的真实骨骼，target 是末端目标，hint 决定肘/膝弯曲平面。
    /// </summary>
    public struct IkHandle
    {
        public TransformStreamHandle root;
        public TransformStreamHandle mid;
        public TransformStreamHandle tip;
        
        public TransformStreamHandle target;
        public TransformStreamHandle hint;

        public KTwoBoneIkData ikData;

        public IkHandle(Animator animator, Transform tip, Transform target, Transform hint)
        {
            // 约定 tip 的父节点为 mid、祖父节点为 root，因此传入骨骼必须处在标准三段层级中。
            var midBone = tip.parent;
            
            this.tip = animator.BindStreamTransform(tip);
            this.mid = animator.BindStreamTransform(midBone);
            this.root = animator.BindStreamTransform(midBone.parent);
            
            this.target = animator.BindStreamTransform(target);
            this.hint = animator.BindStreamTransform(hint);

            ikData = new KTwoBoneIkData()
            {
                // 默认 target、hint 均有效，位置、旋转和提示权重全部参与求解。
                hasValidHint = true,
                hintWeight = 1f,
                rotWeight = 1f,
                posWeight = 1f,
            };
        }

        public void OnProcessIK(AnimationStream stream, float w)
        {
            if (Mathf.Approximately(w, 0f)) return;
            
            // 每帧从 AnimationStream 读取当前姿势，因为前面的动画和 Layer 可能已经修改这些骨骼。
            ikData.tip = AnimLayerJobUtility.GetTransformFromHandle(stream, tip);
            ikData.mid = AnimLayerJobUtility.GetTransformFromHandle(stream, mid);
            ikData.root = AnimLayerJobUtility.GetTransformFromHandle(stream,root);
            
            ikData.target = AnimLayerJobUtility.GetTransformFromHandle(stream,target);
            ikData.hint = AnimLayerJobUtility.GetTransformFromHandle(stream, hint);

            // 末端已经位于目标时无需再次求解，也避免零长度方向带来的数值问题。
            if (ikData.tip.Equals(ikData.target)) return;
            
            // Solver 只计算结果；真正写回 AnimationStream 的责任保留在本结构中。
            KTwoBoneIK.Solve(ref ikData);
            
            // 只写 root/mid/tip 的旋转，骨骼位置由层级和骨长自然传递。
            root.SetRotation(stream, Quaternion.Slerp(root.GetRotation(stream), ikData.root.rotation, w));
            mid.SetRotation(stream, Quaternion.Slerp(mid.GetRotation(stream), ikData.mid.rotation, w));
            tip.SetRotation(stream, Quaternion.Slerp(tip.GetRotation(stream), ikData.tip.rotation, w));
        }
    }
    
    /// <summary>
    /// 手脚双骨骼 IK 的最终求解层。
    /// 前序 Layer 构造 target/hint，本层消费目标并把结果写入真实肢体骨骼。
    /// 因为它写的是最终骨骼旋转，通常应放在所有目标生成和偏移层之后。
    /// </summary>
    public struct IkLayerJob : IAnimationJob, IAnimationLayerJob
    {
        private IkLayerSettings _settings;
        private LayerJobData _jobData;

        private IkHandle _rightHandHandle;
        private IkHandle _leftHandHandle;
        
        private IkHandle _rightFootHandle;
        private IkHandle _leftFootHandle;

        private float _turnOffset;
        private int _turnProperty;

        private void SetupIkHandle(out IkHandle handle, KRigElement tip, KRigElement target, KRigElement hint)
        {
            // KRigElement 是可序列化的逻辑引用；KRigComponent 将其解析为当前角色的实际 Transform。
            var handBone = _jobData.rigComponent.GetRigTransform(tip);
            var targetBone = _jobData.rigComponent.GetRigTransform(target);
            var hintBone = _jobData.rigComponent.GetRigTransform(hint);

            if (handBone == null) Debug.LogError($"IK Layer: couldn't find {tip.name}.");
            if (targetBone == null) Debug.LogError($"IK Layer: couldn't find {target.name}.");
            if (hintBone == null) Debug.LogError($"IK Layer: couldn't find {hint.name}.");

            // 当前实现只记录缺失引用，没有提前返回；完整、正确的 Rig 配置是本层的前置条件。
            handle = new IkHandle(_jobData.animator, handBone, targetBone, hintBone);
        }
        
        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KAnimationMath.IsWeightRelevant(_jobData.weight))
            {
                return;
            }
            
            // 单肢体权重与 Layer 总权重相乘，让 Profile Alpha/曲线遮罩可以统一控制整层。
            _rightHandHandle.OnProcessIK(stream, _jobData.weight * _settings.rightHandWeight);
            _leftHandHandle.OnProcessIK(stream, _jobData.weight * _settings.leftHandWeight);
            
            if (stream.isHumanStream)
            {
                // Humanoid 足部走 AnimationHumanStream Goal；手部仍使用上面的通用双骨骼 Solver。
                var humanStream = stream.AsHuman();

                KTransform rootTransform = AnimLayerJobUtility.GetTransformFromHandle(stream, _jobData.rootHandle);
                KTransform rightFootGoal = AnimLayerJobUtility.GetTransformFromHandle(stream, _rightFootHandle.target);
                KTransform leftFootGoal = AnimLayerJobUtility.GetTransformFromHandle(stream, _leftFootHandle.target);
                
                // 先转到角色 Root 空间，施加可选转身补偿，再转换回世界空间。
                rightFootGoal = rootTransform.GetRelativeTransform(rightFootGoal, false);
                leftFootGoal = rootTransform.GetRelativeTransform(leftFootGoal, false);

                if (_settings.offsetFeetTargets)
                {
                    rootTransform.rotation *= Quaternion.Euler(0f, -_turnOffset, 0f);
                }
                
                rightFootGoal = rootTransform.GetWorldTransform(rightFootGoal, false);
                leftFootGoal = rootTransform.GetWorldTransform(leftFootGoal, false);
                
                // Goal 在 HumanStream 内使用满权重；整层关闭由前面的总权重早退控制。
                humanStream.SetGoalWeightPosition(AvatarIKGoal.RightFoot, 1f);
                humanStream.SetGoalPosition(AvatarIKGoal.RightFoot, rightFootGoal.position);
                
                humanStream.SetGoalWeightRotation(AvatarIKGoal.RightFoot, 1f);
                humanStream.SetGoalRotation(AvatarIKGoal.RightFoot, 
                    humanStream.GetGoalRotationFromPose(AvatarIKGoal.RightFoot));
                
                humanStream.SetGoalWeightPosition(AvatarIKGoal.LeftFoot, 1f);
                humanStream.SetGoalPosition(AvatarIKGoal.LeftFoot, leftFootGoal.position);
                
                humanStream.SetGoalWeightRotation(AvatarIKGoal.LeftFoot, 1f);
                humanStream.SetGoalRotation(AvatarIKGoal.LeftFoot, 
                    humanStream.GetGoalRotationFromPose(AvatarIKGoal.LeftFoot));
                return;
            }
            
            // Generic Rig 没有 HumanStream，使用与手臂相同的显式双骨骼求解。
            _rightFootHandle.OnProcessIK(stream, _jobData.weight * _settings.rightFootWeight);
            _leftFootHandle.OnProcessIK(stream, _jobData.weight * _settings.leftFootWeight);
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void Initialize(LayerJobData jobData, FPSAnimatorLayerSettings settings)
        {
            _settings = (IkLayerSettings) settings;
            _jobData = jobData;

            if (jobData.animator.isHuman)
            {
                // TurnOffset 只供 Humanoid 脚目标补偿使用，缓存索引可避免每帧字符串查找。
                _turnProperty = jobData.inputController.GetPropertyIndex(FPSANames.TurnOffset);
            }

            SetupIkHandle(out _rightHandHandle, _settings.rightHand, _settings.rightHandIk, 
                _settings.rightHandHint);
            SetupIkHandle(out _leftHandHandle, _settings.leftHand, _settings.leftHandIk, 
                _settings.leftHandHint);
            SetupIkHandle(out _rightFootHandle, _settings.rightFoot, _settings.rightFootIk, 
                _settings.rightFootHint);
            SetupIkHandle(out _leftFootHandle, _settings.leftFoot, _settings.leftFootIk, 
                _settings.leftFootHint);
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
            _jobData.weight = weight;

            if (_jobData.animator.isHuman)
            {
                // 动态输入先在主线程读取，再通过 SetJobData 复制给动画求值 Job。
                _turnOffset = _jobData.inputController.GetValue<float>(_turnProperty);
            }
            
            // AnimationScriptPlayable 持有 struct 副本，必须显式同步本帧数据。
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