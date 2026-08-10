using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    public class Pawn : Actor, ICharacterIntentSink, ICharacterMovementCommandSource
    {
        private readonly List<ActorComponent> declaredComponents;
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

        public Pawn(GameObject root, IEnumerable<ActorComponent> components)
        {
            Root = root;
            Transform = root != null ? root.transform : null;
            declaredComponents = components == null
                ? new List<ActorComponent>()
                : new List<ActorComponent>(components);
            if (declaredComponents.Exists(component => component == null))
            {
                throw new ArgumentException("Pawn components cannot contain null.", nameof(components));
            }
        }

        public GameObject Root { get; }

        public Transform Transform { get; }

        public Controller Controller => controller;

        public AbilitySystemComponent AbilitySystem { get; private set; }

        public Quaternion ControlRotation { get; private set; } = Quaternion.identity;

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
        }

        protected override void OnShutdown()
        {
            Controller currentController = controller;
            controller = null;
            currentController?.NotifyPossessedActorUnregistered(this);
            AbilitySystem = null;
            ClearingControlIntent();
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
    }
}
