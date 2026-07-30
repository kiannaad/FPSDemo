using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class CharacterAnimationGraph : IDisposable
    {
        private static readonly string[] compositionOrder =
        {
            "AnimatorController",
            "WeaponPose",
            "UpperBodyWeaponAction",
            "AimAdditive",
            "AdditiveReaction",
            "LeftHandIK",
        };
        private const float MovingThreshold = 0.1f;
        private const float SprintThreshold = 4f;
        private const float StopPoseDuration = 0.15f;
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveYParameter = Animator.StringToHash("MoveY");
        private static readonly int VelocityParameter = Animator.StringToHash("Velocity");
        private static readonly int MovingParameter = Animator.StringToHash("Moving");
        private static readonly int InAirParameter = Animator.StringToHash("InAir");
        private static readonly int SprintingParameter = Animator.StringToHash("Sprinting");
        private readonly Animator animator;
        private readonly AnimatorCullingMode previousCullingMode;
        private readonly PlayableGraph playableGraph;
        private PlayableOutput animatorOutput;
        private readonly Playable animatorControllerSource;
        private readonly AnimationGraphContext context;
        private readonly IAnimationPlayableNode rootNode;
        private Playable rootPlayable;
        private readonly WeaponAnimationDefinitionResolver weaponDefinitionResolver;
        private readonly WeaponLayerBlendNode weaponLayerBlendNode;
        private readonly ActionNode equipActionNode;
        private readonly ActionNode unequipActionNode;
        private readonly ActionNode fireActionNode;
        private readonly ActionNode reloadActionNode;
        private readonly PriorityNode upperBodyActionNode;
        private readonly AimOffsetNode aimOffsetNode;
        private readonly RecoilReactionNode recoilReactionNode;
        private readonly HumanoidLeftHandIkNode leftHandIkNode;
        private readonly Transform observerSpine;
        private readonly Transform observerChest;
        private WeaponAnimationDefinition appliedWeaponDefinition;
        private WeaponEquipmentSnapshot appliedWeaponSnapshot;
        private bool hasConsumableLeftHandBinding;
        private bool wasMoving;
        private float stopPoseElapsed;
        private string currentLocomotionState = "Idle";
        private bool isDisposed;

        public event Action<ActionPresentationEnded> PresentationEnded;

        public CharacterAnimationGraph(Animator animator, CharacterAnimationConfig config)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (config == null || !config.IsValid) throw new ArgumentException("A valid character animation config is required.", nameof(config));

            this.animator = animator;
            previousCullingMode = animator.cullingMode;
            playableGraph = animator.playableGraph;
            if (!playableGraph.IsValid() || playableGraph.GetOutputCount() == 0)
            {
                throw new InvalidOperationException("Animator must own a valid PlayableGraph output before the upper-body graph is initialized.");
            }

            animatorOutput = playableGraph.GetOutput(0);
            animatorControllerSource = animatorOutput.GetSourcePlayable();
            if (!animatorControllerSource.IsValid())
            {
                throw new InvalidOperationException("Animator PlayableGraph output has no valid controller source.");
            }

            context = new AnimationGraphContext(animator, playableGraph);
            var animatorControllerNode = new AnimatorControllerPoseNode(animatorControllerSource);
            weaponDefinitionResolver = new WeaponAnimationDefinitionResolver(config.WeaponDefinitions);
            AvatarMask upperBodyMask = CreateUpperBodyMask();
            weaponLayerBlendNode = new WeaponLayerBlendNode(animatorControllerNode, upperBodyMask);
            WeaponAnimationDefinition initialWeaponDefinition = FindInitialWeaponDefinition(config);
            if (initialWeaponDefinition.Fire == null || !initialWeaponDefinition.Fire.IsValid
                || initialWeaponDefinition.Reload == null || !initialWeaponDefinition.Reload.IsValid)
            {
                throw new ArgumentException("The initial weapon definition requires valid Fire and Reload action assets.", nameof(config));
            }

            equipActionNode = new ActionNode(initialWeaponDefinition.Equip, 30);
            unequipActionNode = new ActionNode(initialWeaponDefinition.Unequip, 40);
            fireActionNode = new ActionNode(initialWeaponDefinition.Fire, 10);
            reloadActionNode = new ActionNode(initialWeaponDefinition.Reload, 35);
            equipActionNode.PresentationEnded += OnActionPresentationEnded;
            unequipActionNode.PresentationEnded += OnActionPresentationEnded;
            fireActionNode.PresentationEnded += OnActionPresentationEnded;
            reloadActionNode.PresentationEnded += OnActionPresentationEnded;
            upperBodyActionNode = new PriorityNode(fireActionNode, equipActionNode, reloadActionNode, unequipActionNode);
            var layered = new LayeredBlendPerBoneNode(
                weaponLayerBlendNode,
                // The imported AK package has a real weapon-mechanism fire clip but no compatible
                // character fire pose. Keep this channel authoritative for action lifetime/notifies
                // without overriding locomotion; the visible impulse belongs to AdditiveReaction.
                new LayeredAnimationInput(
                    upperBodyActionNode,
                    upperBodyMask,
                    context => upperBodyActionNode.ActiveAction == reloadActionNode ? 1f : 0f,
                    false,
                    "UpperBodyWeaponAction"));

            aimOffsetNode = new AimOffsetNode(layered, animator);
            recoilReactionNode = new RecoilReactionNode(aimOffsetNode, animator);
            IAnimationPlayableNode root = recoilReactionNode;

            Transform spine = CharacterBoneResolver.Resolve(
                animator,
                HumanBodyBones.Spine,
                "Spine");
            Transform chest = CharacterBoneResolver.Resolve(
                    animator,
                    HumanBodyBones.UpperChest,
                    "UpperChest")
                ?? CharacterBoneResolver.Resolve(
                    animator,
                    HumanBodyBones.Chest,
                    "Chest");
            observerSpine = chest != null
                ? spine
                : CharacterBoneResolver.Resolve(
                    animator,
                    HumanBodyBones.Neck,
                    "Neck") ?? spine;
            observerChest = chest ?? CharacterBoneResolver.Resolve(
                animator,
                HumanBodyBones.Head,
                "Head");

            Transform leftHand = animator.isHuman
                ? CharacterBoneResolver.Resolve(
                    animator,
                    HumanBodyBones.LeftHand,
                    "Left_Hand")
                : null;
            if (animator.isHuman && leftHand != null)
            {
                leftHandIkNode = new HumanoidLeftHandIkNode(root);
                root = leftHandIkNode;
            }

            rootNode = root;
            rootNode.Initialize(context);
            context.BeginEvaluateFrame();
            rootPlayable = rootNode.Evaluate(context).Playable;
            if (!rootPlayable.IsValid())
            {
                throw new InvalidOperationException("Upper-body graph produced an invalid root playable.");
            }

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            playableGraph.DestroyOutput(animatorOutput);
            animatorOutput = AnimationPlayableOutput.Create(
                playableGraph,
                "CharacterUpperBody",
                animator);
            animatorOutput.SetSourcePlayable(rootPlayable);
            playableGraph.Play();
        }

        public AnimationGraphContext Context => context;
        public string CurrentLocomotionState => currentLocomotionState;
        public bool IsInitialized => !isDisposed
            && playableGraph.IsValid()
            && animatorOutput.IsOutputValid()
            && rootPlayable.IsValid();
        public WeaponId EquippedWeaponId => appliedWeaponSnapshot.EquippedWeaponId;
        public uint WeaponGeneration => appliedWeaponSnapshot.Generation;
        public WeaponLayerBlendNode WeaponLayerBlend => weaponLayerBlendNode;
        public AimOffsetNode AimOffset => aimOffsetNode;
        public ActionNode FireAction => fireActionNode;
        public ActionNode ReloadAction => reloadActionNode;
        public ActionNode EquipAction => equipActionNode;
        public ActionNode UnequipAction => unequipActionNode;
        public RecoilReactionNode RecoilReaction => recoilReactionNode;
        public HumanoidLeftHandIkNode LeftHandIk => leftHandIkNode;
        public static IReadOnlyList<string> CompositionOrder => compositionOrder;

        public void ApplyWeaponEquipment(
            WeaponEquipmentSnapshot snapshot,
            WeaponPresentationBinding binding = null,
            bool force = false)
        {
            if (!force
                && snapshot.Generation == appliedWeaponSnapshot.Generation
                && snapshot.EquippedWeaponId == appliedWeaponSnapshot.EquippedWeaponId)
            {
                return;
            }

            bool wasEquipped = appliedWeaponSnapshot.IsEquipped;
            appliedWeaponSnapshot = snapshot;
            Context.ActiveWeaponGeneration = snapshot.Generation;
            EndActivePresentation(ActionPresentationEndReason.EquipmentChanged);
            recoilReactionNode.Cancel();
            if (!snapshot.IsEquipped)
            {
                appliedWeaponDefinition = null;
                hasConsumableLeftHandBinding = false;
                Context.AimWeight = 0f;
                Context.LeftHandIkWeight = 0f;
                leftHandIkNode?.SetBinding(null, 0.08f);
                weaponLayerBlendNode.SetTarget(null, 0.15f);
                if (wasEquipped)
                {
                    unequipActionNode.Request(CreateEquipmentActionId(snapshot.Generation, false));
                }
                return;
            }

            if (!weaponDefinitionResolver.TryResolve(snapshot.EquippedWeaponId, out WeaponAnimationDefinition definition))
            {
                appliedWeaponDefinition = null;
                hasConsumableLeftHandBinding = false;
                Context.AimWeight = 0f;
                Context.LeftHandIkWeight = 0f;
                leftHandIkNode?.SetBinding(null, 0.08f);
                weaponLayerBlendNode.SetTarget(null, 0.15f);
                Context.RecordDebugEvent(nameof(CharacterAnimationGraph), "WeaponDefinitionFallback", snapshot.Generation);
                return;
            }

            appliedWeaponDefinition = definition;
            equipActionNode.Request(CreateEquipmentActionId(snapshot.Generation, true));
            var nextLayer = new WeaponAnimationLayer(definition, snapshot.Generation, () => currentLocomotionState);
            weaponLayerBlendNode.SetTarget(nextLayer, definition.BlendDuration);
            aimOffsetNode.Configure(
                definition.AimYawRange,
                definition.AimPitchUpRange,
                definition.AimPitchDownRange,
                definition.AimWeight,
                definition.AimSmoothingTime);
            Context.AimWeight = 1f;
            bool hasGrip = leftHandIkNode != null
                && binding != null
                && binding.CanConsume(snapshot.Generation);
            hasConsumableLeftHandBinding = hasGrip;
            Context.LeftHandIkWeight = hasGrip ? 1f : 0f;
            leftHandIkNode?.SetBinding(binding, definition.LeftHandIkSmoothingTime);
            recoilReactionNode.Configure(definition.RecoilImpulse, definition.RecoilMaxPitch, definition.RecoilDecayTime);
            if (!hasGrip)
            {
                Context.RecordDebugEvent(nameof(CharacterAnimationGraph), "LeftHandIkDegraded", snapshot.Generation);
            }
        }

        public void BeginWeaponEquipmentTransition(WeaponEquipmentSnapshot snapshot)
        {
            appliedWeaponSnapshot = snapshot;
            Context.ActiveWeaponGeneration = snapshot.Generation;
            EndActivePresentation(ActionPresentationEndReason.EquipmentChanged);
            recoilReactionNode.Cancel();
            appliedWeaponDefinition = null;
            Context.AimWeight = 0f;
            Context.LeftHandIkWeight = 0f;
            leftHandIkNode?.SetBinding(null, 0.08f);
        }

        public void ApplyWeaponFallback(WeaponEquipmentSnapshot snapshot, string missingField)
        {
            BeginWeaponEquipmentTransition(snapshot);
            weaponLayerBlendNode.SetTarget(null, 0.15f);
            Context.RecordDebugEvent(
                nameof(CharacterAnimationGraph),
                $"WeaponPresentationFallback:{missingField}",
                snapshot.Generation);
        }

        public void SetAimInput(float yaw, float pitch)
        {
            Context.AimYaw = yaw;
            Context.AimPitch = pitch;
        }

        public bool StartWeaponAction(WeaponActionFact fact)
        {
            if (!CanConsume(fact) || fact.Phase != WeaponActionPhase.Started)
            {
                return false;
            }

            ActionNode actionNode = fact.Kind == WeaponActionKind.Fire
                ? fireActionNode
                : fact.Kind == WeaponActionKind.Reload
                    ? reloadActionNode
                    : null;
            if (actionNode == null)
            {
                return false;
            }

            actionNode.Request(fact.ActionId);
            Context.RecordDebugEvent(nameof(CharacterAnimationGraph), $"UpperBodyWeaponAction:Started:{fact.ActionId}");
            return true;
        }

        public bool CommitFireReaction(WeaponActionFact fact)
        {
            if (!CanConsume(fact) || fact.Kind != WeaponActionKind.Fire)
            {
                return false;
            }

            bool triggered = recoilReactionNode.Trigger(fact.ActionId);
            if (triggered)
            {
                Context.RecordDebugEvent(nameof(CharacterAnimationGraph), $"AdditiveReaction:FireCommitted:{fact.ActionId}");
            }
            return triggered;
        }

        public bool EndWeaponAction(WeaponActionFact fact)
        {
            if (fact.ActionId == 0ul)
            {
                return false;
            }

            ActionPresentationEndReason reason = MapEndReason(fact);
            ActionNode actionNode = fact.Kind == WeaponActionKind.Reload ? reloadActionNode : fireActionNode;
            bool ended = actionNode.End(fact.ActionId, reason);
            if (fact.Phase == WeaponActionPhase.Cancelled
                && fact.EndReason != WeaponActionEndReason.Superseded)
            {
                recoilReactionNode.Cancel();
            }
            return ended;
        }

        public void Update(float deltaTime)
        {
            if (!IsInitialized)
            {
                return;
            }

            UpdateAnimatorControllerParameters();
            UpdateLocomotionPresentationState(deltaTime);
            ActionNode activeAction = upperBodyActionNode.ActiveAction;
            float actionIkWeight = activeAction != null
                ? activeAction.SampleNamedCurve("MaskLeftHandIK", 1f)
                : 1f;
            float attachHandWeight = activeAction != null
                ? activeAction.SampleNamedCurve("MaskAttachHand", 1f)
                : 1f;
            Context.LeftHandIkWeight = appliedWeaponSnapshot.IsEquipped && hasConsumableLeftHandBinding
                ? Mathf.Clamp01(actionIkWeight * attachHandWeight)
                : 0f;
            Context.WeaponBoneWeight = activeAction != null
                ? Mathf.Clamp01(activeAction.SampleNamedCurve("WeaponBoneWeight", 1f))
                : 1f;
            context.DeltaTime = deltaTime;
            context.ElapsedTime += Mathf.Max(0f, deltaTime);
            rootNode.Update(context, deltaTime);
            context.BeginEvaluateFrame();
            Playable evaluatedRoot = rootNode.Evaluate(context).Playable;
            if (evaluatedRoot.IsValid() && !evaluatedRoot.Equals(animatorOutput.GetSourcePlayable()))
            {
                rootPlayable = evaluatedRoot;
                animatorOutput.SetSourcePlayable(rootPlayable);
            }
            ApplyObserverAim();
        }

        public AnimationGraphDebugSnapshot GetDebugSnapshot()
        {
            var events = new AnimationDebugEvent[context.DebugEvents.Count];
            for (int i = 0; i < events.Length; i++)
            {
                events[i] = context.DebugEvents[i];
            }

            return new AnimationGraphDebugSnapshot(
                rootNode.GetDebugSnapshot(),
                currentLocomotionState,
                1f,
                context.DebugActiveAction,
                context.DebugActiveActionWeight,
                events);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            equipActionNode.PresentationEnded -= OnActionPresentationEnded;
            unequipActionNode.PresentationEnded -= OnActionPresentationEnded;
            fireActionNode.PresentationEnded -= OnActionPresentationEnded;
            reloadActionNode.PresentationEnded -= OnActionPresentationEnded;
            if (animatorOutput.IsOutputValid())
            {
                playableGraph.DestroyOutput(animatorOutput);
            }

            if (playableGraph.IsValid() && animatorControllerSource.IsValid())
            {
                animatorOutput = AnimationPlayableOutput.Create(
                    playableGraph,
                    "AnimatorController",
                    animator);
                animatorOutput.SetSourcePlayable(animatorControllerSource);
            }

            weaponLayerBlendNode.DetachBaseInput();
            rootNode.Destroy();
            if (playableGraph.IsValid() && rootPlayable.IsValid())
            {
                playableGraph.DestroySubgraph(rootPlayable);
            }

            rootPlayable = Playable.Null;
            animator.cullingMode = previousCullingMode;
            isDisposed = true;
        }

        private void ApplyObserverAim()
        {
            AnimationGraphContext context = Context;
            if (observerSpine == null || context.ObserverAimWeight <= 0f)
            {
                return;
            }

            bool hasChest = observerChest != null && observerChest != observerSpine;
            float spineShare = hasChest ? 0.4f : 0.65f;
            ApplyObserverAimRotation(observerSpine, context, spineShare);
            if (hasChest)
            {
                ApplyObserverAimRotation(observerChest, context, 0.6f);
            }
        }

        private static void ApplyObserverAimRotation(
            Transform bone,
            AnimationGraphContext context,
            float share)
        {
            Quaternion poseRotation = bone.localRotation;
            Quaternion aimOffset = Quaternion.Euler(
                -context.ObserverAimPitch * share,
                context.ObserverAimYawOffset * share,
                0f);
            Quaternion aimedRotation = poseRotation * aimOffset;
            bone.localRotation = Quaternion.Slerp(
                poseRotation,
                aimedRotation,
                Mathf.Clamp01(context.ObserverAimWeight));
        }

        private void UpdateAnimatorControllerParameters()
        {
            Vector2 localMove = new Vector2(context.LocalVelocity.x, context.LocalVelocity.z);
            Vector2 direction = localMove.sqrMagnitude > 0.0001f ? localMove.normalized : Vector2.zero;
            bool isMoving = context.IsGrounded && context.MoveSpeed > MovingThreshold;
            bool isInAir = !context.IsGrounded;
            float sprinting = isMoving && context.MoveSpeed > SprintThreshold ? 1f : 0f;
            animator.SetFloat(MoveXParameter, direction.x);
            animator.SetFloat(MoveYParameter, direction.y);
            animator.SetFloat(VelocityParameter, direction.magnitude);
            animator.SetBool(MovingParameter, isMoving);
            animator.SetBool(InAirParameter, isInAir);
            animator.SetFloat(SprintingParameter, sprinting);
        }

        private void UpdateLocomotionPresentationState(float deltaTime)
        {
            bool isMoving = context.IsGrounded && context.MoveSpeed > MovingThreshold;
            if (!context.IsGrounded)
            {
                currentLocomotionState = "Air";
                stopPoseElapsed = 0f;
            }
            else if (isMoving)
            {
                currentLocomotionState = context.MoveSpeed > SprintThreshold ? "Sprint" : "Move";
                stopPoseElapsed = 0f;
            }
            else
            {
                if (wasMoving)
                {
                    stopPoseElapsed = Mathf.Epsilon;
                }
                else if (stopPoseElapsed > 0f)
                {
                    stopPoseElapsed += Mathf.Max(0f, deltaTime);
                }

                currentLocomotionState = stopPoseElapsed > 0f && stopPoseElapsed < StopPoseDuration
                    ? "Stop"
                    : "Idle";
                if (stopPoseElapsed >= StopPoseDuration)
                {
                    stopPoseElapsed = 0f;
                }
            }

            wasMoving = isMoving;
            context.DebugLocomotionState = currentLocomotionState;
        }

        private bool CanConsume(WeaponActionFact fact)
        {
            return fact.IsValid
                && appliedWeaponDefinition != null
                && fact.Generation == appliedWeaponSnapshot.Generation
                && fact.WeaponId == appliedWeaponSnapshot.EquippedWeaponId;
        }

        private void EndActivePresentation(ActionPresentationEndReason reason)
        {
            EndActionPresentation(fireActionNode, reason);
            EndActionPresentation(equipActionNode, reason);
            EndActionPresentation(unequipActionNode, reason);
        }

        private static void EndActionPresentation(ActionNode actionNode, ActionPresentationEndReason reason)
        {
            if (actionNode.IsActive)
            {
                actionNode.End(actionNode.RequestId, reason);
            }
            else if (actionNode.PendingRequestId > 0ul)
            {
                actionNode.End(actionNode.PendingRequestId, reason);
            }
        }

        private static ulong CreateEquipmentActionId(uint generation, bool equip)
        {
            return ((ulong)generation << 32) | (equip ? 0xE001ul : 0xE002ul);
        }

        private void OnActionPresentationEnded(ActionPresentationEnded ended)
        {
            Context.RecordDebugEvent(nameof(CharacterAnimationGraph), $"PresentationEnded:{ended.RequestId}:{ended.Reason}");
            PresentationEnded?.Invoke(ended);
        }

        private static ActionPresentationEndReason MapEndReason(WeaponActionFact fact)
        {
            if (fact.Phase == WeaponActionPhase.Completed)
            {
                return ActionPresentationEndReason.GameplayCompleted;
            }

            switch (fact.EndReason)
            {
                case WeaponActionEndReason.EquipmentChanged:
                case WeaponActionEndReason.Unequipped:
                    return ActionPresentationEndReason.EquipmentChanged;
                case WeaponActionEndReason.OwnerDisposed:
                    return ActionPresentationEndReason.OwnerDisposed;
                default:
                    return ActionPresentationEndReason.GameplayCancelled;
            }
        }

        private static WeaponAnimationDefinition FindInitialWeaponDefinition(CharacterAnimationConfig config)
        {
            WeaponAnimationDefinition[] definitions = config.WeaponDefinitions;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && definitions[i].IsValid)
                {
                    return definitions[i];
                }
            }

            throw new ArgumentException("A valid weapon animation definition is required.", nameof(config));
        }

        private static AvatarMask CreateUpperBodyMask()
        {
            var mask = new AvatarMask();
            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            return mask;
        }

        private sealed class AnimatorControllerPoseNode : AnimationNodeBase
        {
            private readonly Playable sourcePlayable;

            public AnimatorControllerPoseNode(Playable sourcePlayable)
            {
                this.sourcePlayable = sourcePlayable;
            }

            public override AnimationPoseHandle Evaluate(AnimationGraphContext graphContext)
            {
                return new AnimationPoseHandle(
                    sourcePlayable,
                    1f,
                    graphContext.EvaluateFrameId,
                    nameof(AnimatorControllerPoseNode));
            }

            public override AnimationNodeDebugSnapshot GetDebugSnapshot()
            {
                return new AnimationNodeDebugSnapshot(
                    nameof(AnimatorControllerPoseNode),
                    sourcePlayable.IsValid(),
                    1f,
                    0);
            }

            protected override void OnInitialize(AnimationGraphContext graphContext)
            {
                if (!sourcePlayable.IsValid())
                {
                    throw new InvalidOperationException("Animator controller source playable is invalid.");
                }
            }
        }
    }
}
