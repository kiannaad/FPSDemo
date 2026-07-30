using System;
using CGame;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterWeaponAnimationBridge : IDisposable
    {
        private readonly Animator animator;
        private readonly CharacterAnimationGraph graph;
        private readonly IWeaponPresentationLoader presentationLoader;
        private readonly WeaponBindingLifecycle bindingLifecycle = new WeaponBindingLifecycle();
        private FirstPersonWeaponView firstPersonWeaponView;
        private WeaponEquipmentSnapshot appliedSnapshot;
        private WeaponPresentationInstance currentPresentation;
        private WeaponAnimationDefinition currentDefinition;
        private WeaponRuntime boundRuntime;
        private bool isDisposed;

        public CharacterWeaponAnimationBridge(
            Animator animator,
            CharacterAnimationConfig config,
            CharacterAnimationGraph graph,
            IWeaponPresentationLoader presentationLoader = null,
            bool enableFirstPersonWeaponView = false)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.presentationLoader = presentationLoader ?? new DirectWeaponPresentationLoader(config.WeaponDefinitions);
            firstPersonWeaponView = enableFirstPersonWeaponView ? new FirstPersonWeaponView() : null;
        }

        public WeaponPresentationBinding CurrentBinding => bindingLifecycle.NextBinding ?? bindingLifecycle.CurrentBinding;
        public WeaponPresentationInstance CurrentPresentation => currentPresentation;
        public WeaponBindingState BindingState => bindingLifecycle.State;

        public void SetFirstPersonViewEnabled(bool enabled)
        {
            if (enabled == (firstPersonWeaponView != null))
            {
                return;
            }

            if (!enabled)
            {
                firstPersonWeaponView.Dispose();
                firstPersonWeaponView = null;
                return;
            }

            firstPersonWeaponView = new FirstPersonWeaponView();
            if (currentDefinition != null)
            {
                firstPersonWeaponView.Equip(currentDefinition.PresentationPrefab);
            }
        }

        public void BindRuntime(WeaponRuntime runtime)
        {
            if (boundRuntime == runtime)
            {
                return;
            }

            if (boundRuntime != null)
            {
                boundRuntime.ActionChanged -= OnActionChanged;
                boundRuntime.FireCommitted -= OnFireCommitted;
            }

            boundRuntime = runtime;
            if (boundRuntime != null)
            {
                boundRuntime.ActionChanged += OnActionChanged;
                boundRuntime.FireCommitted += OnFireCommitted;
            }
        }

        public void Update(WeaponEquipmentSnapshot snapshot, float aimYaw, float aimPitch, float deltaTime)
        {
            if (isDisposed)
            {
                return;
            }

            if (bindingLifecycle.State == WeaponBindingState.Blending && !graph.WeaponLayerBlend.IsBlending)
            {
                bindingLifecycle.CompleteBlend();
            }

            if (snapshot.Generation != appliedSnapshot.Generation
                || snapshot.EquippedWeaponId != appliedSnapshot.EquippedWeaponId)
            {
                ApplyEquipment(snapshot);
            }

            graph.SetAimInput(aimYaw, aimPitch);
            currentPresentation?.ModelActionPlayer?.SetWeight(graph.Context.WeaponBoneWeight);
            currentPresentation?.ModelActionPlayer?.Advance(deltaTime);
            firstPersonWeaponView?.SetWeight(graph.Context.WeaponBoneWeight);
            firstPersonWeaponView?.Advance(deltaTime);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            BindRuntime(null);
            UnsubscribePresentation(currentPresentation);
            currentPresentation = null;
            currentDefinition = null;
            firstPersonWeaponView?.Dispose();
            bindingLifecycle.Dispose();
        }

        private void ApplyEquipment(WeaponEquipmentSnapshot snapshot)
        {
            UnsubscribePresentation(currentPresentation);
            currentPresentation = null;
            currentDefinition = null;
            firstPersonWeaponView?.Dispose();
            appliedSnapshot = snapshot;
            WeaponPresentationLoadTicket ticket = bindingLifecycle.Begin(snapshot);
            if (!snapshot.IsEquipped)
            {
                graph.ApplyWeaponEquipment(snapshot, null, true);
                return;
            }

            graph.BeginWeaponEquipmentTransition(snapshot);
            IDisposable cancellation = presentationLoader.BeginLoad(snapshot.EquippedWeaponId, ticket, OnPresentationLoaded);
            bindingLifecycle.SetCancellation(ticket, cancellation);
        }

        private void OnPresentationLoaded(WeaponPresentationLoadTicket ticket, WeaponPresentationLoadResult result)
        {
            if (!bindingLifecycle.CanAccept(ticket))
            {
                result.Lease?.Dispose();
                return;
            }

            WeaponEquipmentSnapshot snapshot = appliedSnapshot;
            if (!result.IsSuccess)
            {
                bindingLifecycle.Reject(ticket, result.Lease);
                graph.ApplyWeaponFallback(snapshot, result.MissingField ?? "Unknown");
                RecordResourceDiagnostic(snapshot, result.DefinitionId, result.MissingField ?? "Unknown");
                return;
            }

            WeaponAnimationDefinition definition = result.Lease.Definition;
            WeaponPresentationBinding binding = CreatePresentation(definition, snapshot.Generation, out WeaponPresentationInstance instance);
            if (binding == null)
            {
                bindingLifecycle.Reject(ticket, result.Lease);
                graph.ApplyWeaponFallback(snapshot, "PresentationBinding");
                RecordResourceDiagnostic(snapshot, result.DefinitionId, "PresentationBinding");
                return;
            }

            bool isDegraded = !binding.HasLeftHandGrip;
            if (!bindingLifecycle.Accept(
                    ticket,
                    result.Lease,
                    binding,
                    () => DestroyPresentation(instance),
                    isDegraded))
            {
                return;
            }

            currentDefinition = definition;
            currentPresentation = instance;
            if (firstPersonWeaponView != null && !firstPersonWeaponView.Equip(definition.PresentationPrefab))
            {
                graph.Context.RecordDebugEvent(
                    nameof(CharacterWeaponAnimationBridge),
                    "FirstPersonWeaponViewUnavailable",
                    snapshot.Generation);
            }
            graph.ApplyWeaponEquipment(snapshot, binding, true);
        }

        private WeaponPresentationBinding CreatePresentation(
            WeaponAnimationDefinition definition,
            uint generation,
            out WeaponPresentationInstance createdInstance)
        {
            createdInstance = null;
            if (definition.PresentationPrefab == null)
            {
                graph.Context.RecordDebugEvent(nameof(CharacterWeaponAnimationBridge), "PresentationPrefabMissing", generation);
                return null;
            }

            Transform rightHand = CharacterBoneResolver.Resolve(
                animator,
                HumanBodyBones.RightHand,
                "Right_Hand");
            if (rightHand == null)
            {
                graph.Context.RecordDebugEvent(nameof(CharacterWeaponAnimationBridge), "RightHandMissing", generation);
                return null;
            }

            GameObject gameObject = UnityEngine.Object.Instantiate(definition.PresentationPrefab);
            gameObject.name = $"{definition.PresentationPrefab.name}[{generation}]";
            WeaponPresentationInstance instance = gameObject.GetComponent<WeaponPresentationInstance>();
            if (instance == null || !instance.AttachTo(rightHand))
            {
                graph.Context.RecordDebugEvent(nameof(CharacterWeaponAnimationBridge), "PresentationBindingInvalid", generation);
                DestroyObject(gameObject);
                return null;
            }

            if (instance.ModelActionPlayer != null)
            {
                instance.ModelActionPlayer.PresentationEnded += OnModelPresentationEnded;
            }
            WeaponPresentationBinding binding = instance.CreateBinding(generation);
            createdInstance = instance;
            if (!binding.HasLeftHandGrip)
            {
                graph.Context.RecordDebugEvent(nameof(CharacterWeaponAnimationBridge), "LeftHandGripMissing", generation);
            }
            return binding;
        }

        private static void DestroyPresentation(WeaponPresentationInstance instance)
        {
            if (instance != null)
            {
                DestroyObject(instance.gameObject);
            }
        }

        private void OnActionChanged(WeaponActionFact fact)
        {
            if (bindingLifecycle.State == WeaponBindingState.PendingPresentation
                || bindingLifecycle.State == WeaponBindingState.Fallback
                || bindingLifecycle.State == WeaponBindingState.Disposed)
            {
                return;
            }

            if (fact.Phase == WeaponActionPhase.Started)
            {
                if (graph.StartWeaponAction(fact)
                    && fact.Kind == WeaponActionKind.Reload
                    && currentDefinition?.WeaponModelReload != null
                    && currentPresentation?.ModelActionPlayer != null)
                {
                    currentPresentation.ModelActionPlayer.Play(
                        currentDefinition.WeaponModelReload.AnimationClip,
                        fact.ActionId);
                    firstPersonWeaponView?.Play(
                        currentDefinition.WeaponModelReload.AnimationClip,
                        fact.ActionId);
                }
                return;
            }

            graph.EndWeaponAction(fact);
            currentPresentation?.ModelActionPlayer?.Stop(fact.ActionId);
            firstPersonWeaponView?.Stop(fact.ActionId);
        }

        private void OnFireCommitted(WeaponActionFact fact)
        {
            if (bindingLifecycle.State == WeaponBindingState.PendingPresentation
                || bindingLifecycle.State == WeaponBindingState.Fallback
                || bindingLifecycle.State == WeaponBindingState.Disposed)
            {
                return;
            }

            if (!graph.CommitFireReaction(fact))
            {
                return;
            }

            if (currentDefinition?.WeaponModelFire != null && currentPresentation?.ModelActionPlayer != null)
            {
                currentPresentation.ModelActionPlayer.Play(currentDefinition.WeaponModelFire.AnimationClip, fact.ActionId);
                firstPersonWeaponView?.Play(currentDefinition.WeaponModelFire.AnimationClip, fact.ActionId);
            }
        }

        private void OnModelPresentationEnded(ulong actionId, WeaponPresentationEndReason reason)
        {
            graph.Context.RecordDebugEvent(nameof(CharacterWeaponAnimationBridge), $"WeaponModelPresentationEnded:{actionId}:{reason}");
        }

        private void UnsubscribePresentation(WeaponPresentationInstance instance)
        {
            if (instance?.ModelActionPlayer != null)
            {
                instance.ModelActionPlayer.PresentationEnded -= OnModelPresentationEnded;
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        private void RecordResourceDiagnostic(
            WeaponEquipmentSnapshot snapshot,
            string definitionId,
            string missingField)
        {
            graph.Context.RecordDebugEvent(
                nameof(CharacterWeaponAnimationBridge),
                $"WeaponPresentationResourceFailure:weaponId={snapshot.EquippedWeaponId};definitionId={definitionId};generation={snapshot.Generation};missingField={missingField}",
                snapshot.Generation);
        }
    }
}
