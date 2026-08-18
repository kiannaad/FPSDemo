using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    public class Pawn : Actor, ICharacterIntentSink, ICharacterMovementCommandSource
    {
        private readonly List<ActorComponent> declaredComponents;
        private readonly RecoilComponent recoilComponent;

        private Controller controller;
        private Vector3 movementInput;
        private Vector3 pendingForce;
        private Vector3 pendingImpulse;
        private bool pendingJump;
        private bool sprintRequested;
        private bool rootDestroyed;

        public Pawn()
            : this(null, Array.Empty<ActorComponent>())
        {
        }

        public Pawn(GameObject root)
            : this(root, Array.Empty<ActorComponent>())
        {
        }

        public Pawn(GameObject root, IEnumerable<ActorComponent> components)
        {
            Root = root;
            Transform = root != null ? root.transform : null;
            recoilComponent = new RecoilComponent(this);

            declaredComponents = components == null
                ? new List<ActorComponent>()
                : new List<ActorComponent>(components);
            if (declaredComponents.Exists(component => component == null))
            {
                throw new ArgumentException("Pawn components cannot contain null.", nameof(components));
            }
        }

        public GameObject Root { get; }



        public RecoilProfile RecoilProfile { get; private set; }
public RecoilComponent Recoil => recoilComponent;

        public Vector2 RecoilRotationOffsetDegrees { get; private set; }

        public Pose WeaponRecoilPose { get; private set; } = new Pose(Vector3.zero, Quaternion.identity);

        public float CameraShakeSample { get; private set; }

        public int RecoilShotSequence { get; private set; }


        public Transform Transform { get; }

        public Controller Controller => controller;

        public AbilitySystemComponent AbilitySystem { get; private set; }

        public Quaternion ControlRotation { get; private set; } = Quaternion.identity;



        public Quaternion EffectivePresentationRotation =>
            PresentationRotation * Quaternion.Euler(
                -RecoilRotationOffsetDegrees.x,
                RecoilRotationOffsetDegrees.y,
                0f);
public Quaternion PresentationRotation { get; private set; } = Quaternion.identity;

        public Quaternion SimulatedRotation { get; private set; } = Quaternion.identity;

        public bool IsAiming { get; private set; }

        public Pose AimPointOffset { get; private set; } = new Pose(Vector3.zero, Quaternion.identity);

        public Transform CurrentWeaponAimPoint { get; private set; }

        public Vector2 ViewAnglesDegrees { get; private set; }

        public Vector2 ViewDeltaDegrees { get; private set; }

        public float LookLayerWeight { get; private set; } = 1f;

        public float LeanAngleDegrees { get; private set; }

        public bool UseFreeAim { get; private set; }

        public Pose RecoilOffset { get; private set; } = new Pose(Vector3.zero, Quaternion.identity);

        public bool WeaponCollisionHasHit { get; private set; }

        public float WeaponCollisionDistance { get; private set; }

        public bool IsRootDestroyed => rootDestroyed || Root == null;

        public virtual void SettingController(Controller nextController)
        {
            controller = nextController ?? throw new ArgumentNullException(nameof(nextController));
        }

        public virtual void ClearingController(Controller expectedController)
        {
            if (ReferenceEquals(controller, expectedController))
            {
                controller = null;
            }
        }

        public void BindingAbilitySystem(AbilitySystemComponent abilitySystem)
        {
            if (abilitySystem == null)
            {
                throw new ArgumentNullException(nameof(abilitySystem));
            }

            if (AbilitySystem != null && !ReferenceEquals(AbilitySystem, abilitySystem))
            {
                throw new InvalidOperationException("Pawn is already bound to another AbilitySystemComponent.");
            }

            AbilitySystem = abilitySystem;
        }

        public void ClearingAbilitySystem(AbilitySystemComponent abilitySystem)
        {
            if (ReferenceEquals(AbilitySystem, abilitySystem))
            {
                AbilitySystem = null;
            }
        }

        public virtual void ApplyingControlRotation(Quaternion controlRotation)
        {
            ControlRotation = controlRotation;
        }

        public void ApplyingPresentationRotation(Quaternion presentationRotation)
        {
            PresentationRotation = IsFinite(presentationRotation)
                ? Quaternion.Normalize(presentationRotation)
                : Quaternion.identity;
        }

        private void LatchSimulatedRotation(float deltaTime)
        {
            SimulatedRotation = PresentationRotation;
        }

public void ResetRotationState()
        {
            ControlRotation = Quaternion.identity;
            PresentationRotation = Quaternion.identity;
            SimulatedRotation = Quaternion.identity;
        }

        public void ApplyingViewDelta(Vector2 viewDeltaDegrees)
        {
            ViewDeltaDegrees = viewDeltaDegrees;
        }

        public void SetLookLayerWeight(float lookLayerWeight)
        {
            LookLayerWeight = Mathf.Clamp01(lookLayerWeight);
        }

        public void SetAimAnimationFacts(bool isAiming, Pose aimPointOffset)
        {
            IsAiming = isAiming;
            AimPointOffset = aimPointOffset;
        }

        public void SetCurrentWeaponAimPoint(Transform aimPoint)
        {
            CurrentWeaponAimPoint = aimPoint;
        }
        public void SetAimingFromInput(bool isAiming)
        {
            SetAimAnimationFacts(isAiming, AimPointOffset);
        }

        public void SetAimingFromAbility(bool isAiming)
        {
            SetAimAnimationFacts(isAiming, AimPointOffset);
        }


        public void SetViewAnimationFacts(
            Vector2 viewAnglesDegrees,
            Vector2 viewDeltaDegrees,
            float leanAngleDegrees,
            bool useFreeAim)
        {
            ViewAnglesDegrees = viewAnglesDegrees;
            ViewDeltaDegrees = viewDeltaDegrees;
            LeanAngleDegrees = leanAngleDegrees;
            UseFreeAim = useFreeAim;
        }

        public void SetRecoilAnimationFact(Pose recoilOffset)
        {
            RecoilOffset = recoilOffset;
        }



        public void BindRecoilProfile(RecoilProfile profile)
        {
            RecoilProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            recoilComponent.Bind(profile);
        }

        public void AdvanceRecoil(float deltaTime)
        {
            recoilComponent.Advance(deltaTime);
        }

        public FireResult NotifySuccessfulShot()
        {
            return recoilComponent.ApplySuccessfulShot(IsAiming);
        }

        internal void SetRecoilFrameData(RecoilFrameData frameData)
        {
            RecoilRotationOffsetDegrees = frameData.RotationOffsetDegrees;
            WeaponRecoilPose = frameData.WeaponRecoilPose;
            CameraShakeSample = frameData.CameraShakeSample;
            RecoilShotSequence = frameData.ShotSequence;
            SetRecoilAnimationFact(frameData.WeaponRecoilPose);
        }


        public void SetWeaponCollisionAnimationFacts(bool hasHit, float distance)
        {
            WeaponCollisionHasHit = hasHit;
            WeaponCollisionDistance = distance;
        }

        public void SubmitControlIntent(in CharacterControlIntent intent)
        {
            movementInput = intent.MovementInput;
            pendingJump |= intent.JumpRequested;
            sprintRequested = intent.SprintRequested;
        }

        public CharacterMovementCommand ConsumeMovementCommand()
        {
            bool jumpRequested = pendingJump;
            pendingJump = false;
            return new CharacterMovementCommand(movementInput, jumpRequested, sprintRequested);
        }

        public Vector3 PeekingMovementInput() => movementInput;

        public void ClearingControlIntent()
        {
            movementInput = Vector3.zero;
            pendingJump = false;
            sprintRequested = false;
        }

        public void AddingForce(Vector3 force) => pendingForce += force;

        public Vector3 ConsumingForce()
        {
            Vector3 force = pendingForce;
            pendingForce = Vector3.zero;
            return force;
        }

        public void AddingImpulse(Vector3 impulse) => pendingImpulse += impulse;

        public Vector3 ConsumingImpulse()
        {
            Vector3 impulse = pendingImpulse;
            pendingImpulse = Vector3.zero;
            return impulse;
        }

        public void AddingJumpInput()
        {
            SubmitControlIntent(new CharacterControlIntent(movementInput, true, sprintRequested));
        }

        public void DestroyCandidate()
        {
            if (State != ActorState.Constructed)
            {
                throw new InvalidOperationException("Only an unregistered Pawn candidate can be destroyed directly.");
            }

            DestroyRoot();
        }

        protected override void OnInitialize()
        {
            for (int index = 0; index < declaredComponents.Count; index++)
            {
                AddComponent(declaredComponents[index]);
            }
        }

        protected override void OnBeginPlay()
        {
            Root?.SetActive(true);
        }

        protected override void OnEndPlay()
        {
            Root?.SetActive(false);
            ClearingControlIntent();
            ClearProceduralAnimationFacts();
        }

        protected override void OnShutdown()
        {
            Controller currentController = controller;
            controller = null;
            currentController?.NotifyPossessedActorUnregistered(this);
            AbilitySystem = null;
            ClearingControlIntent();
            ClearProceduralAnimationFacts();
            pendingForce = Vector3.zero;
            pendingImpulse = Vector3.zero;
            DestroyRoot();
        }

        private void DestroyRoot()
        {
            if (rootDestroyed || Root == null)
            {
                rootDestroyed = true;
                return;
            }

            rootDestroyed = true;
            Root.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(Root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private void ClearProceduralAnimationFacts()
        {
            IsAiming = false;
            AimPointOffset = new Pose(Vector3.zero, Quaternion.identity);
            CurrentWeaponAimPoint = null;
            ViewAnglesDegrees = Vector2.zero;
            ViewDeltaDegrees = Vector2.zero;
            LookLayerWeight = 1f;
            LeanAngleDegrees = 0f;
            UseFreeAim = false;
            RecoilOffset = new Pose(Vector3.zero, Quaternion.identity);
            WeaponCollisionHasHit = false;
            WeaponCollisionDistance = 0f;
            ResetRotationState();
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z)
                && !float.IsNaN(value.w) && !float.IsInfinity(value.w)
                && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > Mathf.Epsilon;
        }
    }
}
