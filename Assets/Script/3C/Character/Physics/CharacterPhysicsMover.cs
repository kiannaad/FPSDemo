using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 表示一个 CharacterPhysicsMover 在模拟中所需的完整状态。
    /// 可用于保存状态或回退到过去的状态
    /// </summary>
    [System.Serializable]
    public struct CharacterPhysicsMoverState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }

    /// <summary>
    /// 负责管理可移动运动学刚体的运动，
    /// 以便与角色进行正确交互
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterPhysicsMover : MonoBehaviour
    {
        /// <summary>
        /// 该 mover 对应的 Rigidbody
        /// </summary>
        [ReadOnly]
        public Rigidbody Rigidbody;

        /// <summary>
        /// 决定平台是通过 rigidbody.MovePosition 移动（true），还是直接修改 rigidbody.position（false）
        /// </summary>
        public bool MoveWithPhysics = true;

        /// <summary>
        /// 该对象在 CharacterPhysicsSystem 数组中的索引
        /// </summary>
        [NonSerialized]
        public IPhysicsMoverController MoverController;
        /// <summary>
        /// 记录插值过程中最近一次的位置
        /// </summary>
        [NonSerialized]
        public Vector3 LatestInterpolationPosition;
        /// <summary>
        /// 记录插值过程中最近一次的旋转
        /// </summary>
        [NonSerialized]
        public Quaternion LatestInterpolationRotation;
        /// <summary>
        /// 插值产生的最近一次位移
        /// </summary>
        [NonSerialized]
        public Vector3 PositionDeltaFromInterpolation;
        /// <summary>
        /// 插值产生的最近一次旋转
        /// </summary>
        [NonSerialized]
        public Quaternion RotationDeltaFromInterpolation;

        /// <summary>
        /// 该对象在 CharacterPhysicsSystem 数组中的索引
        /// </summary>
        public int IndexInCharacterSystem { get; set; }
        /// <summary>
        /// 记录整次模拟开始前的初始位置
        /// </summary>
        public Vector3 Velocity { get; protected set; }
        /// <summary>
        /// 记录整次模拟开始前的初始位置
        /// </summary>
        public Vector3 AngularVelocity { get; protected set; }
        /// <summary>
        /// 记录整次模拟开始前的初始位置
        /// </summary>
        public Vector3 InitialTickPosition { get; set; }
        /// <summary>
        /// 记录整次模拟开始前的初始旋转
        /// </summary>
        public Quaternion InitialTickRotation { get; set; }

        /// <summary>
        /// 该 mover 的 Transform
        /// </summary>
        public Transform Transform { get; private set; }
        /// <summary>
        /// 移动计算开始前的位置
        /// </summary>
        public Vector3 InitialSimulationPosition { get; private set; }
        /// <summary>
        /// 移动计算开始前的旋转
        /// </summary>
        public Quaternion InitialSimulationRotation { get; private set; }

        private Vector3 _internalTransientPosition;

        /// <summary>
        /// mover 的旋转（在角色更新阶段始终保持最新）
        /// </summary>
        public Vector3 TransientPosition
        {
            get
            {
                return _internalTransientPosition;
            }
            private set
            {
                _internalTransientPosition = value;
            }
        }

        private Quaternion _internalTransientRotation;
        /// <summary>
        /// mover 的旋转（在角色更新阶段始终保持最新）
        /// </summary>
        public Quaternion TransientRotation
        {
            get
            {
                return _internalTransientRotation;
            }
            private set
            {
                _internalTransientRotation = value;
            }
        }


        private void Reset()
        {
            ValidateData();
        }

        private void OnValidate()
        {
            ValidateData();
        }

        /// <summary>
        /// 校验并修正所有必需数据
        /// </summary>
        public void ValidateData()
        {
            Rigidbody = gameObject.GetComponent<Rigidbody>();

            Rigidbody.centerOfMass = Vector3.zero;
            Rigidbody.maxAngularVelocity = Mathf.Infinity;
            Rigidbody.maxDepenetrationVelocity = Mathf.Infinity;
            Rigidbody.isKinematic = true;
            Rigidbody.interpolation = RigidbodyInterpolation.None;
        }

        private IPhysicsRegistration physicsRegistration;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ICharacterPhysicsWorld physicsWorld = World.Current?
                .GetSubSystem<CharacterPhysicsSubSystem>()?
                .PhysicsWorld;
            physicsRegistration = physicsWorld?.Register(this)
                ?? throw new InvalidOperationException("Character physics world is not initialized.");
        }

        private void OnDisable()
        {
            physicsRegistration?.Dispose();
            physicsRegistration = null;
        }

        private void Awake()
        {
            Transform = this.transform;
            ValidateData();

            TransientPosition = Rigidbody.position;
            TransientRotation = Rigidbody.rotation;
            InitialSimulationPosition = Rigidbody.position;
            InitialSimulationRotation = Rigidbody.rotation;
            LatestInterpolationPosition = Transform.position;
            LatestInterpolationRotation = Transform.rotation;
        }

        /// <summary>
        /// 直接设置 mover 位置
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            Transform.position = position;
            Rigidbody.position = position;
            InitialSimulationPosition = position;
            TransientPosition = position;
        }

        /// <summary>
        /// 直接设置 mover 旋转
        /// </summary>
        public void SetRotation(Quaternion rotation)
        {
            Transform.rotation = rotation;
            Rigidbody.rotation = rotation;
            InitialSimulationRotation = rotation;
            TransientRotation = rotation;
        }

        /// <summary>
        /// 直接设置 mover 的位置和旋转
        /// </summary>
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            Transform.SetPositionAndRotation(position, rotation);
            Rigidbody.position = position;
            Rigidbody.rotation = rotation;
            InitialSimulationPosition = position;
            InitialSimulationRotation = rotation;
            TransientPosition = position;
            TransientRotation = rotation;
        }

        /// <summary>
        /// 返回该 mover 在模拟中需要的全部状态信息
        /// </summary>
        public CharacterPhysicsMoverState GetState()
        {
            CharacterPhysicsMoverState state = new CharacterPhysicsMoverState();

            state.Position = TransientPosition;
            state.Rotation = TransientRotation;
            state.Velocity = Velocity;
            state.AngularVelocity = AngularVelocity;

            return state;
        }

        /// <summary>
        /// 立即应用一个 mover 状态
        /// </summary>
        public void ApplyState(CharacterPhysicsMoverState state)
        {
            SetPositionAndRotation(state.Position, state.Rotation);
            Velocity = state.Velocity;
            AngularVelocity = state.AngularVelocity;
        }

        /// <summary>
        /// 根据 deltaTime 和目标位置/旋转缓存速度值
        /// </summary>
        public void VelocityUpdate(float deltaTime)
        {
            InitialSimulationPosition = TransientPosition;
            InitialSimulationRotation = TransientRotation;

            MoverController.UpdateMovement(out _internalTransientPosition, out _internalTransientRotation, deltaTime);

            if (deltaTime > 0f)
            {
                Velocity = (TransientPosition - InitialSimulationPosition) / deltaTime;

                Quaternion rotationFromCurrentToGoal = TransientRotation * (Quaternion.Inverse(InitialSimulationRotation));
                AngularVelocity = (Mathf.Deg2Rad * rotationFromCurrentToGoal.eulerAngles) / deltaTime;
            }
        }
    }
}
