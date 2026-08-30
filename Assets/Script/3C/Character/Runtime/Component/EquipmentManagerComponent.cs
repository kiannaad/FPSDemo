
using System;
using System.Collections.Generic;
using CGame.InventoryEquipment;
using CGame.Animation.Rig;
using CGame.Animation;
using CGame.Ability;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame
{
    public sealed class EquipmentManagerComponent : ActorComponent, IEquipmentActionTarget
    {
        private const float MaxPhaseWaitSeconds = 2f;
        private const float RunningSpeedThreshold = 3.2f;
        private PlayerController controller;
        private PawnBindingReceipt actionBinding;
        private PawnBindingReceipt quickBarBinding;
        private PendingEquipmentRequest pendingRequest;
        private GameplayTagGrantHandle switchTagGrant;
        private long switchGeneration;
        private EquipmentSwitchPhase lastSwitchPhase = EquipmentSwitchPhase.Idle;

        private readonly Dictionary<ItemInstanceHandle, WeaponInstance> preparedWeapons = new Dictionary<ItemInstanceHandle, WeaponInstance>();
        private readonly Dictionary<ItemInstanceHandle, string> preheatFailures = new Dictionary<ItemInstanceHandle, string>();
        private PendingArming pendingArming;

        private Transform weaponMount;

        public EquipmentInstance CurrentEquipment { get; private set; }


        public int PreparedWeaponCount => preparedWeapons.Count;

        public int PreheatFailureCount => preheatFailures.Count;

        public WeaponInstance CurrentWeapon => CurrentEquipment as WeaponInstance;

        public EquipmentSwitchPhase SwitchPhase => pendingArming?.Phase ?? lastSwitchPhase;

        public string LastSwitchResult { get; private set; } = string.Empty;

        public bool IsSwitchInProgress => pendingRequest != null || pendingArming != null;

        public bool IsUnarmed => CurrentEquipment == null;

        public string LastFailure { get; private set; } = string.Empty;

        protected override void OnInitialize()
        {
            AddTickTask("Pawn.Equipment", TickGroup.TG_Gameplay, Tick);
        }

protected override void OnBeginPlay()
        {
            if (!(Owner is Pawn pawn) || !(pawn.Controller is PlayerController playerController))
            {
                throw new InvalidOperationException("EquipmentManagerComponent requires a possessed PlayerController.");
            }

            controller = playerController;
            weaponMount = ResolveWeaponMount(pawn);
            PrepareQuickBarWeapons();
            controller.QuickBar.EquipmentRequested += OnEquipmentRequested;
            quickBarBinding = controller.QuickBar.BindPawn(pawn);
            actionBinding = controller.BindEquipmentActionTarget(this);
        }

        private static Transform ResolveWeaponMount(Pawn pawn)
        {
            KRigComponent rig = pawn.Root.GetComponentInChildren<KRigComponent>(true);
            if (rig == null || !rig.IsInitialized) throw new InvalidOperationException("Pawn requires an initialized KRigComponent.");
            Transform[] matches = rig.GetComponentsInChildren<Transform>(true);
            Transform mount = null;
            foreach (Transform candidate in matches)
            {
                if (candidate.name != "IK WeaponBone") continue;
                if (mount != null) throw new InvalidOperationException("Pawn KRig has multiple IK WeaponBone transforms.");
                mount = candidate;
            }
            return mount ?? throw new InvalidOperationException("Pawn KRig has no IK WeaponBone transform.");
        }

        protected override void OnEndPlay()
        {
            UnlinkArmedProfile();
            ReleaseBindings();
        }

protected override void OnShutdown()
        {
            UnlinkArmedProfile();
            EndSwitchTransaction(switchGeneration);
            ReleaseBindings();
            pendingRequest = null;
            if (pendingArming != null)
            {
                pendingArming.Weapon.Dispose();
                pendingArming = null;
                lastSwitchPhase = EquipmentSwitchPhase.Cancelled;
                LastSwitchResult = "Cancelled";
            }

            foreach (WeaponInstance weapon in preparedWeapons.Values)
            {
                weapon.Dispose();
            }

            preparedWeapons.Clear();
            preheatFailures.Clear();
            CurrentEquipment?.Dispose();
            CurrentEquipment = null;
        }

public bool CanAcceptDirectSlotSelection(int slotIndex)
        {
            if (IsSwitchInProgress || IsReloading() || IsOwnerRunning() || controller == null
                || slotIndex < 0 || slotIndex >= controller.QuickBar.Slots.Count)
            {
                return false;
            }

            ItemInstanceHandle handle = controller.QuickBar.Slots[slotIndex];
            return CurrentWeapon?.ItemHandle == handle || preparedWeapons.ContainsKey(handle);
        }

        private bool IsOwnerRunning()
        {
            if (!(Owner is Pawn pawn))
            {
                return false;
            }

            if (pawn.HasRunningIntent)
            {
                return true;
            }

            if (!pawn.TryGetComponent(out PawnMovementComponent movement) || movement.Motor == null)
            {
                return false;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                movement.Motor.Velocity,
                Vector3.up);
            return horizontalVelocity.sqrMagnitude
                > RunningSpeedThreshold * RunningSpeedThreshold;
        }


        private void Tick(float deltaTime)
        {
            CurrentWeapon?.AdvanceHeat(deltaTime);
            if (pendingRequest == null)
            {
                TryCompleteArming(deltaTime);
                return;
            }

            if (pendingRequest.RemainingTicks-- > 0)
            {
                return;
            }

            PendingEquipmentRequest request = pendingRequest;
            pendingRequest = null;
            CompleteRequest(request);
        }

        private void TryCompleteArming(float deltaTime)
        {
            if (pendingArming == null)
            {
                return;
            }

            PendingArming pending = pendingArming;
            if (controller == null
                || pending.Generation != controller.QuickBar.RequestGeneration
                || pending.Handle != controller.QuickBar.RequestedHandle)
            {
                RollbackPendingArming("Weapon switch was cancelled by a newer QuickBar request.", true);
                return;
            }

            try
            {
                switch (pending.Phase)
                {
                    case EquipmentSwitchPhase.Prechecking:
                        EnterSwitchPhase(
                            pending,
                            pending.OldWeapon == null
                                ? EquipmentSwitchPhase.Handoff
                                : EquipmentSwitchPhase.Unequipping);
                        return;

                    case EquipmentSwitchPhase.Unequipping:
                        if (!IsArmedProfileActive(pending.OldWeapon))
                        {
                            WaitForSwitchPhaseOrRollback("The outgoing weapon profile did not remain active.", deltaTime);
                            return;
                        }

                        if (!pending.UnequipMotionStarted)
                        {
                            if (!TryPlayWeaponMotion(pending.OldWeapon, pending.OldWeapon.Definition.UnequipIkMotion))
                            {
                                RollbackPendingArming("The outgoing weapon Unequip motion could not start.", false);
                                return;
                            }

                            pending.UnequipMotionStarted = true;
                            return;
                        }

                        if (!pending.PresentationHandedOff)
                        {
                            if (!HasWeaponMotionReachedEnd(
                                    pending.OldWeapon,
                                    pending.OldWeapon.Definition.UnequipIkMotion))
                            {
                                WaitForSwitchPhaseOrRollback(
                                    "The outgoing weapon Unequip motion did not reach its presentation handoff point.",
                                    deltaTime);
                                return;
                            }

                            if (!pending.Weapon.HasAnimationBinding && !TryPrepareAnimationBinding(pending.Weapon))
                            {
                                RollbackPendingArming("The target weapon Overlay could not be prepared.", false);
                                return;
                            }

                            pending.OldWeapon.HidePresentation();
                            pending.PresentationHandedOff = true;
                            LinkArmedProfile(pending.Weapon);
                            EnterSwitchPhase(pending, EquipmentSwitchPhase.Equipping);
                            return;
                        }

                        return;

                    case EquipmentSwitchPhase.Handoff:
                        if (!pending.Weapon.HasAnimationBinding && !TryPrepareAnimationBinding(pending.Weapon))
                        {
                            RollbackPendingArming("The target weapon Overlay could not be prepared.", false);
                            return;
                        }

                        if (!pending.PresentationHandedOff)
                        {
                            pending.OldWeapon?.HidePresentation();
                            pending.PresentationHandedOff = true;
                        }

                        LinkArmedProfile(pending.Weapon);
                        EnterSwitchPhase(pending, EquipmentSwitchPhase.Equipping);
                        return;

                    case EquipmentSwitchPhase.Equipping:
                        if (!IsArmedProfileActive(pending.Weapon))
                        {
                            WaitForSwitchPhaseOrRollback("The target weapon profile did not become active.", deltaTime);
                            return;
                        }

                        if (!pending.Weapon.IsPresentationVisible)
                        {
                            pending.Weapon.ShowPresentation();
                        }

                        if (!pending.EquipMotionStarted)
                        {
                            Debug.Log(
                                $"[武器装备] Profile 链接成功：武器={pending.Weapon.Definition.name}，"
                                + $"Profile={pending.Weapon.Definition.ArmedProfile.name}。");
                            if (!TryPlayWeaponMotion(pending.Weapon, pending.Weapon.Definition.EquipIkMotion))
                            {
                                RollbackPendingArming("The target weapon Equip motion could not start.", false);
                                return;
                            }

                            pending.EquipMotionStarted = true;
                            pending.RequiredAnimationEvaluationCount = GetCompletedAnimationEvaluationCount();
                            return;
                        }

                        if (!IsWeaponMotionComplete(pending.Weapon, pending.Weapon.Definition.EquipIkMotion))
                        {
                            WaitForSwitchPhaseOrRollback("The target weapon Equip motion did not return to identity.", deltaTime);
                            return;
                        }

                        pending.Weapon.Arm(pending.OldWeapon?.AbilityReceipts);
                        pending.OldWeapon?.Disarm();
                        EnterSwitchPhase(pending, EquipmentSwitchPhase.Completed);
                        return;

                    case EquipmentSwitchPhase.Completed:
                        if (!controller.QuickBar.ConfirmEquipped(pending.Handle, pending.Generation))
                        {
                            RollbackPendingArming("QuickBar no longer accepts the switch transaction.", true);
                            return;
                        }

                        CurrentEquipment = pending.Weapon;
                        LastFailure = string.Empty;
                        LastSwitchResult = "Completed";
                        lastSwitchPhase = EquipmentSwitchPhase.Completed;
                        Debug.Log(
                            $"[武器装备] 装备成功：武器={pending.Weapon.Definition.name}，"
                            + $"Profile={pending.Weapon.Definition.ArmedProfile.name}。");
                        EndSwitchTransaction(pending.Generation);
                        pendingArming = null;
                        return;
                }
            }
            catch (Exception exception)
            {
                RollbackPendingArming(exception.Message, false);
            }
        }

private void OnEquipmentRequested(ItemInstanceHandle handle, long generation)
        {
            LastFailure = string.Empty;
            if (pendingRequest != null || pendingArming != null)
            {
                Reject(handle, generation, "Weapon switch is already in progress.");
                return;
            }

            if (IsReloading())
            {
                Reject(handle, generation, "Weapon switching is blocked while reloading.");
                return;
            }

            if (CurrentWeapon != null && CurrentWeapon.ItemHandle == handle)
            {
                controller?.QuickBar.ConfirmEquipped(handle, generation);
                lastSwitchPhase = EquipmentSwitchPhase.Completed;
                LastSwitchResult = "NoOp";
                Debug.Log($"[武器装备] 装备成功：目标武器已处于装备状态，武器={CurrentWeapon.Definition.name}。");
                return;
            }

            BeginSwitchTransaction(generation);
            lastSwitchPhase = EquipmentSwitchPhase.Prechecking;
            LastSwitchResult = string.Empty;
            StopContinuousWeaponActions();
            if (preparedWeapons.TryGetValue(handle, out WeaponInstance prepared))
            {
                BeginArming(prepared, handle, generation, true);
                return;
            }
            if (preheatFailures.TryGetValue(handle, out string preheatFailure))
            {
                Reject(handle, generation, preheatFailure);
                return;
            }
            if (controller == null || !controller.Inventory.TryGet(handle, out ItemInstance item)
                || !(item.Definition is EquippableItemDefinition equippable)
                || equippable.EquipmentDefinition == null)
            {
                Reject(handle, generation, "Requested item has no equipment definition.");
                return;
            }

            pendingRequest = new PendingEquipmentRequest(handle, generation, equippable.EquipmentDefinition, equippable.EquipmentDefinition.LoadTicks);
        }

private void CompleteRequest(PendingEquipmentRequest request)
        {
            if (controller == null || request.Generation != controller.QuickBar.RequestGeneration
                || request.Handle != controller.QuickBar.RequestedHandle)
            {
                EndSwitchTransaction(request.Generation);
                return;
            }

            if (request.Definition.SimulateLoadFailure)
            {
                Reject(request.Handle, request.Generation, "Equipment definition load failed.");
                return;
            }

            InventoryLease lease = controller.Inventory.AcquireLease(request.Handle);
            if (lease == null)
            {
                Reject(request.Handle, request.Generation, "Requested inventory item no longer exists.");
                return;
            }

            EquipmentInstance candidate = null;
            try
            {
                candidate = request.Definition.CreateInstance(
                    new EquipmentCreateContext(lease, controller.PlayerState.AbilitySystem));
                if (!(candidate is WeaponInstance weapon))
                {
                    throw new InvalidOperationException("Weapon switch transactions only support WeaponInstance targets.");
                }

                WeaponDefinition weaponDefinition = request.Definition as WeaponDefinition;
                if (weaponDefinition?.Prefab == null)
                {
                    throw new InvalidOperationException("Weapon Definition requires a Prefab.");
                }

                GameObject root = UnityEngine.Object.Instantiate(weaponDefinition.Prefab, weaponMount);
                ApplyPresentationTransform(root.transform, weaponDefinition);
                weapon.PreparePresentation(root);
                candidate = null;
                BeginArming(weapon, request.Handle, request.Generation, false);
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                if (candidate == null)
                {
                    lease.Dispose();
                }

                Reject(request.Handle, request.Generation, exception.Message);
            }
        }

        private static void EnterSwitchPhase(PendingArming pending, EquipmentSwitchPhase phase)
        {
            pending.Phase = phase;
            pending.RemainingPhaseSeconds = MaxPhaseWaitSeconds;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string oldWeaponName = pending.OldWeapon?.Definition != null
                ? pending.OldWeapon.Definition.name
                : "<none>";
            string targetWeaponName = pending.Weapon?.Definition != null
                ? pending.Weapon.Definition.name
                : "<none>";
            Debug.Log(
                $"[WeaponSwitchFacingProbe] frame={Time.frameCount}; phase={phase}; "
                + $"old={oldWeaponName}; target={targetWeaponName}; "
                + $"presentationHandedOff={pending.PresentationHandedOff}; "
                + $"requiredEvaluation={pending.RequiredAnimationEvaluationCount}");
#endif
        }

private void WaitForSwitchPhaseOrRollback(string failure, float deltaTime)
        {
            pendingArming.RemainingPhaseSeconds -= Mathf.Max(0f, deltaTime);
            if (pendingArming.RemainingPhaseSeconds > 0f)
            {
                return;
            }

            RollbackPendingArming(failure, false);
        }

        private void RollbackPendingArming(string failure, bool cancelled)
        {
            PendingArming pending = pendingArming;
            if (pending == null)
            {
                return;
            }

            if (!cancelled
                && pending.Phase == EquipmentSwitchPhase.Equipping
                && !IsArmedProfileActive(pending.Weapon))
            {
                string profileName = pending.Weapon.Definition.ArmedProfile != null
                    ? pending.Weapon.Definition.ArmedProfile.name
                    : "<未配置>";
                Debug.LogError(
                    $"[武器装备] Profile 链接失败：武器={pending.Weapon.Definition.name}，"
                    + $"Profile={profileName}，原因={failure}");
            }

            pending.Phase = EquipmentSwitchPhase.RollingBack;
            try
            {
                pending.Weapon.HidePresentation();
                pending.Weapon.Disarm();
                WeaponInstance oldWeapon = pending.OldWeapon;
                if (oldWeapon != null && !oldWeapon.IsDisposed)
                {
                    oldWeapon.Disarm();
                    if (!TryPrepareAnimationBinding(oldWeapon))
                    {
                        throw new InvalidOperationException("The previous weapon Overlay could not be restored.");
                    }

                    oldWeapon.Arm();
                    oldWeapon.ShowPresentation();
                    LinkArmedProfile(oldWeapon);
                    TryPlayWeaponMotion(oldWeapon, oldWeapon.Definition.EquipIkMotion);
                    CurrentEquipment = oldWeapon;
                }
            }
            catch (Exception exception)
            {
                failure = failure + " Rollback failed: " + exception.Message;
            }
            finally
            {
                if (!pending.IsPrecreated)
                {
                    pending.Weapon.Dispose();
                }

                LastFailure = failure;
                LastSwitchResult = cancelled ? "Cancelled" : "Failed";
                lastSwitchPhase = cancelled
                    ? EquipmentSwitchPhase.Cancelled
                    : EquipmentSwitchPhase.Failed;
                if (cancelled)
                {
                    Debug.LogWarning(
                        $"[武器装备] 装备已取消：武器={pending.Weapon.Definition.name}，"
                        + $"阶段={pending.Phase}，原因={failure}");
                }
                else
                {
                    Debug.LogError(
                        $"[武器装备] 装备失败：武器={pending.Weapon.Definition.name}，"
                        + $"阶段={pending.Phase}，原因={failure}");
                }
                pendingArming = null;
                controller?.QuickBar.RejectRequest(pending.Handle, pending.Generation);
                EndSwitchTransaction(pending.Generation);
            }
        }

        private bool IsArmedProfileActive(WeaponInstance weapon)
        {
            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            return weapon != null
                && animation?.AnimInstance?.BoneController.ActiveProfile == weapon.Definition.ArmedProfile;
        }

        private int GetCompletedAnimationEvaluationCount()
        {
            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            return animation?.AnimInstance?.CompletedAnimationEvaluationCount ?? -1;
        }

        private bool TryPlayWeaponMotion(WeaponInstance weapon, IkMotionLayerSettings motion)
        {
            if (motion == null)
            {
                return true;
            }

            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            return weapon != null
                && animation?.AnimInstance != null
                && animation.AnimInstance.TryPlayWeaponIkMotion(motion);
        }

        private bool IsWeaponMotionComplete(WeaponInstance weapon, IkMotionLayerSettings motion)
        {
            if (motion == null)
            {
                return true;
            }

            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            return weapon != null
                && animation?.AnimInstance != null
                && animation.AnimInstance.IsWeaponIkMotionComplete(motion);
        }

        private bool HasWeaponMotionReachedEnd(WeaponInstance weapon, IkMotionLayerSettings motion)
        {
            if (motion == null)
            {
                return true;
            }

            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            return weapon != null
                && animation?.AnimInstance != null
                && animation.AnimInstance.HasWeaponIkMotionReachedEnd(motion);
        }

        private void StopContinuousWeaponActions()
        {
            AbilitySystemComponent abilitySystem = controller?.PlayerState?.AbilitySystem;
            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.AbilityInputTagReleased(CreateTag("InputTag.Weapon.Aim"));
            abilitySystem.ProcessAbilityInput();
        }


        private void Reject(ItemInstanceHandle handle, long generation, string failure)
        {
            LastFailure = failure;
            Debug.LogError($"[武器装备] 装备失败：请求被拒绝，物品={handle}，原因={failure}");
            controller?.QuickBar.RejectRequest(handle, generation);
            EndSwitchTransaction(generation);
        }

        private bool IsReloading()
        {
            return controller?.PlayerState?.AbilitySystem != null
                && controller.PlayerState.AbilitySystem.HasOwnedTag(CreateTag("State.Weapon.Reloading"));
        }

        private void BeginSwitchTransaction(long generation)
        {
            AbilitySystemComponent abilitySystem = controller?.PlayerState?.AbilitySystem;
            if (abilitySystem == null)
            {
                return;
            }

            if (!switchTagGrant.IsValid)
            {
                switchTagGrant = abilitySystem.AddOwnedTag(CreateTag("State.Weapon.Switch"));
            }

            switchGeneration = generation;
        }

        private void EndSwitchTransaction(long generation)
        {
            if (!switchTagGrant.IsValid || generation != switchGeneration)
            {
                return;
            }

            controller?.PlayerState?.AbilitySystem.RemoveOwnedTag(switchTagGrant);
            switchTagGrant = default;
            switchGeneration = 0;
        }

        private static GameplayTag CreateTag(string value)
        {
            if (!GameplayTag.TryCreateSerialized(value, out GameplayTag tag))
            {
                throw new InvalidOperationException($"Invalid gameplay tag '{value}'.");
            }

            return tag;
        }

        private void ReleaseBindings()
        {
            if (controller != null)
            {
                controller.QuickBar.EquipmentRequested -= OnEquipmentRequested;
            }

            actionBinding?.Dispose();
            actionBinding = null;
            quickBarBinding?.Dispose();
            quickBarBinding = null;
            controller = null;
        }

        private sealed class PendingEquipmentRequest
        {
            public PendingEquipmentRequest(
                ItemInstanceHandle handle,
                long generation,
                EquipmentDefinition definition,
                int remainingTicks)
            {
                Handle = handle;
                Generation = generation;
                Definition = definition;
                RemainingTicks = remainingTicks;
            }

            public ItemInstanceHandle Handle { get; }
            public long Generation { get; }
            public EquipmentDefinition Definition { get; }
            public int RemainingTicks { get; set; }
        }

        private sealed class PendingArming
        {
            public PendingArming(
                WeaponInstance weapon,
                WeaponInstance oldWeapon,
                ItemInstanceHandle handle,
                long generation,
                bool isPrecreated)
            {
                Weapon = weapon;
                OldWeapon = oldWeapon;
                Handle = handle;
                Generation = generation;
                IsPrecreated = isPrecreated;
            }

            public WeaponInstance Weapon { get; }
            public WeaponInstance OldWeapon { get; }
            public ItemInstanceHandle Handle { get; }
            public long Generation { get; }
            public bool IsPrecreated { get; }
            public EquipmentSwitchPhase Phase { get; set; } = EquipmentSwitchPhase.Prechecking;
            public float RemainingPhaseSeconds { get; set; } = MaxPhaseWaitSeconds;
            public bool UnequipMotionStarted { get; set; }
            public bool EquipMotionStarted { get; set; }
            public bool PresentationHandedOff { get; set; }
            public int RequiredAnimationEvaluationCount { get; set; } = -1;
        }


private void PrepareQuickBarWeapons()
        {
            foreach (ItemInstanceHandle handle in controller.QuickBar.Slots)
            {
                if (!controller.Inventory.TryGet(handle, out ItemInstance item) || !(item.Definition is WeaponItemDefinition weaponItem))
                {
                    continue;
                }

                InventoryLease lease = null;
                EquipmentInstance candidate = null;
                WeaponInstance weapon = null;
                try
                {
                    WeaponDefinition definition = weaponItem.WeaponDefinition;
                    if (definition == null || definition.Prefab == null) throw new InvalidOperationException("QuickBar weapon requires a configured WeaponDefinition and Prefab.");
                    if (definition.SimulateLoadFailure) throw new InvalidOperationException("Weapon preheat load failed.");

                    lease = controller.Inventory.AcquireLease(handle);
                    if (lease == null) throw new InvalidOperationException("QuickBar weapon inventory item no longer exists.");

                    candidate = definition.CreateInstance(new EquipmentCreateContext(lease, controller.PlayerState.AbilitySystem));
                    weapon = candidate as WeaponInstance
                        ?? throw new InvalidOperationException("WeaponDefinition did not create a WeaponInstance.");
                    candidate = null;
                    lease = null;

                    GameObject root = UnityEngine.Object.Instantiate(definition.Prefab, weaponMount);
                    ApplyPresentationTransform(root.transform, definition);
                    weapon.PreparePresentation(root);
                    preparedWeapons.Add(handle, weapon);
                }
                catch (Exception exception)
                {
                    candidate?.Dispose();
                    weapon?.Dispose();
                    lease?.Dispose();
                    preheatFailures[handle] = exception.Message;
                }
            }
        }

        private static void ApplyPresentationTransform(Transform root, WeaponDefinition definition)
        {
            root.localPosition = definition.PresentationLocalPosition;
            root.localRotation = definition.PresentationLocalRotation;
            root.localScale = definition.PresentationLocalScale;
        }

private void BeginArming(WeaponInstance weapon, ItemInstanceHandle handle, long generation, bool isPrecreated)
        {
            if (weapon == null || weapon.IsDisposed)
            {
                Reject(handle, generation, "The requested weapon is not available.");
                return;
            }

            pendingArming = new PendingArming(
                weapon,
                CurrentWeapon,
                handle,
                generation,
                isPrecreated);
        }

        private bool TryPrepareAnimationBinding(WeaponInstance weapon)
        {
            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            if (animation?.AnimInstance == null) return false;
            WeaponDefinition definition = weapon.Definition;
            definition.ArmedProfile.Validate(animation.RigComponent.Rig);
            AnimationPlaybackHandle overlayHandle = animation.AnimInstance.PrepareInitialPose(definition.OverlayPose);
            if (overlayHandle == null || overlayHandle.State == AnimationPlaybackState.Failed) return false;
            try
            {
                weapon.SetAnimationBinding(animation.AnimInstance, overlayHandle);
                return true;
            }
            catch
            {
                animation.AnimInstance.StopAbilityAnimation(overlayHandle);
                throw;
            }
        }

        private void LinkArmedProfile(WeaponInstance weapon)
        {
            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            if (animation?.AnimInstance == null)
            {
                throw new InvalidOperationException("Weapon arming requires an active CharacterAnimInstance.");
            }

            BoneProfile profile = weapon.Definition.ArmedProfile;
            try
            {
                animation.AnimInstance.BoneController.LinkProfile(profile);
            }
            catch (Exception exception)
            {
                string profileName = profile != null ? profile.name : "<未配置>";
                Debug.LogError(
                    $"[武器装备] Profile 链接失败：武器={weapon.Definition.name}，"
                    + $"Profile={profileName}，原因={exception.Message}");
                throw;
            }
        }

        private void UnlinkArmedProfile()
        {
            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            animation?.AnimInstance?.BoneController.UnlinkProfile();
        }
}
}
