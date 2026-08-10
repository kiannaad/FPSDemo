using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 角色命中非运动学刚体时使用的交互模式。
    /// </summary>
    public enum RigidbodyInteractionType
    {
        // 不主动给动态刚体施加推动，也不根据刚体反作用修正角色速度。
        None,
        // 把角色视为无限力量的运动学物体，推动动态刚体时不考虑角色质量损失。
        Kinematic,
        // 按 SimulatedCharacterMass 模拟角色质量，让角色和刚体都获得速度变化。
        SimulatedDynamic
    }

    /// <summary>
    /// 角色遇到低矮障碍时的台阶处理策略。
    /// </summary>
    public enum StepHandlingMethod
    {
        // 不做台阶跨越，只按普通墙面或坡面处理碰撞。
        None,
        // 使用标准台阶检测，适合大多数角色控制场景。
        Standard,
        // 额外尝试更小深度的台阶检测，能上更窄的台阶，但查询成本更高。
        Extra
    }

    /// <summary>
    /// 单次移动 Sweep 迭代中的阻挡状态，用于处理连续撞墙、折线和墙角。
    /// </summary>
    public enum MovementSweepState
    {
        // 尚未遇到有效阻挡。
        Initial,
        // 已遇到第一次阻挡，下一次命中需要判断是否形成夹角。
        AfterFirstHit,
        // 已识别出阻挡折线，速度只能沿折线方向保留。
        FoundBlockingCrease,
        // 已识别出完全阻挡的角落，剩余速度应被清零。
        FoundBlockingCorner,
    }

    /// <summary>
    /// 表示角色 Motor 在模拟中所需的完整状态。
    /// 可用于保存状态，或回退到之前的状态。
    /// </summary>
    [System.Serializable]
    public struct CharacterPhysicsMotorState
    {
        // 角色 Motor 的世界位置，用于回滚或保存状态。
        public Vector3 Position;
        // 角色 Motor 的世界旋转，用于恢复角色朝向和 Up/Forward 方向。
        public Quaternion Rotation;
        // 不包含附着刚体速度的角色基础速度。
        public Vector3 BaseVelocity;

        // 是否要求下一次接地探测强制离地，跳跃时会用到。
        public bool MustUnground;
        // 强制离地剩余时间，避免刚起跳就被贴地逻辑重新吸回地面。
        public float MustUngroundTime;
        // 上一次移动迭代是否命中过可作为地面的表面，用于下一次探地距离选择。
        public bool LastMovementIterationFoundAnyGround;
        // 保存接地状态的轻量版本，供回滚恢复。
        public CharacterTransientGroundingReport GroundingStatus;

        // 当前附着的刚体或移动平台。
        public Rigidbody AttachedRigidbody;
        // 附着刚体在角色当前位置产生的线速度。
        public Vector3 AttachedRigidbodyVelocity;
    }

    /// <summary>
    /// 描述角色胶囊体与另一个碰撞体之间的重叠结果
    /// </summary>
    public struct OverlapResult
    {
        // 从重叠中解离时使用的法线方向。
        public Vector3 Normal;
        // 与角色胶囊体发生重叠的碰撞体。
        public Collider Collider;

        public OverlapResult(Vector3 normal, Collider collider)
        {
            // 保存本次重叠解离法线，后续移动前会用它投影速度，避免继续挤进碰撞体。
            Normal = normal;
            // 保存碰撞体引用，用于调试或离散碰撞事件。
            Collider = collider;
        }
    }

    /// <summary>
    /// 包含 Motor 接地状态的完整信息
    /// </summary>
    public struct CharacterGroundingReport
    {
        // 是否检测到任何地面或地表，即使该表面不一定能稳定站立。
        public bool FoundAnyGround;
        // 当前地面是否满足稳定站立条件。
        public bool IsStableOnGround;
        // 是否因边缘、落差、强制离地等规则阻止贴地。
        public bool SnappingPrevented;
        // 探测命中的原始地面法线。
        public Vector3 GroundNormal;
        // 边缘检测中角色内侧地面的法线。
        public Vector3 InnerGroundNormal;
        // 边缘检测中角色外侧地面的法线。
        public Vector3 OuterGroundNormal;

        // 当前接地命中的碰撞体；完整报告会保存引用，临时报告不会。
        public Collider GroundCollider;
        // 当前接地命中的世界坐标点。
        public Vector3 GroundPoint;

        public void CopyFrom(CharacterTransientGroundingReport transientGroundingReport)
        {
            // 复制可序列化的接地布尔状态。
            FoundAnyGround = transientGroundingReport.FoundAnyGround;
            IsStableOnGround = transientGroundingReport.IsStableOnGround;
            SnappingPrevented = transientGroundingReport.SnappingPrevented;
            // 复制接地法线信息，供下一帧坡面和边缘逻辑使用。
            GroundNormal = transientGroundingReport.GroundNormal;
            InnerGroundNormal = transientGroundingReport.InnerGroundNormal;
            OuterGroundNormal = transientGroundingReport.OuterGroundNormal;

            // 临时报告不保存 Collider/Point，恢复时必须清空，避免持有过期引用。
            GroundCollider = null;
            GroundPoint = Vector3.zero;
        }
    }

    /// <summary>
    /// 包含与模拟相关的临时接地状态信息
    /// </summary>
    public struct CharacterTransientGroundingReport
    {
        // 是否检测到任意地表。
        public bool FoundAnyGround;
        // 是否稳定站立在地面上。
        public bool IsStableOnGround;
        // 当前帧是否阻止贴地吸附。
        public bool SnappingPrevented;
        // 接地命中的主法线。
        public Vector3 GroundNormal;
        // 边缘检测内侧法线。
        public Vector3 InnerGroundNormal;
        // 边缘检测外侧法线。
        public Vector3 OuterGroundNormal;

        public void CopyFrom(CharacterGroundingReport groundingReport)
        {
            // 从完整接地报告复制轻量字段，用于状态保存或上一帧缓存。
            FoundAnyGround = groundingReport.FoundAnyGround;
            IsStableOnGround = groundingReport.IsStableOnGround;
            SnappingPrevented = groundingReport.SnappingPrevented;
            GroundNormal = groundingReport.GroundNormal;
            InnerGroundNormal = groundingReport.InnerGroundNormal;
            OuterGroundNormal = groundingReport.OuterGroundNormal;
        }
    }

    /// <summary>
    /// 包含一次命中稳定性评估的全部信息
    /// </summary>
    public struct HitStabilityReport
    {
        // 这次命中的表面是否能被角色视为稳定地面。
        public bool IsStable;

        // 是否找到边缘检测内侧法线。
        public bool FoundInnerNormal;
        // 命中点朝角色内侧探测到的法线。
        public Vector3 InnerNormal;
        // 是否找到边缘检测外侧法线。
        public bool FoundOuterNormal;
        // 命中点朝角色外侧探测到的法线。
        public Vector3 OuterNormal;

        // 是否检测到可跨越的有效台阶。
        public bool ValidStepDetected;
        // 可跨越台阶所属的碰撞体。
        public Collider SteppedCollider;

        // 是否检测到角色正处于边缘附近。
        public bool LedgeDetected;
        // 角色是否处于边缘的空侧。
        public bool IsOnEmptySideOfLedge;
        // 角色胶囊体中心轴到边缘的水平距离。
        public float DistanceFromLedge;
        // 当前速度是否朝边缘空侧移动。
        public bool IsMovingTowardsEmptySideOfLedge;
        // 边缘附近可作为地面的法线。
        public Vector3 LedgeGroundNormal;
        // 边缘线的右方向。
        public Vector3 LedgeRightDirection;
        // 从安全侧指向空侧的方向。
        public Vector3 LedgeFacingDirection;
    }

    /// <summary>
    /// 记录移动阶段命中的刚体信息，以便后续处理
    /// </summary>
    public struct RigidbodyProjectionHit
    {
        // 被角色命中的刚体。
        public Rigidbody Rigidbody;
        // 命中点世界坐标，用于 AddForceAtPosition。
        public Vector3 HitPoint;
        // 经过地面和阻挡规则修正后的有效命中法线。
        public Vector3 EffectiveHitNormal;
        // 角色撞上刚体时使用的速度。
        public Vector3 HitVelocity;
        // 该命中是否被视为稳定地面命中。
        public bool StableOnHit;
    }

    public enum MovementState
    {
        Walking,
        Falling,
        None,
    }

    /// <summary>
    /// 负责管理角色碰撞与移动求解的组件
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterPhysicsMotor : MonoBehaviour
    {
#pragma warning disable 0414
        [Header("Components")]
        /// <summary>
        /// 该 Motor 使用的胶囊体碰撞器
        /// </summary>
        [ReadOnly]
        public CapsuleCollider Capsule;

        [Header("Capsule Settings")]
        /// <summary>
        /// 角色胶囊体半径
        /// </summary>
        [SerializeField]
        [Tooltip("角色胶囊体半径")]
        private float _capsuleRadius = 0.5f;
        /// <summary>
        /// 角色胶囊体高度
        /// </summary>
        [SerializeField]
        [Tooltip("角色胶囊体高度")]
        private float _capsuleHeight = 2f;
        /// <summary>
        /// 角色胶囊体中心的本地 Y 坐标
        /// </summary>
        [SerializeField]
        [Tooltip("角色胶囊体中心的本地 Y 坐标")]
        private float _capsuleYOffset = 1f;
        /// <summary>
        /// 角色胶囊体使用的物理材质
        /// </summary>
        [SerializeField]
        [Tooltip("角色胶囊体使用的物理材质；不影响角色自身移动，只影响其他物体与它碰撞时的表现")]
#pragma warning disable 0649
        private PhysicMaterial _capsulePhysicsMaterial;
#pragma warning restore 0649


        [Header("Grounding settings")]
        /// <summary>
        /// 增加地面探测范围，使角色在高速下也能贴附到地面
        /// </summary>
        [Tooltip("增加地面探测范围，使角色高速移动时也能继续贴地")]
        public float GroundDetectionExtraDistance = 0f;
        /// <summary>
        /// 角色可稳定站立的最大坡度角
        /// </summary>
        [Range(0f, 89f)]
        [Tooltip("角色能够稳定站立的最大坡度角")]
        public float MaxStableSlopeAngle = 60f;
        /// <summary>
        /// 哪些层会被角色视为稳定地面
        /// </summary>
        [Tooltip("哪些层可以被角色视为稳定地面")]
        public LayerMask StableGroundLayers = -1;
        /// <summary>
        /// 在检测到离散碰撞时通知 Character Controller
        /// </summary>
        [Tooltip("检测到离散重叠碰撞时是否通知角色控制器")]
        public bool DiscreteCollisionEvents = false;


        [Header("Step settings")]
        /// <summary>
        /// 更准确地在台阶上检测接地状态，但会增加性能开销
        /// </summary>
        [Tooltip("是否启用台阶接地检测；检测更准确，但会增加性能开销")]
        public StepHandlingMethod StepHandling = StepHandlingMethod.Standard;
        /// <summary>
        /// 角色能够跨上的最大台阶高度
        /// </summary>
        [Tooltip("角色能够跨上的最大台阶高度")]
        public float MaxStepHeight = 0.5f;
        /// <summary>
        /// 角色在当前不稳定接地时，是否仍允许跨越障碍
        /// </summary>
        [Tooltip("角色当前没有稳定接地时，是否仍允许跨上障碍")]
        public bool AllowSteppingWithoutStableGrounding = false;
        /// <summary>
        /// 角色能够站上的最小台阶深度。
        /// 用于 Extra stepping，可让角色站上深度小于半径的台阶。
        /// </summary>
        [Tooltip("Extra 台阶模式下角色可站上的最小台阶深度，可用于跨上小于胶囊半径的台阶")]
        public float MinRequiredStepDepth = 0.1f;


        [Header("Ledge settings")]
        /// <summary>
        /// 更准确地检测边缘信息和接地状态，但会增加性能开销
        /// </summary>
        [Tooltip("是否启用边缘与落差检测；接地更准确，但会增加性能开销")]
        public bool LedgeAndDenivelationHandling = true;
        /// <summary>
        /// 角色距离胶囊体中心轴多远时，站在边缘上仍会被视为稳定
        /// </summary>
        [Tooltip("角色距离胶囊体中心轴多远时，站在边缘上仍会被视为稳定")]
        public float MaxStableDistanceFromLedge = 0.5f;
        /// <summary>
        /// 当速度超过阈值时，阻止角色在边缘继续贴地
        /// </summary>
        [Tooltip("角色朝边缘空侧移动速度超过该值时，阻止继续贴地")]
        public float MaxVelocityForLedgeSnap = 0f;
        /// <summary>
        /// 角色在保持贴地时允许承受的最大向下坡度变化
        /// </summary>
        [Tooltip("角色保持贴地时允许承受的最大向下坡度变化角")]
        [Range(1f, 180f)]
        public float MaxStableDenivelationAngle = 180f;


        [Header("Rigidbody interaction settings")]
        /// <summary>
        /// 正确处理角色被 CharacterPhysicsMover 或动态刚体推动、站在其上，以及推动动态刚体的情况
        /// </summary>
        [Tooltip("是否处理角色站在 CharacterPhysicsMover/动态刚体上、被其推动，以及推动动态刚体")]
        public bool InteractiveRigidbodyHandling = true;
        /// <summary>
        /// 定义角色如何与非运动学刚体交互。
        /// Kinematic 表示角色像运动学物体一样以无限力量推动刚体。
        /// SimulatedDynamic 表示使用模拟质量计算角色和刚体的速度变化。
        /// </summary>
        [Tooltip("角色与非运动学刚体的交互方式；Kinematic 为无限力量推动，SimulatedDynamic 为按模拟质量推动")]
        public RigidbodyInteractionType RigidbodyInteractionType;
        [Tooltip("角色推动动态刚体时使用的模拟质量")]
        public float SimulatedCharacterMass = 1f;
        /// <summary>
        /// 决定角色从移动平台离地时是否保留平台速度
        /// </summary>
        [Tooltip("角色离开移动平台时，是否保留平台带来的速度")]
        public bool PreserveAttachedRigidbodyMomentum = true;


        [Header("Constraints settings")]
        /// <summary>
        /// 决定角色移动是否使用平面约束
        /// </summary>
        [Tooltip("角色移动是否使用平面约束")]
        public bool HasPlanarConstraint = false;
        /// <summary>
        /// 启用平面约束时，定义角色移动被约束的平面法线
        /// </summary>
        [Tooltip("启用平面约束时，定义角色移动被约束到的平面法线")]
        public Vector3 PlanarConstraintAxis = Vector3.forward;

        [Header("Other settings")]
        /// <summary>
        /// 每次更新最多进行多少次移动扫描
        /// </summary>
        [Tooltip("每次更新最多执行多少次移动 Sweep")]
        public int MaxMovementIterations = 5;
        /// <summary>
        /// 每次更新最多进行多少次去穿插检测
        /// </summary>
        [Tooltip("每次更新最多执行多少次去穿插检测")]
        public int MaxDecollisionIterations = 1;
        /// <summary>
        /// 单次物理更新最多拆分出的模拟时间片数量。
        /// </summary>
        [Tooltip("单次物理更新最多执行多少个模拟时间片")]
        public int MaxSimulationIterations = 8;
        /// <summary>
        /// 单个模拟时间片允许使用的最大时长。
        /// </summary>
        [Tooltip("单个模拟时间片的最大时长（秒）")]
        public float MaxSimulationTimeStep = 0.05f;
        /// <summary>
        /// Falling 是否在上升速度跨过零点时拆出一个精确到达最高点的时间片。
        /// </summary>
        [Tooltip("跳跃上升速度跨过零点时，是否强制在最高点拆分时间片")]
        public bool ForceJumpApexSubstep = true;
        /// <summary>
        /// 单次 Falling 模拟最多允许多少次最高点修正，防止异常速度反复跨零。
        /// </summary>
        [Tooltip("单次 Falling 模拟允许的最高点时间片修正次数")]
        public int MaxJumpApexAttemptsPerSimulation = 2;
        /// <summary>
        /// 在执行移动体积投射前先检查重叠，确保即使角色已与几何体相交也能检测到碰撞。
        /// 这会增加开销，但能提高防穿透安全性。
        /// </summary>
        [Tooltip("移动 Cast 前是否先检查重叠；更防穿透，但会增加性能开销")]
        public bool CheckMovementInitialOverlaps = true;
        /// <summary>
        /// 当超过最大移动迭代次数时，将速度归零
        /// </summary>
        [Tooltip("超过最大移动迭代次数时是否将速度归零")]
        public bool KillVelocityWhenExceedMaxMovementIterations = true;
        /// <summary>
        /// 当超过最大移动迭代次数时，将剩余移动量清零
        /// </summary>
        [Tooltip("超过最大移动迭代次数时是否将剩余移动量归零")]
        public bool KillRemainingMovementWhenExceedMaxMovementIterations = true;

        /// <summary>
        /// 包含当前接地信息
        /// </summary>
        [System.NonSerialized]
        public CharacterGroundingReport GroundingStatus = new CharacterGroundingReport();
        /// <summary>
        /// 包含上一帧接地信息
        /// </summary>
        [System.NonSerialized]
        public CharacterTransientGroundingReport LastGroundingStatus = new CharacterTransientGroundingReport();
        /// <summary>
        /// 指定角色移动算法可检测碰撞的 LayerMask。
        /// 默认会使用该刚体层的碰撞矩阵。
        /// </summary>
        [System.NonSerialized]
        public LayerMask CollidableLayers = -1;

        /// <summary>
        /// 角色 Motor 的 Transform
        /// </summary>
        public Transform Transform { get { return _transform; } }
        private Transform _transform;
        /// <summary>
        /// 角色在移动计算中的目标位置，在角色更新阶段始终保持最新
        /// </summary>
        public Vector3 TransientPosition { get { return _transientPosition; } }
        private Vector3 _transientPosition;
        /// <summary>
        /// 角色的 Up 方向，在角色更新阶段始终保持最新
        /// </summary>
        public Vector3 CharacterUp { get { return _characterUp; } }
        private Vector3 _characterUp;
        /// <summary>
        /// 角色的 Forward 方向，在角色更新阶段始终保持最新
        /// </summary>
        public Vector3 CharacterForward { get { return _characterForward; } }
        private Vector3 _characterForward;
        /// <summary>
        /// 角色的 Right 方向，在角色更新阶段始终保持最新
        /// </summary>
        public Vector3 CharacterRight { get { return _characterRight; } }
        private Vector3 _characterRight;
        /// <summary>
        /// 移动计算开始前的位置
        /// </summary>
        public Vector3 InitialSimulationPosition { get { return _initialSimulationPosition; } }
        private Vector3 _initialSimulationPosition;
        /// <summary>
        /// 移动计算开始前的旋转
        /// </summary>
        public Quaternion InitialSimulationRotation { get { return _initialSimulationRotation; } }
        private Quaternion _initialSimulationRotation;
        /// <summary>
        /// 表示当前附着的 Rigidbody
        /// </summary>
        public Rigidbody AttachedRigidbody { get { return _attachedRigidbody; } }
        private Rigidbody _attachedRigidbody;
        /// <summary>
        /// 从角色 Transform 位置指向胶囊体中心的向量
        /// </summary>
        public Vector3 CharacterTransformToCapsuleCenter { get { return _characterTransformToCapsuleCenter; } }
        private Vector3 _characterTransformToCapsuleCenter;
        /// <summary>
        /// 从角色 Transform 位置指向胶囊体底部的向量
        /// </summary>
        public Vector3 CharacterTransformToCapsuleBottom { get { return _characterTransformToCapsuleBottom; } }
        private Vector3 _characterTransformToCapsuleBottom;
        /// <summary>
        /// 从角色 Transform 位置指向胶囊体顶部的向量
        /// </summary>
        public Vector3 CharacterTransformToCapsuleTop { get { return _characterTransformToCapsuleTop; } }
        private Vector3 _characterTransformToCapsuleTop;
        /// <summary>
        /// 从角色 Transform 位置指向胶囊体底部半球中心的向量
        /// </summary>
        public Vector3 CharacterTransformToCapsuleBottomHemi { get { return _characterTransformToCapsuleBottomHemi; } }
        private Vector3 _characterTransformToCapsuleBottomHemi;
        /// <summary>
        /// 从角色 Transform 位置指向胶囊体顶部半球中心的向量
        /// </summary>
        public Vector3 CharacterTransformToCapsuleTopHemi { get { return _characterTransformToCapsuleTopHemi; } }
        private Vector3 _characterTransformToCapsuleTopHemi;
        /// <summary>
        /// 角色因站在刚体或 CharacterPhysicsMover 上而获得的速度
        /// </summary>
        public Vector3 AttachedRigidbodyVelocity { get { return _attachedRigidbodyVelocity; } }
        private Vector3 _attachedRigidbodyVelocity;
        /// <summary>
        /// 角色更新过程中当前已检测到的重叠数量，每次更新开始时会重置
        /// </summary>
        public int OverlapsCount { get { return _overlapsCount; } }
        private int _overlapsCount;
        /// <summary>
        /// 角色更新过程中当前记录到的重叠结果
        /// </summary>
        public OverlapResult[] Overlaps { get { return _overlaps; } }
        private OverlapResult[] _overlaps = new OverlapResult[MaxRigidbodyOverlapsCount];

        /// <summary>
        /// 当前 motor 绑定的 controller
        /// </summary>
        [NonSerialized]
        public ICharacterPhysicsController CharacterController;
        /// <summary>
        /// motor 上一次 sweep 碰撞检测是否找到了地面
        /// </summary>
        [NonSerialized]
        public bool LastMovementIterationFoundAnyGround;
        /// <summary>
        /// 该对象在 CharacterPhysicsSystem 数组中的索引
        /// </summary>
        [NonSerialized]
        public int IndexInCharacterSystem;
        /// <summary>
        /// 记录整次模拟开始前的初始位置
        /// </summary>
        [NonSerialized]
        public Vector3 InitialTickPosition;
        /// <summary>
        /// 记录整次模拟开始前的初始旋转
        /// </summary>
        [NonSerialized]
        public Quaternion InitialTickRotation;
        /// <summary>
        /// 指定要保持附着的 Rigidbody
        /// </summary>
        [NonSerialized]
        public Rigidbody AttachedRigidbodyOverride;
        /// <summary>
        /// 角色因直接移动得到的速度
        /// </summary>
        [NonSerialized]
        public Vector3 BaseVelocity;
        /// <summary>
        /// 当前物理移动状态。
        /// </summary>
        [NonSerialized]
        public MovementState PhysicsState = MovementState.Walking;

        // 私有字段：这些缓存只服务本次或最近一次物理求解，避免在高频物理帧中反复分配内存。
        // 角色 Sweep、Raycast、地面探测等查询的共享命中缓存。
        private RaycastHit[] _internalCharacterHits = new RaycastHit[MaxHitsBudget];
        // Overlap 查询的共享碰撞体缓存。
        private Collider[] _internalProbedColliders = new Collider[MaxCollisionBudget];
        // 本次移动已经推动过的刚体，避免同一物理步重复施加速度变化。
        private List<Rigidbody> _rigidbodiesPushedThisMove = new List<Rigidbody>(16);
        // 记录本次移动命中的刚体，移动求解结束后统一处理速度交换。
        private RigidbodyProjectionHit[] _internalRigidbodyProjectionHits = new RigidbodyProjectionHit[MaxRigidbodyOverlapsCount];
        // 上一帧附着的刚体，用于判断是否刚离开平台并保留动量。
        private Rigidbody _lastAttachedRigidbody;
        // 是否启用移动碰撞求解；关闭时会直接改位置，不做 Sweep/Overlap 解算。
        private bool _solveMovementCollisions = true;
        // 是否启用接地求解；关闭后不会判断稳定地面。
        private bool _solveGrounding = true;
        // 外部是否请求了 MoveCharacter，Phase1 会消费这个标记。
        private bool _movePositionDirty = false;
        // MoveCharacter 请求的目标位置。
        private Vector3 _movePositionTarget = Vector3.zero;
        // 外部是否请求了 RotateCharacter，Phase2 会消费这个标记。
        private bool _moveRotationDirty = false;
        // RotateCharacter 请求的目标旋转。
        private Quaternion _moveRotationTarget = Quaternion.identity;
        // 上一次重叠解离法线是否有效，用于辅助连续去穿插逻辑。
        private bool _lastSolvedOverlapNormalDirty = false;
        // 上一次重叠解离得到的法线。
        private Vector3 _lastSolvedOverlapNormal = Vector3.forward;
        // 本次移动记录到的刚体命中数量。
        private int _rigidbodyProjectionHitCount = 0;
        // 当前是否正在应用附着刚体带来的位移，供投影/交互逻辑区分来源。
        private bool _isMovingFromAttachedRigidbody = false;
        // 是否要求下一次探地强制离地。
        private bool _mustUnground = false;
        // 强制离地剩余时间计数。
        private float _mustUngroundTimeCounter = 0f;
        // 缓存世界 Up，减少高频访问和临时构造。
        private Vector3 _cachedWorldUp = Vector3.up;
        // 缓存世界 Forward。
        private Vector3 _cachedWorldForward = Vector3.forward;
        // 缓存世界 Right。
        private Vector3 _cachedWorldRight = Vector3.right;
        // 缓存零向量。
        private Vector3 _cachedZeroVector = Vector3.zero;

        // Motor 内部维护的临时旋转；setter 会同步刷新角色 Up/Forward/Right。
        private Quaternion _transientRotation;
        /// <summary>
        /// 角色在移动计算中的目标旋转，在角色更新阶段始终保持最新
        /// </summary>
        public Quaternion TransientRotation
        {
            get
            {
                return _transientRotation;
            }
            private set
            {
                _transientRotation = value;
                _characterUp = _transientRotation * _cachedWorldUp;
                _characterForward = _transientRotation * _cachedWorldForward;
                _characterRight = _transientRotation * _cachedWorldRight;
            }
        }

        /// <summary>
        /// 角色的总速度，包含站在刚体或 CharacterPhysicsMover 上时附带的速度
        /// </summary>
        public Vector3 Velocity
        {
            get
            {
                return BaseVelocity + _attachedRigidbodyVelocity;
            }
        }

        // 警告：除非你非常清楚这些常量的作用，否则不要改它们！
        // 单次物理查询最多缓存多少个命中结果。
        public const int MaxHitsBudget = 16;
        // 单次重叠查询最多缓存多少个碰撞体。
        public const int MaxCollisionBudget = 16;
        // 地面探测最多允许几次反弹式 Sweep。
        public const int MaxGroundingSweepIterations = 2;
        // 台阶检测最多允许几次 Sweep。
        public const int MaxSteppingSweepIterations = 3;
        // 单次移动最多记录多少个刚体交互命中。
        public const int MaxRigidbodyOverlapsCount = 16;
        // 胶囊体与障碍保持的最小安全间隔，避免浮点误差导致贴面穿插。
        public const float CollisionOffset = 0.01f;
        // 地面探测遇到不稳定面后反弹继续探测的距离。
        public const float GroundProbeReboundDistance = 0.02f;
        // 最小地面探测距离，避免探测距离为 0 时错过近地面。
        public const float MinimumGroundProbingDistance = 0.005f;
        // 地面 Sweep 开始时向反方向回退的距离，用于避免起点贴面导致漏检。
        public const float GroundProbingBackstepDistance = 0.1f;
        // 普通移动 Sweep 开始时向反方向回退的距离，用于过滤起点误差。
        public const float SweepProbingBackstepDistance = 0.002f;
        // 边缘/台阶二次探测时向上抬起的高度。
        public const float SecondaryProbesVertical = 0.02f;
        // 边缘/台阶二次探测时向内外侧偏移的距离。
        public const float SecondaryProbesHorizontal = 0.001f;
        // 小于该值的速度会被归零，避免微小抖动持续积累。
        public const float MinVelocityMagnitude = 0.01f;
        // 台阶检测时从障碍前方向前额外探测的距离。
        public const float SteppingForwardDistance = 0.03f;
        // 判定边缘所需的最小高度距离。
        public const float MinDistanceForLedge = 0.05f;
        // 判断障碍是否近似垂直的点乘阈值。
        public const float CorrelationForVerticalObstruction = 0.01f;
        // Extra 台阶模式使用的额外前向距离。
        public const float ExtraSteppingForwardDistance = 0.01f;
        // Extra 台阶模式使用的额外高度容差。
        public const float ExtraStepHeightPadding = 0.01f;
        // 模拟时间片的最小时长，避免极小 deltaTime 进入速度和位移计算。
        public const float MinSimulationTimeStep = 0.0001f;
#pragma warning restore 0414

        private IPhysicsRegistration physicsRegistration;
        private ICharacterPhysicsEventSink physicsEventSink;

        private void OnEnable()
        {
            ICharacterPhysicsWorld physicsWorld = World.Current?.CharacterMotorSimulation as ICharacterPhysicsWorld;
            physicsRegistration = physicsWorld?.Register(this)
                ?? throw new InvalidOperationException("Character physics world is not initialized.");
        }

        private void OnDisable()
        {
            // 当前 Motor 禁用后必须注销，否则系统会继续模拟失效对象。
            physicsRegistration?.Dispose();
            physicsRegistration = null;
        }

        internal void SetPhysicsEventSink(ICharacterPhysicsEventSink eventSink)
        {
            physicsEventSink = eventSink;
        }

        private void Reset()
        {
            // 组件第一次添加或重置时，自动修正胶囊体与参数。
            ValidateData();
        }

        private void OnValidate()
        {
            // Inspector 中参数变化时立即校验，防止出现无效半径、台阶高度等配置。
            ValidateData();
        }

        [ContextMenu("Remove Component")]
        private void HandleRemoveComponent()
        {
            // 菜单移除组件时，同时移除自动绑定的 CapsuleCollider。
            CapsuleCollider tmpCapsule = gameObject.GetComponent<CapsuleCollider>();
            DestroyImmediate(this);
            DestroyImmediate(tmpCapsule);
        }

        /// <summary>
        /// 校验并修正所有必需数据
        /// </summary>
        public void ValidateData()
        {
            // Motor 必须依赖同一个物体上的 CapsuleCollider 作为角色物理体。
            Capsule = GetComponent<CapsuleCollider>();
            // 半径不能超过高度的一半，否则胶囊体几何会退化。
            _capsuleRadius = Mathf.Clamp(_capsuleRadius, 0f, _capsuleHeight * 0.5f);
            // Unity CapsuleCollider 的 direction=1 表示沿本地 Y 轴。
            Capsule.direction = 1;
            // 物理材质只影响外部物体与胶囊体的物理响应，不参与 Motor 自己的移动求解。
            Capsule.sharedMaterial = _capsulePhysicsMaterial;
            // 统一通过 SetCapsuleDimensions 写入尺寸，并同步内部胶囊体偏移缓存。
            SetCapsuleDimensions(_capsuleRadius, _capsuleHeight, _capsuleYOffset);

            // 台阶高度不能为负。
            MaxStepHeight = Mathf.Clamp(MaxStepHeight, 0f, Mathf.Infinity);
            // 最小台阶深度不能超过胶囊半径，否则 Extra 台阶检测会失去意义。
            MinRequiredStepDepth = Mathf.Clamp(MinRequiredStepDepth, 0f, _capsuleRadius);
            // 角色能稳定站在边缘上的距离不能超过胶囊半径。
            MaxStableDistanceFromLedge = Mathf.Clamp(MaxStableDistanceFromLedge, 0f, _capsuleRadius);

            // Motor 不支持缩放，强制 Transform 缩放为 1，避免胶囊体查询与数学缓存不一致。
            transform.localScale = Vector3.one;

#if UNITY_EDITOR
            Capsule.hideFlags = HideFlags.NotEditable;
            if (!Mathf.Approximately(transform.lossyScale.x, 1f) || !Mathf.Approximately(transform.lossyScale.y, 1f) || !Mathf.Approximately(transform.lossyScale.z, 1f))
            {
                Debug.LogError("角色的 lossyScale 不是 (1,1,1)。CharacterPhysicsMotor 不允许角色或父物体带缩放，请确保角色 Transform 及其所有父节点缩放都是 (1,1,1)。", this.gameObject);
            }
#endif
        }

        #region 直接设置

        /// <summary>
        /// 设置胶囊体碰撞器是否参与碰撞检测
        /// </summary>
        public void SetCapsuleCollisionsActivation(bool collisionsActive)
        {
            // 通过 Trigger 开关决定胶囊体是否参与 Unity 物理阻挡。
            Capsule.isTrigger = !collisionsActive;
        }

        /// <summary>
        /// 设置 motor 在移动过程中（或被移动到某位置时）是否进行碰撞求解
        /// </summary>
        public void SetMovementCollisionsSolvingActivation(bool movementCollisionsSolvingActive)
        {
            // 关闭后 Motor 仍会更新位置，但不会通过 Sweep/Overlap 处理障碍。
            _solveMovementCollisions = movementCollisionsSolvingActive;
        }

        /// <summary>
        /// 设置是否对所有命中都评估接地状态
        /// </summary>
        public void SetGroundSolvingActivation(bool stabilitySolvingActive)
        {
            // 关闭后所有命中都不会被视为稳定地面，适合飞行/无碰撞等特殊状态。
            _solveGrounding = stabilitySolvingActive;
        }

        /// <summary>
        /// 直接设置角色位置
        /// </summary>
        public void SetPosition(Vector3 position, bool bypassInterpolation = true)
        {
            // 立即同步 Transform，外部读取位置时能拿到新值。
            _transform.position = position;
            // 同步本次模拟起点，避免下一次物理步把角色从旧位置继续推进。
            _initialSimulationPosition = position;
            // 同步 Motor 内部真实物理位置。
            _transientPosition = position;

            if (bypassInterpolation)
            {
                // 跳过插值时，把渲染插值起点也改到新位置，避免瞬移后拉回旧位置。
                InitialTickPosition = position;
            }
        }

        /// <summary>
        /// 直接设置角色旋转
        /// </summary>
        public void SetRotation(Quaternion rotation, bool bypassInterpolation = true)
        {
            // 立即同步 Transform 旋转。
            _transform.rotation = rotation;
            // 同步本次模拟起始旋转。
            _initialSimulationRotation = rotation;
            // 通过属性 setter 同步角色 Up/Forward/Right。
            TransientRotation = rotation;

            if (bypassInterpolation)
            {
                // 跳过插值时，把渲染插值起点也改到新旋转。
                InitialTickRotation = rotation;
            }
        }

        /// <summary>
        /// 直接设置角色的位置和旋转
        /// </summary>
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool bypassInterpolation = true)
        {
            // 同时设置 Transform 位置和旋转，减少两次 Transform 写入。
            _transform.SetPositionAndRotation(position, rotation);
            // 同步模拟起点。
            _initialSimulationPosition = position;
            _initialSimulationRotation = rotation;
            // 同步 Motor 内部真实位置和旋转。
            _transientPosition = position;
            TransientRotation = rotation;

            if (bypassInterpolation)
            {
                // 瞬移/回滚时同步插值起点，避免渲染层补间到错误姿态。
                InitialTickPosition = position;
                InitialTickRotation = rotation;
            }
        }

        /// <summary>
        /// 移动角色位置，并把移动碰撞求解考虑进去。实际位移会在下次 motor 更新时生效
        /// </summary>
        public void MoveCharacter(Vector3 toPosition)
        {
            // 这里只记录请求，真正位移会在 Phase1 中按碰撞规则求解。
            _movePositionDirty = true;
            _movePositionTarget = toPosition;
        }

        /// <summary>
        /// 移动角色旋转。实际旋转会在下次 motor 更新时生效
        /// </summary>
        public void RotateCharacter(Quaternion toRotation)
        {
            // 这里只记录请求，真正旋转会在 Phase2 中应用。
            _moveRotationDirty = true;
            _moveRotationTarget = toRotation;
        }

        /// <summary>
        /// 返回 motor 在模拟中需要的全部状态信息
        /// </summary>
        public CharacterPhysicsMotorState GetState()
        {
            // 显式构造状态快照，供存档、预测回滚或网络校正使用。
            CharacterPhysicsMotorState state = new CharacterPhysicsMotorState();

            // 保存 Motor 真实物理位姿，而不是当前渲染插值后的 Transform。
            state.Position = _transientPosition;
            state.Rotation = _transientRotation;

            // 保存角色自身速度和平台附加速度。
            state.BaseVelocity = BaseVelocity;
            state.AttachedRigidbodyVelocity = _attachedRigidbodyVelocity;

            // 保存会影响下一帧接地/移动的瞬时状态。
            state.MustUnground = _mustUnground;
            state.MustUngroundTime = _mustUngroundTimeCounter;
            state.LastMovementIterationFoundAnyGround = LastMovementIterationFoundAnyGround;
            state.GroundingStatus.CopyFrom(GroundingStatus);
            // 保存当前附着刚体，恢复后才能继续处理平台速度。
            state.AttachedRigidbody = _attachedRigidbody;

            return state;
        }

        /// <summary>
        /// 立即应用一个 motor 状态
        /// </summary>
        public void ApplyState(CharacterPhysicsMotorState state, bool bypassInterpolation = true)
        {
            // 先恢复位姿，确保后续速度和接地状态基于正确位置。
            SetPositionAndRotation(state.Position, state.Rotation, bypassInterpolation);

            // 恢复基础速度和附着刚体速度。
            BaseVelocity = state.BaseVelocity;
            _attachedRigidbodyVelocity = state.AttachedRigidbodyVelocity;

            // 恢复会影响接地吸附和下一帧移动的状态。
            _mustUnground = state.MustUnground;
            _mustUngroundTimeCounter = state.MustUngroundTime;
            LastMovementIterationFoundAnyGround = state.LastMovementIterationFoundAnyGround;
            GroundingStatus.CopyFrom(state.GroundingStatus);
            _attachedRigidbody = state.AttachedRigidbody;
        }

        /// <summary>
        /// 调整胶囊体尺寸，同时缓存重要的胶囊体尺寸数据
        /// </summary>
        public void SetCapsuleDimensions(float radius, float height, float yOffset)
        {
            // 高度至少要略大于直径，避免胶囊体几何退化。
            height = Mathf.Max(height, (radius * 2f) + 0.01f);

            // 记录 Inspector/运行时配置值。
            _capsuleRadius = radius;
            _capsuleHeight = height;
            _capsuleYOffset = yOffset;

            // 写回 Unity CapsuleCollider 的真实几何数据。
            Capsule.radius = _capsuleRadius;
            Capsule.height = Mathf.Clamp(_capsuleHeight, _capsuleRadius * 2f, _capsuleHeight);
            Capsule.center = new Vector3(0f, _capsuleYOffset, 0f);

            // 缓存从角色 Transform 到胶囊关键点的本地偏移，后续所有查询都会复用这些值。
            _characterTransformToCapsuleCenter = Capsule.center;
            _characterTransformToCapsuleBottom = Capsule.center + (-_cachedWorldUp * (Capsule.height * 0.5f));
            _characterTransformToCapsuleTop = Capsule.center + (_cachedWorldUp * (Capsule.height * 0.5f));
            _characterTransformToCapsuleBottomHemi = Capsule.center + (-_cachedWorldUp * (Capsule.height * 0.5f)) + (_cachedWorldUp * Capsule.radius);
            _characterTransformToCapsuleTopHemi = Capsule.center + (_cachedWorldUp * (Capsule.height * 0.5f)) + (-_cachedWorldUp * Capsule.radius);
        }

        #endregion

        private void Awake()
        {
            // 缓存 Transform，避免高频物理更新中反复访问 this.transform。
            _transform = this.transform;
            // 初始化并校验胶囊体参数。
            ValidateData();

            // 初始化 Motor 内部真实位姿。
            _transientPosition = _transform.position;
            TransientRotation = _transform.rotation;

            // 构建 CollidableLayers 掩码
            CollidableLayers = 0;
            for (int i = 0; i < 32; i++)
            {
                // 按 Unity 项目层碰撞矩阵过滤掉与角色所在层互相忽略的层。
                if (!Physics.GetIgnoreLayerCollision(this.gameObject.layer, i))
                {
                    CollidableLayers |= (1 << i);
                }
            }

            // 再次同步胶囊体尺寸，确保缓存偏移与 Awake 后的最终参数一致。
            SetCapsuleDimensions(_capsuleRadius, _capsuleHeight, _capsuleYOffset);
        }

        #region 更新阶段一

        /// <summary>
        /// 更新阶段 1 应在 physics mover 计算完速度之后调用，但要在它们真正模拟目标位置/旋转之前执行
        /// 它负责以下事情：
        /// - 初始化本帧模拟数据
        /// - 处理 MoveCharacter 请求
        /// - 解决初始穿插
        /// - 探测地面并处理贴地
        /// - 检测潜在可交互刚体
        /// </summary>
        public void UpdatePhase1(float deltaTime)
        {
            // NaN 传播安全保护
            if (float.IsNaN(BaseVelocity.x) || float.IsNaN(BaseVelocity.y) || float.IsNaN(BaseVelocity.z))
            {
                BaseVelocity = Vector3.zero;
            }
            if (float.IsNaN(_attachedRigidbodyVelocity.x) || float.IsNaN(_attachedRigidbodyVelocity.y) || float.IsNaN(_attachedRigidbodyVelocity.z))
            {
                _attachedRigidbodyVelocity = Vector3.zero;
            }

#if UNITY_EDITOR
            if (!Mathf.Approximately(_transform.lossyScale.x, 1f) || !Mathf.Approximately(_transform.lossyScale.y, 1f) || !Mathf.Approximately(_transform.lossyScale.z, 1f))
            {
                Debug.LogError("角色的 lossyScale 不是 (1,1,1)。CharacterPhysicsMotor 不允许角色或父物体带缩放，请确保角色 Transform 及其所有父节点缩放都是 (1,1,1)。", this.gameObject);
            }
#endif

            // 每个物理步重新记录被推动刚体，保证同一帧内不重复推动。
            _rigidbodiesPushedThisMove.Clear();

            // 更新前回调
            CharacterController.BeforeCharacterUpdate(deltaTime);

            _transientPosition = _transform.position;
            TransientRotation = _transform.rotation;
            // 记录本次模拟开始时的位姿，平面约束和调试状态会用到。
            _initialSimulationPosition = _transientPosition;
            _initialSimulationRotation = _transientRotation;
            // 清空本帧刚体命中和重叠缓存。
            _rigidbodyProjectionHitCount = 0;
            _overlapsCount = 0;
            _lastSolvedOverlapNormalDirty = false;

            #region 处理外部请求的移动位置
            if (_movePositionDirty)
            {
                if (_solveMovementCollisions)
                {
                    // 把目标位置差值转换为速度，让普通移动求解流程处理碰撞和刚体交互。
                    Vector3 tmpVelocity = GetVelocityFromMovement(_movePositionTarget - _transientPosition, deltaTime);
                    if (InternalCharacterMove(ref tmpVelocity, deltaTime))
                    {
                        if (InteractiveRigidbodyHandling)
                        {
                            // 如果 MoveCharacter 过程中碰到刚体，也按刚体交互规则处理速度。
                            ProcessVelocityForRigidbodyHits(ref tmpVelocity, deltaTime);
                        }
                    }
                }
                else
                {
                    // 不解碰撞时直接移动到目标位置。
                    _transientPosition = _movePositionTarget;
                }

                // 请求已消费，避免下一帧重复执行。
                _movePositionDirty = false;
            }
            #endregion

            // 把当前接地状态缓存为上一帧状态，再重建本帧接地报告。
            LastGroundingStatus.CopyFrom(GroundingStatus);
            GroundingStatus = new CharacterGroundingReport();
            // 默认地面法线为角色 Up，避免没有地面时出现零向量。
            GroundingStatus.GroundNormal = _characterUp;

            if (_solveMovementCollisions)
            {
                #region 解决初始重叠
                // 初始重叠解离：角色可能因为传送、平台挤压或上一帧误差已经卡进静态几何体。
                Vector3 resolutionDirection = _cachedWorldUp;
                float resolutionDistance = 0f;
                int iterationsMade = 0;
                bool overlapSolved = false;
                while (iterationsMade < MaxDecollisionIterations && !overlapSolved)
                {
                    // 查询当前位置胶囊体与哪些碰撞体重叠。
                    int nbOverlaps = CharacterCollisionsOverlap(_transientPosition, _transientRotation, _internalProbedColliders);

                    if (nbOverlaps > 0)
                    {
                        // 解算那些不涉及动态刚体或 physics mover 的重叠
                        for (int i = 0; i < nbOverlaps; i++)
                        {
                            if (GetInteractiveRigidbody(_internalProbedColliders[i]) == null)
                            {
                                // 计算重叠解离方向与距离
                                Transform overlappedTransform = _internalProbedColliders[i].GetComponent<Transform>();
                                if (Physics.ComputePenetration(
                                        Capsule,
                                        _transientPosition,
                                        _transientRotation,
                                        _internalProbedColliders[i],
                                        overlappedTransform.position,
                                        overlappedTransform.rotation,
                                        out resolutionDirection,
                                        out resolutionDistance))
                                {
                                    // 计算有效阻挡法线
                                    HitStabilityReport mockReport = new HitStabilityReport();
                                    mockReport.IsStable = IsStableOnNormal(resolutionDirection);
                                    resolutionDirection = GetObstructionNormal(resolutionDirection, mockReport.IsStable);

                                    // 应用解离位移
                                    Vector3 resolutionMovement = resolutionDirection * (resolutionDistance + CollisionOffset);
                                    _transientPosition += resolutionMovement;

                                    // 记录本次重叠解离结果
                                    if (_overlapsCount < _overlaps.Length)
                                    {
                                        _overlaps[_overlapsCount] = new OverlapResult(resolutionDirection, _internalProbedColliders[i]);
                                        _overlapsCount++;
                                    }

                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 没有重叠，说明初始位置已经可用。
                        overlapSolved = true;
                    }

                    // 限制迭代次数，避免复杂几何导致死循环。
                    iterationsMade++;
                }
                #endregion
            }

            #region 地面探测与贴地
            // 处理取消接地
            if (_solveGrounding)
            {
                if (MustUnground())
                {
                    // 强制把角色稍微抬离地面，避免起跳等动作被贴地探测重新吸回地面。
                    _transientPosition += _characterUp * (MinimumGroundProbingDistance * 1.5f);
                }
                else
                {
                    // 选择合适的地面探测距离
                    float selectedGroundProbingDistance = MinimumGroundProbingDistance;
                    if (!LastGroundingStatus.SnappingPrevented && (LastGroundingStatus.IsStableOnGround || LastMovementIterationFoundAnyGround))
                    {
                        if (StepHandling != StepHandlingMethod.None)
                        {
                            // 启用台阶时，探测距离至少覆盖胶囊半径或最大台阶高度。
                            selectedGroundProbingDistance = Mathf.Max(_capsuleRadius, MaxStepHeight);
                        }
                        else
                        {
                            // 不处理台阶时，探测距离覆盖胶囊半径即可。
                            selectedGroundProbingDistance = _capsuleRadius;
                        }

                        // 项目可额外增加贴地探测距离，用于高速下坡或高速移动场景。
                        selectedGroundProbingDistance += GroundDetectionExtraDistance;
                    }

                    // 执行地面 Sweep，必要时会修正 _transientPosition 来完成贴地。
                    ProbeGround(ref _transientPosition, _transientRotation, selectedGroundProbingDistance, ref GroundingStatus);

                    if (!LastGroundingStatus.IsStableOnGround && GroundingStatus.IsStableOnGround)
                    {
                        // 重新定向速度，使其贴合新地面
                        BaseVelocity = Vector3.ProjectOnPlane(BaseVelocity, CharacterUp);
                        BaseVelocity = GetDirectionTangentToSurface(BaseVelocity, GroundingStatus.GroundNormal) * BaseVelocity.magnitude;
                    }
                }
            }

            LastMovementIterationFoundAnyGround = false;

            if (_mustUngroundTimeCounter > 0f)
            {
                // 强制离地计时逐步减少，归零后允许恢复正常探地。
                _mustUngroundTimeCounter -= deltaTime;
            }
            // 单帧强制离地标记在探地后清空，持续离地由计时器承担。
            _mustUnground = false;
            #endregion

            if (_solveGrounding)
            {
                // 接地状态已经更新，业务层可在这里处理落地/离地事件。
                CharacterController.PostGroundingUpdate(deltaTime);
            }

            if (InteractiveRigidbodyHandling)
            {
                #region 可交互刚体处理
                // 缓存上一帧附着刚体，用于判断平台切换和离开平台。
                _lastAttachedRigidbody = _attachedRigidbody;
                if (AttachedRigidbodyOverride)
                {
                    // 外部强制指定附着刚体时，优先使用覆盖值。
                    _attachedRigidbody = AttachedRigidbodyOverride;
                }
                else
                {
                    // 从接地结果中检测可交互的刚体
                    if (GroundingStatus.IsStableOnGround && GroundingStatus.GroundCollider.attachedRigidbody)
                    {
                        Rigidbody interactiveRigidbody = GetInteractiveRigidbody(GroundingStatus.GroundCollider);
                        if (interactiveRigidbody)
                        {
                            _attachedRigidbody = interactiveRigidbody;
                        }
                    }
                    else
                    {
                        _attachedRigidbody = null;
                    }
                }

                Vector3 tmpVelocityFromCurrentAttachedRigidbody = Vector3.zero;
                Vector3 tmpAngularVelocityFromCurrentAttachedRigidbody = Vector3.zero;
                if (_attachedRigidbody)
                {
                    // 计算附着刚体在角色当前位置产生的线速度和角速度。
                    GetVelocityFromRigidbodyMovement(_attachedRigidbody, _transientPosition, deltaTime, out tmpVelocityFromCurrentAttachedRigidbody, out tmpAngularVelocityFromCurrentAttachedRigidbody);
                }

                // 从附着刚体上失去稳定接地时保留动量
                if (PreserveAttachedRigidbodyMomentum && _lastAttachedRigidbody != null && _attachedRigidbody != _lastAttachedRigidbody)
                {
                    // 离开旧平台时，把旧平台速度转移到角色基础速度中。
                    BaseVelocity += _attachedRigidbodyVelocity;
                    // 如果同时落到新平台上，扣掉新平台当前速度，避免速度被叠两次。
                    BaseVelocity -= tmpVelocityFromCurrentAttachedRigidbody;
                }

                // 处理来自附着刚体的额外速度
                _attachedRigidbodyVelocity = _cachedZeroVector;
                if (_attachedRigidbody)
                {
                    // 当前平台速度作为附加速度，不直接混进 BaseVelocity。
                    _attachedRigidbodyVelocity = tmpVelocityFromCurrentAttachedRigidbody;

                    // 来自附着刚体的旋转
                    Vector3 newForward = Vector3.ProjectOnPlane(Quaternion.Euler(Mathf.Rad2Deg * tmpAngularVelocityFromCurrentAttachedRigidbody * deltaTime) * _characterForward, _characterUp).normalized;
                    TransientRotation = Quaternion.LookRotation(newForward, _characterUp);
                }

                // 落到附着刚体上时，消除多余的水平速度
                if (GroundingStatus.GroundCollider &&
                    GroundingStatus.GroundCollider.attachedRigidbody &&
                    GroundingStatus.GroundCollider.attachedRigidbody == _attachedRigidbody &&
                    _attachedRigidbody != null &&
                    _lastAttachedRigidbody == null)
                {
                    // 刚落上平台时，去掉与平台相同的水平速度，避免角色相对平台突然滑动。
                    BaseVelocity -= Vector3.ProjectOnPlane(_attachedRigidbodyVelocity, _characterUp);
                }

                // 来自附着刚体的位移
                if (_attachedRigidbodyVelocity.sqrMagnitude > 0f)
                {
                    _isMovingFromAttachedRigidbody = true;

                    if (_solveMovementCollisions)
                    {
                        // 根据刚体速度执行位移
                        InternalCharacterMove(ref _attachedRigidbodyVelocity, deltaTime);
                    }
                    else
                    {
                        // 不解碰撞时，直接把平台位移叠加到角色位置。
                        _transientPosition += _attachedRigidbodyVelocity * deltaTime;
                    }

                    _isMovingFromAttachedRigidbody = false;
                }
                #endregion
            }

            SetMovementState(GroundingStatus.IsStableOnGround
                ? MovementState.Walking
                : MovementState.Falling);
        }

        #endregion

        #region 更新阶段二

        /// <summary>
        /// 更新阶段 2 应在 physics mover 已经模拟完目标位置/旋转之后调用。
        /// 在这一阶段结束时，TransientPosition/Rotation 会更新为 motor 完成此次移动后应处于的最终姿态。
        /// 它负责以下事情：
        /// - 计算角色旋转
        /// - 处理 RotateCharacter 请求
        /// - 解决平台/刚体移动造成的潜在重叠
        /// - 计算业务速度并执行最终移动
        /// - 应用平面约束
        /// </summary>
        public void UpdatePhase2(float deltaTime)
        {
            // 处理旋转
            CharacterController.UpdateRotation(ref _transientRotation, deltaTime);
            // 通过属性 setter 同步角色 Up/Forward/Right。
            TransientRotation = _transientRotation;

            // 处理与位移相关的旋转
            if (_moveRotationDirty)
            {
                // 外部 RotateCharacter 请求优先覆盖业务回调后的旋转。
                TransientRotation = _moveRotationTarget;
                // 请求已消费，避免下一帧重复应用。
                _moveRotationDirty = false;
            }

            if (_solveMovementCollisions && InteractiveRigidbodyHandling)
            {
                if (InteractiveRigidbodyHandling)
                {
                    #region 解决附着刚体可能造成的重叠
                    if (_attachedRigidbody)
                    {
                        // 平台移动后，角色可能被压进平台；先从脚下向上留出一个胶囊半径的检测空间。
                        float upwardsOffset = Capsule.radius;

                        RaycastHit closestHit;
                        if (CharacterGroundSweep(
                                _transientPosition + (_characterUp * upwardsOffset),
                                _transientRotation,
                                -_characterUp,
                                upwardsOffset,
                                out closestHit))
                        {
                            if (closestHit.collider.attachedRigidbody == _attachedRigidbody && IsStableOnNormal(closestHit.normal))
                            {
                                // 如果仍命中当前附着刚体，就把角色沿 Up 推出平台表面。
                                float distanceMovedUp = (upwardsOffset - closestHit.distance);
                                _transientPosition = _transientPosition + (_characterUp * distanceMovedUp) + (_characterUp * CollisionOffset);
                            }
                        }
                    }
                    #endregion
                }

                if (InteractiveRigidbodyHandling)
                {
                    #region 解决旋转或移动平台推动造成的重叠
                    // Phase2 中再次去穿插：旋转、平台落位或刚体推动都可能制造新的重叠。
                    Vector3 resolutionDirection = _cachedWorldUp;
                    float resolutionDistance = 0f;
                    int iterationsMade = 0;
                    bool overlapSolved = false;
                    while (iterationsMade < MaxDecollisionIterations && !overlapSolved)
                    {
                        int nbOverlaps = CharacterCollisionsOverlap(_transientPosition, _transientRotation, _internalProbedColliders);
                        if (nbOverlaps > 0)
                        {
                            for (int i = 0; i < nbOverlaps; i++)
                            {
                                // 计算重叠解离方向与距离
                                Transform overlappedTransform = _internalProbedColliders[i].GetComponent<Transform>();
                                if (Physics.ComputePenetration(
                                        Capsule,
                                        _transientPosition,
                                        _transientRotation,
                                        _internalProbedColliders[i],
                                        overlappedTransform.position,
                                        overlappedTransform.rotation,
                                        out resolutionDirection,
                                        out resolutionDistance))
                                {
                                    // 计算有效阻挡法线
                                    HitStabilityReport mockReport = new HitStabilityReport();
                                    mockReport.IsStable = IsStableOnNormal(resolutionDirection);
                                    resolutionDirection = GetObstructionNormal(resolutionDirection, mockReport.IsStable);

                                    // 应用解离位移
                                    Vector3 resolutionMovement = resolutionDirection * (resolutionDistance + CollisionOffset);
                                    _transientPosition += resolutionMovement;

                                    // 如果命中的是可交互刚体，则将其登记为会影响速度的刚体命中
                                    if (InteractiveRigidbodyHandling)
                                    {
                                        Rigidbody probedRigidbody = GetInteractiveRigidbody(_internalProbedColliders[i]);
                                        if (probedRigidbody != null)
                                        {
                                            HitStabilityReport tmpReport = new HitStabilityReport();
                                            tmpReport.IsStable = IsStableOnNormal(resolutionDirection);
                                            if (tmpReport.IsStable)
                                            {
                                                // 记录本次移动中找到了稳定地面，影响下一帧探地距离。
                                                LastMovementIterationFoundAnyGround = tmpReport.IsStable;
                                            }
                                            if (probedRigidbody != _attachedRigidbody)
                                            {
                                                // 估算碰撞点；当前实现没有精确点，使用角色位置作为近似交互点。
                                                Vector3 characterCenter = _transientPosition + (_transientRotation * _characterTransformToCapsuleCenter);
                                                Vector3 estimatedCollisionPoint = _transientPosition;


                                                StoreRigidbodyHit(
                                                    probedRigidbody,
                                                    Velocity,
                                                    estimatedCollisionPoint,
                                                    resolutionDirection,
                                                    tmpReport);
                                            }
                                        }
                                    }

                                    // 记录本次重叠解离结果
                                    if (_overlapsCount < _overlaps.Length)
                                    {
                                        _overlaps[_overlapsCount] = new OverlapResult(resolutionDirection, _internalProbedColliders[i]);
                                        _overlapsCount++;
                                    }

                                    break;
                                }
                            }
                        }
                        else
                        {
                            // 所有重叠都已解开，停止去穿插循环。
                            overlapSolved = true;
                        }

                        // 限制去穿插迭代次数，避免复杂几何卡住一帧。
                        iterationsMade++;
                    }
                    #endregion
                }
            }

            StartNewPhysics(deltaTime, 0);

            // 应用平面约束
            if (HasPlanarConstraint)
            {
                // 把本次模拟产生的位移投影到指定平面上，用于 2.5D 或特殊约束场景。
                _transientPosition = _initialSimulationPosition + Vector3.ProjectOnPlane(_transientPosition - _initialSimulationPosition, PlanarConstraintAxis.normalized);
            }

            // 触发离散碰撞事件
            if (DiscreteCollisionEvents)
            {
                int nbOverlaps = CharacterCollisionsOverlap(_transientPosition, _transientRotation, _internalProbedColliders, CollisionOffset * 2f);
                for (int i = 0; i < nbOverlaps; i++)
                {
                    // 通知业务层当前最终位置仍与某些碰撞体存在离散重叠。
                    physicsEventSink?.Enqueue(CharacterController, _internalProbedColliders[i]);
                }
            }

            // 所有位移、速度、刚体交互和离散碰撞都处理完毕，通知业务层收尾。
            CharacterController.AfterCharacterUpdate(deltaTime);
        }

        #endregion

        public void SetMovementState(MovementState state)
        {
            PhysicsState = state;
        }

        private void StartNewPhysics(float deltaTime, int iterations)
        {
            int maxIterations = Mathf.Max(1, MaxSimulationIterations);
            if (deltaTime < MinSimulationTimeStep || iterations >= maxIterations)
            {
                return;
            }

            switch (PhysicsState)
            {
                case MovementState.Walking:
                    PhysicsWalking(deltaTime, iterations);
                    break;
                case MovementState.Falling:
                    PhysicsFalling(deltaTime, iterations);
                    break;
                case MovementState.None:
                    break;
            }
        }

        private float GetSimulationTimeStep(float remainingTime, int iterations)
        {
            int maxIterations = Mathf.Max(1, MaxSimulationIterations);
            float maxTimeStep = Mathf.Max(MinSimulationTimeStep, MaxSimulationTimeStep);
            if (remainingTime > maxTimeStep && iterations < maxIterations)
            {
                return Mathf.Max(MinSimulationTimeStep, Mathf.Min(maxTimeStep, remainingTime * 0.5f));
            }

            return Mathf.Max(MinSimulationTimeStep, remainingTime);
        }

        private void PhysicsWalking(float deltaTime, int iterations)
        {
            if (deltaTime < MinSimulationTimeStep)
            {
                return;
            }

            float remainingTime = deltaTime;
            int maxIterations = Mathf.Max(1, MaxSimulationIterations);
            MovementState startingMovementState = PhysicsState;

            while (remainingTime >= MinSimulationTimeStep && iterations < maxIterations)
            {
                iterations++;
                float timeTick = GetSimulationTimeStep(remainingTime, iterations);
                remainingTime = Mathf.Max(0f, remainingTime - timeTick);

                CharacterController.UpdateVelocity(ref BaseVelocity, timeTick);
                if (PhysicsState != startingMovementState)
                {
                    StartNewPhysics(remainingTime + timeTick, iterations - 1);
                    return;
                }

                if (BaseVelocity.magnitude < MinVelocityMagnitude)
                {
                    BaseVelocity = Vector3.zero;
                }

                Vector3 oldPosition = _transientPosition;
                Vector3 desiredMovement = BaseVelocity * timeTick;
                bool startedFalling = MustUnground() && Vector3.Dot(BaseVelocity, _characterUp) > 0f;
                MoveCharacterForTimeStep(timeTick);

                if (startedFalling)
                {
                    GroundingStatus.IsStableOnGround = false;
                    GroundingStatus.SnappingPrevented = true;
                    SetMovementState(MovementState.Falling);
                    CharacterController.PostGroundingUpdate(timeTick);
                    StartNewPhysics(remainingTime, iterations);
                    return;
                }

                if (PhysicsState != startingMovementState)
                {
                    float desiredDistance = Vector3.ProjectOnPlane(desiredMovement, _characterUp).magnitude;
                    if (desiredDistance > Mathf.Epsilon)
                    {
                        float actualDistance = Vector3.ProjectOnPlane(_transientPosition - oldPosition, _characterUp).magnitude;
                        float consumedFraction = Mathf.Min(1f, actualDistance / desiredDistance);
                        remainingTime += timeTick * (1f - consumedFraction);
                    }

                    StartNewPhysics(remainingTime, iterations);
                    return;
                }

                if (desiredMovement.sqrMagnitude <= Mathf.Epsilon ||
                    (_transientPosition - oldPosition).sqrMagnitude <= Mathf.Epsilon)
                {
                    break;
                }
            }
        }

        private void MoveCharacterForTimeStep(float deltaTime)
        {
            if (BaseVelocity.sqrMagnitude > 0f)
            {
                if (_solveMovementCollisions)
                {
                    InternalCharacterMove(ref BaseVelocity, deltaTime);
                }
                else
                {
                    _transientPosition += BaseVelocity * deltaTime;
                }
            }

            if (InteractiveRigidbodyHandling)
            {
                ProcessVelocityForRigidbodyHits(ref BaseVelocity, deltaTime);
            }
        }

        private void PhysicsFalling(float deltaTime, int iterations)
        {
            if (deltaTime < MinSimulationTimeStep)
            {
                return;
            }

            float remainingTime = deltaTime;
            int maxIterations = Mathf.Max(1, MaxSimulationIterations);
            int apexAttempts = 0;
            MovementState startingMovementState = PhysicsState;

            while (remainingTime >= MinSimulationTimeStep && iterations < maxIterations)
            {
                iterations++;
                float timeTick = GetSimulationTimeStep(remainingTime, iterations);
                remainingTime = Mathf.Max(0f, remainingTime - timeTick);

                Vector3 oldVelocity = BaseVelocity;
                CharacterController.UpdateVelocity(ref BaseVelocity, timeTick);
                if (PhysicsState != startingMovementState)
                {
                    StartNewPhysics(remainingTime + timeTick, iterations - 1);
                    return;
                }

                Vector3 newVelocity = BaseVelocity;
                float oldVerticalSpeed = Vector3.Dot(oldVelocity, _characterUp);
                float newVerticalSpeed = Vector3.Dot(newVelocity, _characterUp);

                if (ForceJumpApexSubstep &&
                    oldVerticalSpeed > 0f &&
                    newVerticalSpeed <= 0f &&
                    apexAttempts < Mathf.Max(0, MaxJumpApexAttemptsPerSimulation))
                {
                    Vector3 derivedAcceleration = (newVelocity - oldVelocity) / timeTick;
                    float verticalAcceleration = Vector3.Dot(derivedAcceleration, _characterUp);
                    if (!Mathf.Approximately(verticalAcceleration, 0f))
                    {
                        float timeToApex = -oldVerticalSpeed / verticalAcceleration;
                        if (timeToApex >= MinSimulationTimeStep && timeToApex < timeTick)
                        {
                            float refundedTime = timeTick - timeToApex;
                            remainingTime += refundedTime;
                            timeTick = timeToApex;
                            iterations--;
                            apexAttempts++;

                            newVelocity = oldVelocity + (derivedAcceleration * timeToApex);
                            newVelocity = Vector3.ProjectOnPlane(newVelocity, _characterUp);
                            BaseVelocity = newVelocity;
                        }
                    }
                }

                if (BaseVelocity.magnitude < MinVelocityMagnitude)
                {
                    BaseVelocity = Vector3.zero;
                    newVelocity = Vector3.zero;
                }

                Vector3 oldPosition = _transientPosition;
                bool startedUpwardImpulse = oldVerticalSpeed <= 0f && Vector3.Dot(newVelocity, _characterUp) > 0f;
                Vector3 movementVelocity = startedUpwardImpulse
                    ? newVelocity
                    : (oldVelocity + newVelocity) * 0.5f;
                Vector3 desiredMovement = movementVelocity * timeTick;

                MoveCharacterForFallingTimeStep(ref movementVelocity, newVelocity, timeTick);

                if (PhysicsState != startingMovementState)
                {
                    RefundUnusedMovementTime(ref remainingTime, timeTick, desiredMovement, oldPosition);
                    StartNewPhysics(remainingTime, iterations);
                    return;
                }

                if (TryTransitionFromFallingToWalking(timeTick))
                {
                    RefundUnusedMovementTime(ref remainingTime, timeTick, desiredMovement, oldPosition);
                    StartNewPhysics(remainingTime, iterations);
                    return;
                }

                if (desiredMovement.sqrMagnitude <= Mathf.Epsilon ||
                    (_transientPosition - oldPosition).sqrMagnitude <= Mathf.Epsilon)
                {
                    break;
                }
            }
        }

        private void MoveCharacterForFallingTimeStep(ref Vector3 movementVelocity, Vector3 targetVelocity, float deltaTime)
        {
            Vector3 velocityBeforeMove = movementVelocity;

            if (movementVelocity.sqrMagnitude > 0f)
            {
                if (_solveMovementCollisions)
                {
                    InternalCharacterMove(ref movementVelocity, deltaTime);
                }
                else
                {
                    _transientPosition += movementVelocity * deltaTime;
                }
            }

            BaseVelocity = (movementVelocity - velocityBeforeMove).sqrMagnitude <= Mathf.Epsilon
                ? targetVelocity
                : movementVelocity;

            if (InteractiveRigidbodyHandling)
            {
                ProcessVelocityForRigidbodyHits(ref BaseVelocity, deltaTime);
            }
        }

        private bool TryTransitionFromFallingToWalking(float deltaTime)
        {
            if (!_solveGrounding || MustUnground() || Vector3.Dot(BaseVelocity, _characterUp) > 0f)
            {
                return false;
            }

            CharacterGroundingReport landingGround = new CharacterGroundingReport();
            landingGround.GroundNormal = _characterUp;
            float probingDistance = Mathf.Max(MinimumGroundProbingDistance, CollisionOffset * 2f);
            ProbeGround(ref _transientPosition, _transientRotation, probingDistance, ref landingGround);
            if (!landingGround.IsStableOnGround)
            {
                return false;
            }

            GroundingStatus = landingGround;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(BaseVelocity, _characterUp);
            BaseVelocity = horizontalVelocity.sqrMagnitude > 0f
                ? GetDirectionTangentToSurface(horizontalVelocity, landingGround.GroundNormal) * horizontalVelocity.magnitude
                : Vector3.zero;
            SetMovementState(MovementState.Walking);
            CharacterController.PostGroundingUpdate(deltaTime);
            return true;
        }

        private void RefundUnusedMovementTime(ref float remainingTime, float timeTick, Vector3 desiredMovement, Vector3 oldPosition)
        {
            float desiredDistance = desiredMovement.magnitude;
            if (desiredDistance <= Mathf.Epsilon)
            {
                return;
            }

            float actualDistance = (_transientPosition - oldPosition).magnitude;
            float consumedFraction = Mathf.Min(1f, actualDistance / desiredDistance);
            remainingTime += timeTick * (1f - consumedFraction);
        }

        /// <summary>
        /// 判断 motor 在给定命中法线下是否可视为稳定地面
        /// </summary>
        private bool IsStableOnNormal(Vector3 normal)
        {
            // 命中法线与角色 Up 的夹角小于最大稳定坡度，就可以被视为可站立表面。
            return Vector3.Angle(_characterUp, normal) <= MaxStableSlopeAngle;
        }

        /// <summary>
        /// 在基础坡度判断之外，结合边缘、落差和速度等特殊情况再次判断是否稳定
        /// </summary>
        private bool IsStableWithSpecialCases(ref HitStabilityReport stabilityReport, Vector3 velocity)
        {
            if (LedgeAndDenivelationHandling)
            {
                if (stabilityReport.LedgeDetected)
                {
                    if (stabilityReport.IsMovingTowardsEmptySideOfLedge)
                    {
                        // 边缘外侧速度过大时，不再视为稳定
                        Vector3 velocityOnLedgeNormal = Vector3.Project(velocity, stabilityReport.LedgeFacingDirection);
                        if (velocityOnLedgeNormal.magnitude >= MaxVelocityForLedgeSnap)
                        {
                            return false;
                        }
                    }

                    // 站在边缘空侧且离边缘过远时，不再稳定
                    if (stabilityReport.IsOnEmptySideOfLedge && stabilityReport.DistanceFromLedge > MaxStableDistanceFromLedge)
                    {
                        return false;
                    }
                }

                // 如果地面法线变化过大，角色会从坡面或落差处自然离地，而不是继续强制贴地。
                if (LastGroundingStatus.FoundAnyGround && stabilityReport.InnerNormal.sqrMagnitude != 0f && stabilityReport.OuterNormal.sqrMagnitude != 0f)
                {
                    // 先比较当前内外侧地面法线的落差角。
                    float denivelationAngle = Vector3.Angle(stabilityReport.InnerNormal, stabilityReport.OuterNormal);
                    if (denivelationAngle > MaxStableDenivelationAngle)
                    {
                        return false;
                    }
                    else
                    {
                        // 再比较上一帧内侧地面与当前外侧地面的落差角，避免高速经过边缘时漏判。
                        denivelationAngle = Vector3.Angle(LastGroundingStatus.InnerGroundNormal, stabilityReport.OuterNormal);
                        if (denivelationAngle > MaxStableDenivelationAngle)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 探测有效地面，并在发生贴地时修正输入的 transientPosition
        /// </summary>
        public void ProbeGround(ref Vector3 probingPosition, Quaternion atRotation, float probingDistance, ref CharacterGroundingReport groundingReport)
        {
            if (probingDistance < MinimumGroundProbingDistance)
            {
                probingDistance = MinimumGroundProbingDistance;
            }

            int groundSweepsMade = 0;
            RaycastHit groundSweepHit = new RaycastHit();
            bool groundSweepingIsOver = false;
            Vector3 groundSweepPosition = probingPosition;
            Vector3 groundSweepDirection = (atRotation * -_cachedWorldUp);
            float groundProbeDistanceRemaining = probingDistance;
            while (groundProbeDistanceRemaining > 0 && (groundSweepsMade <= MaxGroundingSweepIterations) && !groundSweepingIsOver)
            {
                // 通过 sweep 检测地面
                if (CharacterGroundSweep(
                        groundSweepPosition, // 地面探测起点位置
                        atRotation, // 当前角色旋转
                        groundSweepDirection, // 探测方向
                        groundProbeDistanceRemaining, // 剩余探测距离
                        out groundSweepHit)) // 地面命中结果
                {
                    Vector3 targetPosition = groundSweepPosition + (groundSweepDirection * groundSweepHit.distance);
                    HitStabilityReport groundHitStabilityReport = new HitStabilityReport();
                    EvaluateHitStability(groundSweepHit.collider, groundSweepHit.normal, groundSweepHit.point, targetPosition, _transientRotation, BaseVelocity, ref groundHitStabilityReport);

                    groundingReport.FoundAnyGround = true;
                    groundingReport.GroundNormal = groundSweepHit.normal;
                    groundingReport.InnerGroundNormal = groundHitStabilityReport.InnerNormal;
                    groundingReport.OuterGroundNormal = groundHitStabilityReport.OuterNormal;
                    groundingReport.GroundCollider = groundSweepHit.collider;
                    groundingReport.GroundPoint = groundSweepHit.point;
                    groundingReport.SnappingPrevented = false;

                        // 命中了稳定地面
                    if (groundHitStabilityReport.IsStable)
                    {
                        // 找出所有需要取消贴地的情况
                        groundingReport.SnappingPrevented = !IsStableWithSpecialCases(ref groundHitStabilityReport, BaseVelocity);

                        groundingReport.IsStableOnGround = true;

                        // 如果允许贴地，则修正探测位置
                        if (!groundingReport.SnappingPrevented)
                        {
                            probingPosition = groundSweepPosition + (groundSweepDirection * (groundSweepHit.distance - CollisionOffset));
                        }

                        CharacterController.OnGroundHit(groundSweepHit.collider, groundSweepHit.normal, groundSweepHit.point, ref groundHitStabilityReport);
                        groundSweepingIsOver = true;
                    }
                    else
                    {
                        // 计算本次迭代的移动量并推进位置
                        Vector3 sweepMovement = (groundSweepDirection * groundSweepHit.distance) + ((atRotation * _cachedWorldUp) * Mathf.Max(CollisionOffset, groundSweepHit.distance));
                        groundSweepPosition = groundSweepPosition + sweepMovement;

                        // 计算剩余探测距离
                        groundProbeDistanceRemaining = Mathf.Min(GroundProbeReboundDistance, Mathf.Max(groundProbeDistanceRemaining - sweepMovement.magnitude, 0f));

                        // 将探测方向沿命中表面重定向
                        groundSweepDirection = Vector3.ProjectOnPlane(groundSweepDirection, groundSweepHit.normal).normalized;
                    }
                }
                else
                {
                    groundSweepingIsOver = true;
                }

                groundSweepsMade++;
            }
        }

        /// <summary>
        /// 强制角色在下一次接地更新时进入离地状态
        /// </summary>
        public void ForceUnground(float time = 0.1f)
        {
            _mustUnground = true;
            _mustUngroundTimeCounter = time;
        }

        public bool MustUnground()
        {
            return _mustUnground || _mustUngroundTimeCounter > 0f;
        }

        /// <summary>
        /// 返回一个相对于角色 Up 方向、且与指定表面法线相切的调整后方向。
        /// 适合在坡面上重定向方向，同时尽量不引入横向偏移
        /// </summary>
        public Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal)
        {
            // 先求出输入方向相对角色 Up 的右方向。
            Vector3 directionRight = Vector3.Cross(direction, _characterUp);
            // 再用表面法线和右方向重建贴合表面的前进方向。
            return Vector3.Cross(surfaceNormal, directionRight).normalized;
        }

        /// <summary>
        /// 让角色按给定位移进行移动，同时考虑物理模拟、台阶处理以及
        /// 所有会影响角色 motor 的速度投影规则
        /// </summary>
        /// <returns>如果移动在最大迭代次数内完整解出，返回 true；否则返回 false。</returns>
        private bool InternalCharacterMove(ref Vector3 transientVelocity, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                // 没有有效时间步时无法把速度转换成位移。
                return false;
            }

            // 先应用平面约束
            if (HasPlanarConstraint)
            {
                transientVelocity = Vector3.ProjectOnPlane(transientVelocity, PlanarConstraintAxis.normalized);
            }

            bool wasCompleted = true;
            // 当前剩余位移方向。
            Vector3 remainingMovementDirection = transientVelocity.normalized;
            // 当前剩余位移长度，速度乘以固定步长得到本帧要走的距离。
            float remainingMovementMagnitude = transientVelocity.magnitude * deltaTime;
            // 保留最初速度方向，后续投影和夹角判断会用它做参照。
            Vector3 originalVelocityDirection = remainingMovementDirection;
            // 本次移动已经执行了多少次 Sweep。
            int sweepsMade = 0;
            // 本轮 Sweep 是否命中过东西；没有命中时可以直接走完剩余位移。
            bool hitSomethingThisSweepIteration = true;
            // 移动求解过程中的临时位置，最终才写回 _transientPosition。
            Vector3 tmpMovedPosition = _transientPosition;
            // 上一次命中是否为稳定表面。
            bool previousHitIsStable = false;
            // 上一次命中前的速度。
            Vector3 previousVelocity = _cachedZeroVector;
            // 上一次命中的有效阻挡法线。
            Vector3 previousObstructionNormal = _cachedZeroVector;
            // 当前移动 Sweep 状态，用于识别墙角和夹缝。
            MovementSweepState sweepState = MovementSweepState.Initial;

            // 在执行 sweep 前，先把移动量投影到当前重叠的约束之上
            for (int i = 0; i < _overlapsCount; i++)
            {
                Vector3 overlapNormal = _overlaps[i].Normal;
                if (Vector3.Dot(remainingMovementDirection, overlapNormal) < 0f)
                {
                    // 如果移动方向正朝重叠法线的反方向推进，说明会继续挤进碰撞体，需要先投影速度。
                    bool stableOnHit = IsStableOnNormal(overlapNormal) && !MustUnground();
                    Vector3 velocityBeforeProjection = transientVelocity;
                    Vector3 obstructionNormal = GetObstructionNormal(overlapNormal, stableOnHit);

                    InternalHandleVelocityProjection(
                        stableOnHit,
                        overlapNormal,
                        obstructionNormal,
                        originalVelocityDirection,
                        ref sweepState,
                        previousHitIsStable,
                        previousVelocity,
                        previousObstructionNormal,
                        ref transientVelocity,
                        ref remainingMovementMagnitude,
                        ref remainingMovementDirection);

                    previousHitIsStable = stableOnHit;
                    previousVelocity = velocityBeforeProjection;
                    previousObstructionNormal = obstructionNormal;
                }
            }

            // 循环执行 sweep，直到移动量耗尽或本轮不再命中
            while (remainingMovementMagnitude > 0f &&
                (sweepsMade <= MaxMovementIterations) &&
                hitSomethingThisSweepIteration)
            {
                bool foundClosestHit = false;
                Vector3 closestSweepHitPoint = default;
                Vector3 closestSweepHitNormal = default;
                float closestSweepHitDistance = 0f;
                Collider closestSweepHitCollider = null;

                if (CheckMovementInitialOverlaps)
                {
                    // 在 Cast 前先查重叠，能处理角色起点已经陷入几何体的情况。
                    int numOverlaps = CharacterCollisionsOverlap(
                                        tmpMovedPosition,
                                        _transientRotation,
                                        _internalProbedColliders,
                                        0f,
                                        false);
                    if (numOverlaps > 0)
                    {
                        closestSweepHitDistance = 0f;

                        float mostObstructingOverlapNormalDotProduct = 2f;

                        for (int i = 0; i < numOverlaps; i++)
                        {
                            Collider tmpCollider = _internalProbedColliders[i];

                            // ComputePenetration 会给出从重叠中推出去的方向和距离。
                            if (Physics.ComputePenetration(
                                Capsule,
                                tmpMovedPosition,
                                _transientRotation,
                                tmpCollider,
                                tmpCollider.transform.position,
                                tmpCollider.transform.rotation,
                                out Vector3 resolutionDirection,
                                out float resolutionDistance))
                            {
                                float dotProduct = Vector3.Dot(remainingMovementDirection, resolutionDirection);
                                if (dotProduct < 0f && dotProduct < mostObstructingOverlapNormalDotProduct)
                                {
                                    // 选择最阻挡当前移动方向的重叠法线，作为本轮最近命中。
                                    mostObstructingOverlapNormalDotProduct = dotProduct;

                                    closestSweepHitNormal = resolutionDirection;
                                    closestSweepHitCollider = tmpCollider;
                                    closestSweepHitPoint = tmpMovedPosition + (_transientRotation * CharacterTransformToCapsuleCenter) + (resolutionDirection * resolutionDistance);

                                    foundClosestHit = true;
                                }
                            }
                        }
                    }
                }

                if (!foundClosestHit && CharacterCollisionsSweep(
                        tmpMovedPosition, // Sweep 起点位置
                        _transientRotation, // Sweep 使用的角色旋转
                        remainingMovementDirection, // Sweep 方向
                        remainingMovementMagnitude + CollisionOffset, // Sweep 距离，额外加安全偏移
                        out RaycastHit closestSweepHit, // 最近命中
                        _internalCharacterHits) // 所有命中缓存
                    > 0)
                {
                    closestSweepHitNormal = closestSweepHit.normal;
                    closestSweepHitDistance = closestSweepHit.distance;
                    closestSweepHitCollider = closestSweepHit.collider;
                    closestSweepHitPoint = closestSweepHit.point;

                    foundClosestHit = true;
                }

                if (foundClosestHit)
                {
                    // 先移动到命中点前的安全位置
                    Vector3 sweepMovement = (remainingMovementDirection * (Mathf.Max(0f, closestSweepHitDistance - CollisionOffset)));
                    tmpMovedPosition += sweepMovement;
                    remainingMovementMagnitude -= sweepMovement.magnitude;

                    // 评估当前命中是否稳定
                    HitStabilityReport moveHitStabilityReport = new HitStabilityReport();
                    EvaluateHitStability(closestSweepHitCollider, closestSweepHitNormal, closestSweepHitPoint, tmpMovedPosition, _transientRotation, transientVelocity, ref moveHitStabilityReport);

                    // 处理高于胶囊体底部半径的台阶跨越
                    bool foundValidStepHit = false;
                    if (_solveGrounding && StepHandling != StepHandlingMethod.None && moveHitStabilityReport.ValidStepDetected)
                    {
                        // 障碍法线越接近水平，越不像可跨越的竖直台阶面。
                        float obstructionCorrelation = Mathf.Abs(Vector3.Dot(closestSweepHitNormal, _characterUp));
                        if (obstructionCorrelation <= CorrelationForVerticalObstruction)
                        {
                            // 从阻挡面反方向得到跨台阶的前进方向。
                            Vector3 stepForwardDirection = Vector3.ProjectOnPlane(-closestSweepHitNormal, _characterUp).normalized;
                            // 从台阶上方开始向下探测，寻找可站立的顶部位置。
                            Vector3 stepCastStartPoint = (tmpMovedPosition + (stepForwardDirection * SteppingForwardDistance)) +
                                (_characterUp * MaxStepHeight);

                            // 从台阶高度顶部向下做 cast
                            int nbStepHits = CharacterCollisionsSweep(
                                                stepCastStartPoint, // 台阶检测起点
                                                _transientRotation, // 当前角色旋转
                                                -_characterUp, // 向下检测
                                                MaxStepHeight, // 最大下探距离
                                                out RaycastHit closestStepHit, // 最近台阶命中
                                                _internalCharacterHits,
                                                0f,
                                                true); // 只接受稳定地面层

                            // 检查命中是否对应当前尝试跨上的碰撞体
                            for (int i = 0; i < nbStepHits; i++)
                            {
                                if (_internalCharacterHits[i].collider == moveHitStabilityReport.SteppedCollider)
                                {
                                    // 把角色放到台阶顶部并保留 CollisionOffset，避免贴面穿插。
                                    Vector3 endStepPosition = stepCastStartPoint + (-_characterUp * (_internalCharacterHits[i].distance - CollisionOffset));
                                    tmpMovedPosition = endStepPosition;
                                    foundValidStepHit = true;

                                    // 跨上台阶后，把速度重新投影到角色 Up 平面
                                    transientVelocity = Vector3.ProjectOnPlane(transientVelocity, CharacterUp);
                                    remainingMovementDirection = transientVelocity.normalized;

                                    break;
                                }
                            }
                        }
                    }

                    // 如果本次没有成功跨台阶，则按普通碰撞处理
                    if (!foundValidStepHit)
                    {
                        Vector3 obstructionNormal = GetObstructionNormal(closestSweepHitNormal, moveHitStabilityReport.IsStable);

                        // 通知 controller 发生了移动命中
                        CharacterController.OnMovementHit(closestSweepHitCollider, closestSweepHitNormal, closestSweepHitPoint, ref moveHitStabilityReport);

                        // 若命中刚体，则记录到刚体命中列表中
                        if (InteractiveRigidbodyHandling && closestSweepHitCollider.attachedRigidbody)
                        {
                            StoreRigidbodyHit(
                                closestSweepHitCollider.attachedRigidbody,
                                transientVelocity,
                                closestSweepHitPoint,
                                obstructionNormal,
                                moveHitStabilityReport);
                        }

                        bool stableOnHit = moveHitStabilityReport.IsStable && !MustUnground();
                        Vector3 velocityBeforeProj = transientVelocity;

                        // 为下一次迭代投影速度
                        InternalHandleVelocityProjection(
                            stableOnHit,
                            closestSweepHitNormal,
                            obstructionNormal,
                            originalVelocityDirection,
                            ref sweepState,
                            previousHitIsStable,
                            previousVelocity,
                            previousObstructionNormal,
                            ref transientVelocity,
                            ref remainingMovementMagnitude,
                            ref remainingMovementDirection);

                        previousHitIsStable = stableOnHit;
                        previousVelocity = velocityBeforeProj;
                        previousObstructionNormal = obstructionNormal;
                    }
                }
                // 本轮没有命中任何碰撞体，剩余位移可以直接走完。
                else
                {
                    hitSomethingThisSweepIteration = false;
                }

                // 超过最大允许 sweep 次数时的安全保护
                sweepsMade++;
                if (sweepsMade > MaxMovementIterations)
                {
                    if (KillRemainingMovementWhenExceedMaxMovementIterations)
                    {
                        // 清掉剩余移动量，避免继续推进到不可靠的位置。
                        remainingMovementMagnitude = 0f;
                    }

                    if (KillVelocityWhenExceedMaxMovementIterations)
                    {
                        // 清掉速度，避免下一帧继续沿无法求解的方向挤压。
                        transientVelocity = Vector3.zero;
                    }
                    // 标记本次移动没有完整求解，调用方可以据此处理异常情况。
                    wasCompleted = false;
                }
            }

            // 用剩余移动量继续推进位置
            tmpMovedPosition += (remainingMovementDirection * remainingMovementMagnitude);
            _transientPosition = tmpMovedPosition;

            return wasCompleted;
        }

        /// <summary>
        /// 根据当前接地状态，获取用于移动阻挡求解的有效法线
        /// </summary>
        private Vector3 GetObstructionNormal(Vector3 hitNormal, bool stableOnHit)
        {
            // 求取命中法线、阻挡法线与偏移法线
            Vector3 obstructionNormal = hitNormal;
            if (GroundingStatus.IsStableOnGround && !MustUnground() && !stableOnHit)
            {
                // 稳定接地时遇到墙面，使用沿地面的阻挡法线，避免墙面法线把角色从地面上弹起。
                Vector3 obstructionLeftAlongGround = Vector3.Cross(GroundingStatus.GroundNormal, obstructionNormal).normalized;
                obstructionNormal = Vector3.Cross(obstructionLeftAlongGround, _characterUp).normalized;
            }

            // 处理平行法线叉积结果为 0 的情况
            if (obstructionNormal.sqrMagnitude == 0f)
            {
                obstructionNormal = hitNormal;
            }

            return obstructionNormal;
        }

        /// <summary>
        /// 记录一次刚体命中，留待后续处理
        /// </summary>
        private void StoreRigidbodyHit(Rigidbody hitRigidbody, Vector3 hitVelocity, Vector3 hitPoint, Vector3 obstructionNormal, HitStabilityReport hitStabilityReport)
        {
            if (_rigidbodyProjectionHitCount < _internalRigidbodyProjectionHits.Length)
            {
                if (!hitRigidbody.GetComponent<CharacterPhysicsMotor>())
                {
                    RigidbodyProjectionHit rph = new RigidbodyProjectionHit();
                    rph.Rigidbody = hitRigidbody;
                    rph.HitPoint = hitPoint;
                    rph.EffectiveHitNormal = obstructionNormal;
                    rph.HitVelocity = hitVelocity;
                    rph.StableOnHit = hitStabilityReport.IsStable;

                    _internalRigidbodyProjectionHits[_rigidbodyProjectionHitCount] = rph;
                    _rigidbodyProjectionHitCount++;
                }
            }
        }

        public void SetTransientPosition(Vector3 newPos)
        {
            _transientPosition = newPos;
        }

        /// <summary>
        /// 在检测到命中后处理移动投影
        /// </summary>
        private void InternalHandleVelocityProjection(bool stableOnHit, Vector3 hitNormal, Vector3 obstructionNormal, Vector3 originalDirection,
            ref MovementSweepState sweepState, bool previousHitIsStable, Vector3 previousVelocity, Vector3 previousObstructionNormal,
            ref Vector3 transientVelocity, ref float remainingMovementMagnitude, ref Vector3 remainingMovementDirection)
        {
            if (transientVelocity.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 velocityBeforeProjection = transientVelocity;

            if (stableOnHit)
            {
                LastMovementIterationFoundAnyGround = true;
                HandleVelocityProjection(ref transientVelocity, obstructionNormal, stableOnHit);
            }
            else
            {
                // 第一次非稳定命中：按普通阻挡投影
                if (sweepState == MovementSweepState.Initial)
                {
                    HandleVelocityProjection(ref transientVelocity, obstructionNormal, stableOnHit);
                    sweepState = MovementSweepState.AfterFirstHit;
                }
                // 第二次非稳定命中：尝试识别折线/夹角
                else if (sweepState == MovementSweepState.AfterFirstHit)
                {
                    EvaluateCrease(
                        transientVelocity,
                        previousVelocity,
                        obstructionNormal,
                        previousObstructionNormal,
                        stableOnHit,
                        previousHitIsStable,
                        GroundingStatus.IsStableOnGround && !MustUnground(),
                        out bool foundCrease,
                        out Vector3 creaseDirection);

                    if (foundCrease)
                    {
                        if (GroundingStatus.IsStableOnGround && !MustUnground())
                        {
                            transientVelocity = Vector3.zero;
                            sweepState = MovementSweepState.FoundBlockingCorner;
                        }
                        else
                        {
                            transientVelocity = Vector3.Project(transientVelocity, creaseDirection);
                            sweepState = MovementSweepState.FoundBlockingCrease;
                        }
                    }
                    else
                    {
                        HandleVelocityProjection(ref transientVelocity, obstructionNormal, stableOnHit);
                    }
                }
                // 已经处于阻挡折线状态，再命中则视为角落阻塞
                else if (sweepState == MovementSweepState.FoundBlockingCrease)
                {
                    transientVelocity = Vector3.zero;
                    sweepState = MovementSweepState.FoundBlockingCorner;
                }
            }

            if (HasPlanarConstraint)
            {
                transientVelocity = Vector3.ProjectOnPlane(transientVelocity, PlanarConstraintAxis.normalized);
            }

            float newVelocityFactor = transientVelocity.magnitude / velocityBeforeProjection.magnitude;
            remainingMovementMagnitude *= newVelocityFactor;
            remainingMovementDirection = transientVelocity.normalized;
        }

        private void EvaluateCrease(
            Vector3 currentCharacterVelocity,
            Vector3 previousCharacterVelocity,
            Vector3 currentHitNormal,
            Vector3 previousHitNormal,
            bool currentHitIsStable,
            bool previousHitIsStable,
            bool characterIsStable,
            out bool isValidCrease,
            out Vector3 creaseDirection)
        {
            isValidCrease = false;
            creaseDirection = default;

            if (!characterIsStable || !currentHitIsStable || !previousHitIsStable)
            {
                Vector3 tmpBlockingCreaseDirection = Vector3.Cross(currentHitNormal, previousHitNormal).normalized;
                float dotPlanes = Vector3.Dot(currentHitNormal, previousHitNormal);
                bool isVelocityConstrainedByCrease = false;

                // 如果两个平面相同，则跳过多余计算
                if (dotPlanes < 0.999f)
                {
                    // 后续可评估：这一整段是否可以用更简单的方法实现，例如二维投影。
                    Vector3 normalAOnCreasePlane = Vector3.ProjectOnPlane(currentHitNormal, tmpBlockingCreaseDirection).normalized;
                    Vector3 normalBOnCreasePlane = Vector3.ProjectOnPlane(previousHitNormal, tmpBlockingCreaseDirection).normalized;
                    float dotPlanesOnCreasePlane = Vector3.Dot(normalAOnCreasePlane, normalBOnCreasePlane);

                    Vector3 enteringVelocityDirectionOnCreasePlane = Vector3.ProjectOnPlane(previousCharacterVelocity, tmpBlockingCreaseDirection).normalized;

                    if (dotPlanesOnCreasePlane <= (Vector3.Dot(-enteringVelocityDirectionOnCreasePlane, normalAOnCreasePlane) + 0.001f) &&
                        dotPlanesOnCreasePlane <= (Vector3.Dot(-enteringVelocityDirectionOnCreasePlane, normalBOnCreasePlane) + 0.001f))
                    {
                        isVelocityConstrainedByCrease = true;
                    }
                }

                if (isVelocityConstrainedByCrease)
                {
                    // 翻转折线方向，使其更符合速度真正会被投影到的方向
                    if (Vector3.Dot(tmpBlockingCreaseDirection, currentCharacterVelocity) < 0f)
                    {
                        tmpBlockingCreaseDirection = -tmpBlockingCreaseDirection;
                    }

                    isValidCrease = true;
                    creaseDirection = tmpBlockingCreaseDirection;
                }
            }
        }

        /// <summary>
        /// 允许你自定义速度在阻挡面上的投影方式
        /// </summary>
        public virtual void HandleVelocityProjection(ref Vector3 velocity, Vector3 obstructionNormal, bool stableOnHit)
        {
            if (GroundingStatus.IsStableOnGround && !MustUnground())
            {
                // 在稳定坡面上，只需重新定向移动，不做额外损失
                if (stableOnHit)
                {
                    velocity = GetDirectionTangentToSurface(velocity, obstructionNormal) * velocity.magnitude;
                }
                // 遇到阻挡命中时，在保持接地平面的前提下把移动投影到阻挡面上
                else
                {
                    Vector3 obstructionRightAlongGround = Vector3.Cross(obstructionNormal, GroundingStatus.GroundNormal).normalized;
                    Vector3 obstructionUpAlongGround = Vector3.Cross(obstructionRightAlongGround, obstructionNormal).normalized;
                    velocity = GetDirectionTangentToSurface(velocity, obstructionUpAlongGround) * velocity.magnitude;
                    velocity = Vector3.ProjectOnPlane(velocity, obstructionNormal);
                }
            }
            else
            {
                if (stableOnHit)
                {
                    // 在空中命中稳定面时，重新定向速度
                    velocity = Vector3.ProjectOnPlane(velocity, CharacterUp);
                    velocity = GetDirectionTangentToSurface(velocity, obstructionNormal) * velocity.magnitude;
                }
                // 处理一般阻挡
                else
                {
                    velocity = Vector3.ProjectOnPlane(velocity, obstructionNormal);
                }
            }
        }

        /// <summary>
        /// 允许你自定义命中刚体时的推动 / 交互方式。
        /// 如果这次交互会影响角色速度，就必须修改 ProcessedVelocity。
        /// </summary>
        public virtual void HandleSimulatedRigidbodyInteraction(ref Vector3 processedVelocity, RigidbodyProjectionHit hit, float deltaTime)
        {
        }

        /// <summary>
        /// 把命中刚体带来的影响计入角色速度
        /// </summary>
        private void ProcessVelocityForRigidbodyHits(ref Vector3 processedVelocity, float deltaTime)
        {
            // 移动求解阶段只记录刚体命中；这里统一把命中转换成角色速度和刚体速度变化。
            for (int i = 0; i < _rigidbodyProjectionHitCount; i++)
            {
                // 取出本次记录的刚体命中。
                RigidbodyProjectionHit bodyHit = _internalRigidbodyProjectionHits[i];

                if (bodyHit.Rigidbody && !_rigidbodiesPushedThisMove.Contains(bodyHit.Rigidbody))
                {
                    if (_internalRigidbodyProjectionHits[i].Rigidbody != _attachedRigidbody)
                    {
                        // 记录本次已经处理过的刚体，避免重复推动
                        _rigidbodiesPushedThisMove.Add(bodyHit.Rigidbody);

                        float characterMass = SimulatedCharacterMass;
                        Vector3 characterVelocity = bodyHit.HitVelocity;

                        // 被命中的刚体也可能属于另一个 CharacterPhysicsMotor。
                        CharacterPhysicsMotor hitCharacterMotor = bodyHit.Rigidbody.GetComponent<CharacterPhysicsMotor>();
                        bool hitBodyIsCharacter = hitCharacterMotor != null;
                        bool hitBodyIsDynamic = !bodyHit.Rigidbody.isKinematic;
                        float hitBodyMass = bodyHit.Rigidbody.mass;
                        float hitBodyMassAtPoint = bodyHit.Rigidbody.mass; // 当前先用刚体质量近似命中点质量。
                        Vector3 hitBodyVelocity = bodyHit.Rigidbody.velocity;
                        if (hitBodyIsCharacter)
                        {
                            // 命中另一个角色时，用对方角色的模拟质量和基础速度。
                            hitBodyMass = hitCharacterMotor.SimulatedCharacterMass;
                            hitBodyMassAtPoint = hitCharacterMotor.SimulatedCharacterMass; // 当前先用角色模拟质量近似命中点质量。
                            hitBodyVelocity = hitCharacterMotor.BaseVelocity;
                        }
                        else if (!hitBodyIsDynamic)
                        {
                            // 命中运动学平台时，尝试从 CharacterPhysicsMover 读取平台真实运动速度。
                            CharacterPhysicsMover physicsMover = bodyHit.Rigidbody.GetComponent<CharacterPhysicsMover>();
                            if(physicsMover)
                            {
                                hitBodyVelocity = physicsMover.Velocity;
                            }
                        }

                        // 计算角色质量在总质量中的占比
                        float characterToBodyMassRatio = 1f;
                        {
                            if (characterMass + hitBodyMassAtPoint > 0f)
                            {
                                characterToBodyMassRatio = characterMass / (characterMass + hitBodyMassAtPoint);
                            }
                            else
                            {
                                characterToBodyMassRatio = 0.5f;
                            }

                            // 命中非动态物体
                            if (!hitBodyIsDynamic)
                            {
                                characterToBodyMassRatio = 0f;
                            }
                            // 模拟运动学刚体的交互方式
                            else if (RigidbodyInteractionType == RigidbodyInteractionType.Kinematic && !hitBodyIsCharacter)
                            {
                                characterToBodyMassRatio = 1f;
                            }
                        }

                        ComputeCollisionResolutionForHitBody(
                            bodyHit.EffectiveHitNormal,
                            characterVelocity,
                            hitBodyVelocity,
                            characterToBodyMassRatio,
                            out Vector3 velocityChangeOnCharacter,
                            out Vector3 velocityChangeOnBody);

                        // 把碰撞反作用应用到角色处理后速度上。
                        processedVelocity += velocityChangeOnCharacter;

                        if (hitBodyIsCharacter)
                        {
                            // 命中另一个角色时，直接修改对方 Motor 的基础速度。
                            hitCharacterMotor.BaseVelocity += velocityChangeOnCharacter;
                        }
                        else if (hitBodyIsDynamic)
                        {
                            // 命中动态刚体时，用速度变化模式在命中点施加冲量。
                            bodyHit.Rigidbody.AddForceAtPosition(velocityChangeOnBody, bodyHit.HitPoint, ForceMode.VelocityChange);
                        }

                        if (RigidbodyInteractionType == RigidbodyInteractionType.SimulatedDynamic)
                        {
                            // 给派生类一个自定义模拟动态交互的扩展点。
                            HandleSimulatedRigidbodyInteraction(ref processedVelocity, bodyHit, deltaTime);
                        }
                    }
                }
            }

        }

        public void ComputeCollisionResolutionForHitBody(
            Vector3 hitNormal,
            Vector3 characterVelocity,
            Vector3 bodyVelocity,
            float characterToBodyMassRatio,
            out Vector3 velocityChangeOnCharacter,
            out Vector3 velocityChangeOnBody)
        {
            // 输出默认为无速度变化，只有存在有效相对冲击时才写入。
            velocityChangeOnCharacter = default;
            velocityChangeOnBody = default;

            // 角色质量占比越大，角色自身受到的速度变化越小。
            float bodyToCharacterMassRatio = 1f - characterToBodyMassRatio;
            // 角色速度在命中法线方向上的分量。
            float characterVelocityMagnitudeOnHitNormal = Vector3.Dot(characterVelocity, hitNormal);
            // 被命中物体速度在命中法线方向上的分量。
            float bodyVelocityMagnitudeOnHitNormal = Vector3.Dot(bodyVelocity, hitNormal);

            // 如果角色速度原本朝向阻挡面，则恢复在移动阶段被投影掉的那部分速度
            if (characterVelocityMagnitudeOnHitNormal < 0f)
            {
                Vector3 restoredCharacterVelocity = hitNormal * characterVelocityMagnitudeOnHitNormal;
                velocityChangeOnCharacter += restoredCharacterVelocity;
            }

            // 解算双方的冲量速度，但只在对方刚体速度确实会对角色形成阻力时才处理
            if (bodyVelocityMagnitudeOnHitNormal > characterVelocityMagnitudeOnHitNormal)
            {
                Vector3 relativeImpactVelocity = hitNormal * (bodyVelocityMagnitudeOnHitNormal - characterVelocityMagnitudeOnHitNormal);
                // 质量占比决定速度变化如何分配给角色和被命中刚体。
                velocityChangeOnCharacter += relativeImpactVelocity * bodyToCharacterMassRatio;
                velocityChangeOnBody += -relativeImpactVelocity * characterToBodyMassRatio;
            }
        }

        /// <summary>
        /// 判断输入碰撞体是否适合参与碰撞处理
        /// </summary>
        /// <returns>如果该碰撞体允许参与角色碰撞，返回 true。</returns>
        private bool CheckIfColliderValidForCollisions(Collider coll)
        {
            // 过滤掉角色自己的胶囊体，避免角色和自身发生碰撞。
            if (coll == Capsule)
            {
                return false;
            }

            // 继续执行内部刚体规则和业务层规则。
            if (!InternalIsColliderValidForCollisions(coll))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断输入碰撞体是否适合参与碰撞处理
        /// </summary>
        private bool InternalIsColliderValidForCollisions(Collider coll)
        {
            Rigidbody colliderAttachedRigidbody = coll.attachedRigidbody;
            if (colliderAttachedRigidbody)
            {
                bool isRigidbodyKinematic = colliderAttachedRigidbody.isKinematic;

                // 如果当前位移来自 AttachedRigidbody，则忽略这个 AttachedRigidbody 本身
                if (_isMovingFromAttachedRigidbody && (!isRigidbodyKinematic || colliderAttachedRigidbody == _attachedRigidbody))
                {
                    return false;
                }

                // 如果 RigidbodyInteractionType 是 kinematic，则不要与动态刚体发生碰撞求解
                if (RigidbodyInteractionType == RigidbodyInteractionType.Kinematic && !isRigidbodyKinematic)
                {
                    // 唤醒该刚体
                    if (coll.attachedRigidbody)
                    {
                        coll.attachedRigidbody.WakeUp();
                    }

                    return false;
                }
            }

            // 询问 CharacterController 该碰撞体是否有效
            bool colliderValid = CharacterController.IsColliderValidForCollisions(coll);
            if (!colliderValid)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断 motor 在某次命中上是否应被视为稳定
        /// </summary>
        public void EvaluateHitStability(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, Vector3 withCharacterVelocity, ref HitStabilityReport stabilityReport)
        {
            if (!_solveGrounding)
            {
                stabilityReport.IsStable = false;
                return;
            }

            Vector3 atCharacterUp = atCharacterRotation * _cachedWorldUp;
            Vector3 innerHitDirection = Vector3.ProjectOnPlane(hitNormal, atCharacterUp).normalized;

            stabilityReport.IsStable = this.IsStableOnNormal(hitNormal);

            stabilityReport.FoundInnerNormal = false;
            stabilityReport.FoundOuterNormal = false;
            stabilityReport.InnerNormal = hitNormal;
            stabilityReport.OuterNormal = hitNormal;

            // 边缘与高度落差检测
            if (LedgeAndDenivelationHandling)
            {
                float ledgeCheckHeight = MinDistanceForLedge;
                if (StepHandling != StepHandlingMethod.None)
                {
                    ledgeCheckHeight = MaxStepHeight;
                }

                bool isStableLedgeInner = false;
                bool isStableLedgeOuter = false;

                if (CharacterCollisionsRaycast(
                        hitPoint + (atCharacterUp * SecondaryProbesVertical) + (innerHitDirection * SecondaryProbesHorizontal),
                        -atCharacterUp,
                        ledgeCheckHeight + SecondaryProbesVertical,
                        out RaycastHit innerLedgeHit,
                        _internalCharacterHits) > 0)
                {
                    Vector3 innerLedgeNormal = innerLedgeHit.normal;
                    stabilityReport.InnerNormal = innerLedgeNormal;
                    stabilityReport.FoundInnerNormal = true;
                    isStableLedgeInner = IsStableOnNormal(innerLedgeNormal);
                }

                if (CharacterCollisionsRaycast(
                        hitPoint + (atCharacterUp * SecondaryProbesVertical) + (-innerHitDirection * SecondaryProbesHorizontal),
                        -atCharacterUp,
                        ledgeCheckHeight + SecondaryProbesVertical,
                        out RaycastHit outerLedgeHit,
                        _internalCharacterHits) > 0)
                {
                    Vector3 outerLedgeNormal = outerLedgeHit.normal;
                    stabilityReport.OuterNormal = outerLedgeNormal;
                    stabilityReport.FoundOuterNormal = true;
                    isStableLedgeOuter = IsStableOnNormal(outerLedgeNormal);
                }

                stabilityReport.LedgeDetected = (isStableLedgeInner != isStableLedgeOuter);
                if (stabilityReport.LedgeDetected)
                {
                    stabilityReport.IsOnEmptySideOfLedge = isStableLedgeOuter && !isStableLedgeInner;
                    stabilityReport.LedgeGroundNormal = isStableLedgeOuter ? stabilityReport.OuterNormal : stabilityReport.InnerNormal;
                    stabilityReport.LedgeRightDirection = Vector3.Cross(hitNormal, stabilityReport.LedgeGroundNormal).normalized;
                    stabilityReport.LedgeFacingDirection = Vector3.ProjectOnPlane(Vector3.Cross(stabilityReport.LedgeGroundNormal, stabilityReport.LedgeRightDirection), CharacterUp).normalized;
                    stabilityReport.DistanceFromLedge = Vector3.ProjectOnPlane((hitPoint - (atCharacterPosition + (atCharacterRotation * _characterTransformToCapsuleBottom))), atCharacterUp).magnitude;
                    stabilityReport.IsMovingTowardsEmptySideOfLedge = Vector3.Dot(withCharacterVelocity.normalized, stabilityReport.LedgeFacingDirection) > 0f;
                }

                if (stabilityReport.IsStable)
                {
                    stabilityReport.IsStable = IsStableWithSpecialCases(ref stabilityReport, withCharacterVelocity);
                }
            }

            // 台阶检测
            if (StepHandling != StepHandlingMethod.None && !stabilityReport.IsStable)
            {
                // 动态刚体上不支持台阶跨越
                Rigidbody hitRigidbody = hitCollider.attachedRigidbody;
                if (!(hitRigidbody && !hitRigidbody.isKinematic))
                {
                    DetectSteps(atCharacterPosition, atCharacterRotation, hitPoint, innerHitDirection, ref stabilityReport);

                    if (stabilityReport.ValidStepDetected)
                    {
                        stabilityReport.IsStable = true;
                    }
                }
            }

            CharacterController.ProcessHitStabilityReport(hitCollider, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, ref stabilityReport);
        }

        private void DetectSteps(Vector3 characterPosition, Quaternion characterRotation, Vector3 hitPoint, Vector3 innerHitDirection, ref HitStabilityReport stabilityReport)
        {
            int nbStepHits = 0;
            Collider tmpCollider;
            RaycastHit outerStepHit;
            Vector3 characterUp = characterRotation * _cachedWorldUp;
            Vector3 verticalCharToHit = Vector3.Project((hitPoint - characterPosition), characterUp);
            Vector3 horizontalCharToHitDirection = Vector3.ProjectOnPlane((hitPoint - characterPosition), characterUp).normalized;
            Vector3 stepCheckStartPos = (hitPoint - verticalCharToHit) + (characterUp * MaxStepHeight) + (horizontalCharToHitDirection * CollisionOffset * 3f);

            // 在命中点处做外侧台阶检测（capsule cast）
            nbStepHits = CharacterCollisionsSweep(
                            stepCheckStartPos,
                            characterRotation,
                            -characterUp,
                            MaxStepHeight + CollisionOffset,
                            out outerStepHit,
                            _internalCharacterHits,
                            0f,
                            true);

            // 在命中位置检查重叠和阻挡
            if (CheckStepValidity(nbStepHits, characterPosition, characterRotation, innerHitDirection, stepCheckStartPos, out tmpCollider))
            {
                stabilityReport.ValidStepDetected = true;
                stabilityReport.SteppedCollider = tmpCollider;
            }

            if (StepHandling == StepHandlingMethod.Extra && !stabilityReport.ValidStepDetected)
            {
                // 在命中点处做最小可达台阶检测（capsule cast）
                stepCheckStartPos = characterPosition + (characterUp * MaxStepHeight) + (-innerHitDirection * MinRequiredStepDepth);
                nbStepHits = CharacterCollisionsSweep(
                                stepCheckStartPos,
                                characterRotation,
                                -characterUp,
                                MaxStepHeight - CollisionOffset,
                                out outerStepHit,
                                _internalCharacterHits,
                                0f,
                                true);

                // 在命中位置检查重叠和阻挡
                if (CheckStepValidity(nbStepHits, characterPosition, characterRotation, innerHitDirection, stepCheckStartPos, out tmpCollider))
                {
                    stabilityReport.ValidStepDetected = true;
                    stabilityReport.SteppedCollider = tmpCollider;
                }
            }
        }

        private bool CheckStepValidity(int nbStepHits, Vector3 characterPosition, Quaternion characterRotation, Vector3 innerHitDirection, Vector3 stepCheckStartPos, out Collider hitCollider)
        {
            hitCollider = null;
            Vector3 characterUp = characterRotation * Vector3.up;

            // 寻找最远的有效台阶命中
            bool foundValidStepPosition = false;

            while (nbStepHits > 0 && !foundValidStepPosition)
            {
                // 从剩余命中中找出最远的命中
                RaycastHit farthestHit = new RaycastHit();
                float farthestDistance = 0f;
                int farthestIndex = 0;
                for (int i = 0; i < nbStepHits; i++)
                {
                    float hitDistance = _internalCharacterHits[i].distance;
                    if (hitDistance > farthestDistance)
                    {
                        farthestDistance = hitDistance;
                        farthestHit = _internalCharacterHits[i];
                        farthestIndex = i;
                    }
                }

                Vector3 characterPositionAtHit = stepCheckStartPos + (-characterUp * (farthestHit.distance - CollisionOffset));

                int atStepOverlaps = CharacterCollisionsOverlap(characterPositionAtHit, characterRotation, _internalProbedColliders);
                if (atStepOverlaps <= 0)
                {
                    // 在台阶位置检查外侧命中坡面法线是否稳定
                    if (CharacterCollisionsRaycast(
                            farthestHit.point + (characterUp * SecondaryProbesVertical) + (-innerHitDirection * SecondaryProbesHorizontal),
                            -characterUp,
                            MaxStepHeight + SecondaryProbesVertical,
                            out RaycastHit outerSlopeHit,
                            _internalCharacterHits,
                            true) > 0)
                    {
                        if (IsStableOnNormal(outerSlopeHit.normal))
                        {
                            // 向上做 cast，检测移动到该位置时是否会被阻挡
                            if (CharacterCollisionsSweep(
                                                characterPosition, // 当前角色位置
                                                characterRotation, // 当前角色旋转
                                                characterUp, // 向上检测
                                                MaxStepHeight - farthestHit.distance, // 向上检测距离
                                                out RaycastHit tmpUpObstructionHit, // 最近阻挡命中
                                                _internalCharacterHits) // 所有命中缓存
                                    <= 0)
                            {
                                // 进行内侧台阶检测……
                                bool innerStepValid = false;
                                RaycastHit innerStepHit;

                                if (AllowSteppingWithoutStableGrounding)
                                {
                                    innerStepValid = true;
                                }
                                else
                                {
                                    // 位于台阶高度处的胶囊体中心
                                    if (CharacterCollisionsRaycast(
                                            characterPosition + Vector3.Project((characterPositionAtHit - characterPosition), characterUp),
                                            -characterUp,
                                            MaxStepHeight,
                                            out innerStepHit,
                                            _internalCharacterHits,
                                            true) > 0)
                                    {
                                        if (IsStableOnNormal(innerStepHit.normal))
                                        {
                                            innerStepValid = true;
                                        }
                                    }
                                }

                                if (!innerStepValid)
                                {
                                    // 位于台阶点内侧
                                    if (CharacterCollisionsRaycast(
                                            farthestHit.point + (innerHitDirection * SecondaryProbesHorizontal),
                                            -characterUp,
                                            MaxStepHeight,
                                            out innerStepHit,
                                            _internalCharacterHits,
                                            true) > 0)
                                    {
                                        if (IsStableOnNormal(innerStepHit.normal))
                                        {
                                            innerStepValid = true;
                                        }
                                    }
                                }

                                // 对台阶进行最终验证
                                if (innerStepValid)
                                {
                                    hitCollider = farthestHit.collider;
                                    foundValidStepPosition = true;
                                    return true;
                                }
                            }
                        }
                    }
                }

                // 如果该命中不是有效台阶，则丢弃
                if (!foundValidStepPosition)
                {
                    nbStepHits--;
                    if (farthestIndex < nbStepHits)
                    {
                        _internalCharacterHits[farthestIndex] = _internalCharacterHits[nbStepHits];
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 获取刚体某一点的真实线速度（考虑旋转带来的速度）
        /// </summary>
        public void GetVelocityFromRigidbodyMovement(Rigidbody interactiveRigidbody, Vector3 atPoint, float deltaTime, out Vector3 linearVelocity, out Vector3 angularVelocity)
        {
            if (deltaTime > 0f)
            {
                linearVelocity = interactiveRigidbody.velocity;
                angularVelocity = interactiveRigidbody.angularVelocity;
                if(interactiveRigidbody.isKinematic)
                {
                    CharacterPhysicsMover physicsMover = interactiveRigidbody.GetComponent<CharacterPhysicsMover>();
                    if (physicsMover)
                    {
                        linearVelocity = physicsMover.Velocity;
                        angularVelocity = physicsMover.AngularVelocity;
                    }
                }

                if (angularVelocity != Vector3.zero)
                {
                    Vector3 centerOfRotation = interactiveRigidbody.transform.TransformPoint(interactiveRigidbody.centerOfMass);

                    Vector3 centerOfRotationToPoint = atPoint - centerOfRotation;
                    Quaternion rotationFromInteractiveRigidbody = Quaternion.Euler(Mathf.Rad2Deg * angularVelocity * deltaTime);
                    Vector3 finalPointPosition = centerOfRotation + (rotationFromInteractiveRigidbody * centerOfRotationToPoint);
                    linearVelocity += (finalPointPosition - atPoint) / deltaTime;
                }
            }
            else
            {
                linearVelocity = default;
                angularVelocity = default;
                return;
            }
        }

        /// <summary>
        /// 判断某个碰撞体是否挂接了可交互的刚体
        /// </summary>
        private Rigidbody GetInteractiveRigidbody(Collider onCollider)
        {
            Rigidbody colliderAttachedRigidbody = onCollider.attachedRigidbody;
            if (colliderAttachedRigidbody)
            {
                if (colliderAttachedRigidbody.gameObject.GetComponent<CharacterPhysicsMover>())
                {
                    return colliderAttachedRigidbody;
                }

                if (!colliderAttachedRigidbody.isKinematic)
                {
                    return colliderAttachedRigidbody;
                }
            }
            return null;
        }

        /// <summary>
        /// 计算在指定 deltaTime 内把角色移动到目标位置所需的速度
        /// 适用于你希望在 UpdateVelocity 回调中以“位置”而不是“速度”来思考运动时
        /// </summary>
        public Vector3 GetVelocityForMovePosition(Vector3 fromPosition, Vector3 toPosition, float deltaTime)
        {
            return GetVelocityFromMovement(toPosition - fromPosition, deltaTime);
        }

        public Vector3 GetVelocityFromMovement(Vector3 movement, float deltaTime)
        {
            if (deltaTime <= 0f)
                return Vector3.zero;

            return movement / deltaTime;
        }

        /// <summary>
        /// 裁剪一个向量，使其受限于某个平面
        /// </summary>
        private void RestrictVectorToPlane(ref Vector3 vector, Vector3 toPlane)
        {
            if (vector.x > 0 != toPlane.x > 0)
            {
                vector.x = 0;
            }
            if (vector.y > 0 != toPlane.y > 0)
            {
                vector.y = 0;
            }
            if (vector.z > 0 != toPlane.z > 0)
            {
                vector.z = 0;
            }
        }

        /// <summary>
        /// 检测角色胶囊体是否与任何可碰撞物体发生重叠
        /// </summary>
        /// <returns>返回有效重叠碰撞体数量。</returns>
        public int CharacterCollisionsOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, float inflate = 0f, bool acceptOnlyStableGroundLayer = false)
        {
            int queryLayers = CollidableLayers;
            if (acceptOnlyStableGroundLayer)
            {
                queryLayers = CollidableLayers & StableGroundLayers;
            }

            Vector3 bottom = position + (rotation * _characterTransformToCapsuleBottomHemi);
            Vector3 top = position + (rotation * _characterTransformToCapsuleTopHemi);
            if (inflate != 0f)
            {
                bottom += (rotation * Vector3.down * inflate);
                top += (rotation * Vector3.up * inflate);
            }

            int nbHits = 0;
            int nbUnfilteredHits = Physics.OverlapCapsuleNonAlloc(
                        bottom,
                        top,
                        Capsule.radius + inflate,
                        overlappedColliders,
                        queryLayers,
                        QueryTriggerInteraction.Ignore);

            // 过滤掉无效碰撞体
            nbHits = nbUnfilteredHits;
            for (int i = nbUnfilteredHits - 1; i >= 0; i--)
            {
                if (!CheckIfColliderValidForCollisions(overlappedColliders[i]))
                {
                    nbHits--;
                    if (i < nbHits)
                    {
                        overlappedColliders[i] = overlappedColliders[nbHits];
                    }
                }
            }

            return nbHits;
        }

        /// <summary>
        /// 检测角色胶囊体是否与任何物体重叠
        /// </summary>
        /// <returns>返回重叠碰撞体数量。</returns>
        public int CharacterOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, LayerMask layers, QueryTriggerInteraction triggerInteraction, float inflate = 0f)
        {
            Vector3 bottom = position + (rotation * _characterTransformToCapsuleBottomHemi);
            Vector3 top = position + (rotation * _characterTransformToCapsuleTopHemi);
            if (inflate != 0f)
            {
                bottom += (rotation * Vector3.down * inflate);
                top += (rotation * Vector3.up * inflate);
            }

            int nbHits = 0;
            int nbUnfilteredHits = Physics.OverlapCapsuleNonAlloc(
                        bottom,
                        top,
                        Capsule.radius + inflate,
                        overlappedColliders,
                        layers,
                        triggerInteraction);

            // 过滤掉角色胶囊体自身
            nbHits = nbUnfilteredHits;
            for (int i = nbUnfilteredHits - 1; i >= 0; i--)
            {
                if (overlappedColliders[i] == Capsule)
                {
                    nbHits--;
                    if (i < nbHits)
                    {
                        overlappedColliders[i] = overlappedColliders[nbHits];
                    }
                }
            }

            return nbHits;
        }

        /// <summary>
        /// 对胶囊体体积做 sweep，以检测碰撞命中
        /// </summary>
        /// <returns>返回有效命中数量。</returns>
        public int CharacterCollisionsSweep(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, float inflate = 0f, bool acceptOnlyStableGroundLayer = false)
        {
            int queryLayers = CollidableLayers;
            if (acceptOnlyStableGroundLayer)
            {
                queryLayers = CollidableLayers & StableGroundLayers;
            }

            Vector3 bottom = position + (rotation * _characterTransformToCapsuleBottomHemi) - (direction * SweepProbingBackstepDistance);
            Vector3 top = position + (rotation * _characterTransformToCapsuleTopHemi) - (direction * SweepProbingBackstepDistance);
            if (inflate != 0f)
            {
                bottom += (rotation * Vector3.down * inflate);
                top += (rotation * Vector3.up * inflate);
            }

            // 执行 capsule cast
            int nbHits = 0;
            int nbUnfilteredHits = Physics.CapsuleCastNonAlloc(
                    bottom,
                    top,
                    Capsule.radius + inflate,
                    direction,
                    hits,
                    distance + SweepProbingBackstepDistance,
                    queryLayers,
                    QueryTriggerInteraction.Ignore);

            // 初始化最近命中并过滤结果
            closestHit = new RaycastHit();
            float closestDistance = Mathf.Infinity;
            nbHits = nbUnfilteredHits;
            for (int i = nbUnfilteredHits - 1; i >= 0; i--)
            {
                hits[i].distance -= SweepProbingBackstepDistance;

                RaycastHit hit = hits[i];
                float hitDistance = hit.distance;

                // 过滤掉无效命中
                if (hitDistance <= 0f || !CheckIfColliderValidForCollisions(hit.collider))
                {
                    nbHits--;
                    if (i < nbHits)
                    {
                        hits[i] = hits[nbHits];
                    }
                }
                else
                {
                    // 找到最近的有效命中
                    if (hitDistance < closestDistance)
                    {
                        closestHit = hit;
                        closestDistance = hitDistance;
                    }
                }
            }

            return nbHits;
        }

        /// <summary>
        /// 对胶囊体体积做 sweep，以检测命中
        /// </summary>
        /// <returns>返回命中数量。</returns>
        public int CharacterSweep(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, LayerMask layers, QueryTriggerInteraction triggerInteraction, float inflate = 0f)
        {
            closestHit = new RaycastHit();

            Vector3 bottom = position + (rotation * _characterTransformToCapsuleBottomHemi);
            Vector3 top = position + (rotation * _characterTransformToCapsuleTopHemi);
            if (inflate != 0f)
            {
                bottom += (rotation * Vector3.down * inflate);
                top += (rotation * Vector3.up * inflate);
            }

            // 执行 capsule cast
            int nbHits = 0;
            int nbUnfilteredHits = Physics.CapsuleCastNonAlloc(
                bottom,
                top,
                Capsule.radius + inflate,
                direction,
                hits,
                distance,
                layers,
                triggerInteraction);

            // 初始化最近命中并过滤结果
            float closestDistance = Mathf.Infinity;
            nbHits = nbUnfilteredHits;
            for (int i = nbUnfilteredHits - 1; i >= 0; i--)
            {
                RaycastHit hit = hits[i];

                // 过滤掉角色胶囊体
                if (hit.distance <= 0f || hit.collider == Capsule)
                {
                    nbHits--;
                    if (i < nbHits)
                    {
                        hits[i] = hits[nbHits];
                    }
                }
                else
                {
                    // 找到最近的有效命中
                    float hitDistance = hit.distance;
                    if (hitDistance < closestDistance)
                    {
                        closestHit = hit;
                        closestDistance = hitDistance;
                    }
                }
            }

            return nbHits;
        }

        /// <summary>
        /// 沿角色向下方向对角色体积做 cast，以检测地面
        /// </summary>
        /// <returns>如果找到有效地面命中，返回 true。</returns>
        private bool CharacterGroundSweep(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            closestHit = new RaycastHit();

            // 执行 capsule cast
            int nbUnfilteredHits = Physics.CapsuleCastNonAlloc(
                position + (rotation * _characterTransformToCapsuleBottomHemi) - (direction * GroundProbingBackstepDistance),
                position + (rotation * _characterTransformToCapsuleTopHemi) - (direction * GroundProbingBackstepDistance),
                Capsule.radius,
                direction,
                _internalCharacterHits,
                distance + GroundProbingBackstepDistance,
                CollidableLayers & StableGroundLayers,
                QueryTriggerInteraction.Ignore);

            // 初始化最近命中并过滤结果
            bool foundValidHit = false;
            float closestDistance = Mathf.Infinity;
            for (int i = 0; i < nbUnfilteredHits; i++)
            {
                RaycastHit hit = _internalCharacterHits[i];
                float hitDistance = hit.distance;

                // 找到最近的有效命中
                if (hitDistance > 0f && CheckIfColliderValidForCollisions(hit.collider))
                {
                    if (hitDistance < closestDistance)
                    {
                        closestHit = hit;
                        closestHit.distance -= GroundProbingBackstepDistance;
                        closestDistance = hitDistance;

                        foundValidHit = true;
                    }
                }
            }

            return foundValidHit;
        }

        /// <summary>
        /// 用射线检测碰撞命中
        /// </summary>
        /// <returns>返回有效射线命中数量。</returns>
        public int CharacterCollisionsRaycast(Vector3 position, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, bool acceptOnlyStableGroundLayer = false)
        {
            int queryLayers = CollidableLayers;
            if (acceptOnlyStableGroundLayer)
            {
                queryLayers = CollidableLayers & StableGroundLayers;
            }

            // 执行 raycast
            int nbHits = 0;
            int nbUnfilteredHits = Physics.RaycastNonAlloc(
                position,
                direction,
                hits,
                distance,
                queryLayers,
                QueryTriggerInteraction.Ignore);

            // 初始化最近命中并过滤结果
            closestHit = new RaycastHit();
            float closestDistance = Mathf.Infinity;
            nbHits = nbUnfilteredHits;
            for (int i = nbUnfilteredHits - 1; i >= 0; i--)
            {
                RaycastHit hit = hits[i];
                float hitDistance = hit.distance;

                // 过滤掉无效命中
                if (hitDistance <= 0f ||
                    !CheckIfColliderValidForCollisions(hit.collider))
                {
                    nbHits--;
                    if (i < nbHits)
                    {
                        hits[i] = hits[nbHits];
                    }
                }
                else
                {
                    // 找到最近的有效命中
                    if (hitDistance < closestDistance)
                    {
                        closestHit = hit;
                        closestDistance = hitDistance;
                    }
                }
            }

            return nbHits;
        }
    }
}
