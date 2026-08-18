
using System;
using System.Collections.Generic;
using CGame.InventoryEquipment;
using CGame.Animation.Rig;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    public sealed class EquipmentManagerComponent : ActorComponent, IEquipmentActionTarget
    {
        private PlayerController controller;
        private PawnBindingReceipt actionBinding;
        private PawnBindingReceipt quickBarBinding;
        private PendingEquipmentRequest pendingRequest;
        private float fireCooldownRemaining;

        private readonly Dictionary<ItemInstanceHandle, WeaponInstance> preparedWeapons = new Dictionary<ItemInstanceHandle, WeaponInstance>();
private PendingArming pendingArming;

        private Transform weaponMount;

        public EquipmentInstance CurrentEquipment { get; private set; }


        public int PreparedWeaponCount => preparedWeapons.Count;
public WeaponInstance CurrentWeapon => CurrentEquipment as WeaponInstance;

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
            ReleaseBindings();
            pendingRequest = null;
            pendingArming?.Weapon.Dispose();
            pendingArming = null;
            foreach (WeaponInstance weapon in preparedWeapons.Values) weapon.Dispose();
            preparedWeapons.Clear();
            CurrentEquipment?.Dispose();
            CurrentEquipment = null;
        }

        public bool Fire() => CurrentWeapon?.Fire() == true;

        public void UpdateFireInput(bool fireHeld, float deltaTime)
        {
            if (!fireHeld)
            {
                fireCooldownRemaining = 0f;
                return;
            }

            fireCooldownRemaining = Mathf.Max(0f, fireCooldownRemaining - Mathf.Max(0f, deltaTime));
            if (fireCooldownRemaining > 0f || CurrentWeapon == null)
            {
                return;
            }

            if (Fire())
            {
                fireCooldownRemaining = Mathf.Max(0.001f, CurrentWeapon.Definition.FireInterval);
            }
        }

        public int Reload() => CurrentWeapon?.Reload() ?? 0;

        public bool Melee() => CurrentWeapon?.Melee() == true;

        private void Tick(float deltaTime)
        {
            if (pendingRequest == null)
            {
                TryCompleteArming();
                return;
            }

            if (pendingRequest.RemainingTicks-- > 0)
            {
                return;
            }

            PendingEquipmentRequest request = pendingRequest;
            pendingRequest = null;
            fireCooldownRemaining = 0f;
            CompleteRequest(request);
        }

private void TryCompleteArming()
        {
            if (pendingArming == null) return;
            WeaponInstance weapon = pendingArming.Weapon;
            if (controller == null || pendingArming.Generation != controller.QuickBar.RequestGeneration || pendingArming.Handle != controller.QuickBar.RequestedHandle)
            {
                weapon.Dispose();
                pendingArming = null;
                return;
            }
            if (!weapon.HasAnimationBinding)
            {
                if (!TryPrepareAnimationBinding(weapon)) return;
            }
            if (!controller.QuickBar.ConfirmEquipped(pendingArming.Handle, pendingArming.Generation))
            {
                weapon.Dispose();
                pendingArming = null;
                return;
            }
            if (ReferenceEquals(CurrentEquipment, weapon))
            {
                LinkArmedProfile(weapon);
                pendingArming = null;
                return;
            }
            weapon.Arm(CurrentEquipment?.AbilityReceipts);
            EquipmentInstance oldEquipment = CurrentEquipment;
            CurrentEquipment = weapon;
            oldEquipment?.Dispose();
            LinkArmedProfile(weapon);
            pendingArming = null;
        }

private void OnEquipmentRequested(ItemInstanceHandle handle, long generation)
        {
            LastFailure = string.Empty;
            if (preparedWeapons.TryGetValue(handle, out WeaponInstance prepared))
            {
                BeginArming(prepared, handle, generation);
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
                    new EquipmentCreateContext(
                        lease,
                        controller.PlayerState.AbilitySystem,
                        replacedAbilityReceipts: CurrentEquipment?.AbilityReceipts));
                if (candidate == null)
                {
                    throw new InvalidOperationException("EquipmentDefinition returned null.");
                }

                if (candidate is WeaponInstance weaponCandidate)
                {
                    WeaponDefinition weaponDefinition = request.Definition as WeaponDefinition;
                    if (weaponDefinition?.Prefab == null) throw new InvalidOperationException("Weapon Definition requires a Prefab.");
                    GameObject root = UnityEngine.Object.Instantiate(weaponDefinition.Prefab, weaponMount);
                    ApplyPresentationTransform(root.transform, weaponDefinition);
                    weaponCandidate.PreparePresentation(root);
                }

                if (!controller.QuickBar.ConfirmEquipped(request.Handle, request.Generation))
                {
                    candidate.Dispose();
                    return;
                }

                if (candidate is WeaponInstance weapon)
                {
                    pendingArming = new PendingArming(weapon, request.Handle, request.Generation);
                    candidate = null;
                    return;
                }

                EquipmentInstance oldEquipment = CurrentEquipment;
                CurrentEquipment = candidate;
                candidate = null;
                oldEquipment?.Dispose();
                UnlinkArmedProfile();
                LastFailure = string.Empty;
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

        private void Reject(ItemInstanceHandle handle, long generation, string failure)
        {
            LastFailure = failure;
            controller?.QuickBar.RejectRequest(handle, generation);
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
            public PendingArming(WeaponInstance weapon, ItemInstanceHandle handle, long generation)
            {
                Weapon = weapon;
                Handle = handle;
                Generation = generation;
            }

            public WeaponInstance Weapon { get; }
            public ItemInstanceHandle Handle { get; }
            public long Generation { get; }
        }


private void PrepareQuickBarWeapons()
        {
            var created = new List<WeaponInstance>();
            try
            {
                foreach (ItemInstanceHandle handle in controller.QuickBar.Slots)
                {
                    if (!controller.Inventory.TryGet(handle, out ItemInstance item) || !(item.Definition is WeaponItemDefinition weaponItem)) continue;
                    WeaponDefinition definition = weaponItem.WeaponDefinition;
                    if (definition == null || definition.Prefab == null) throw new InvalidOperationException("QuickBar weapon requires a configured WeaponDefinition and Prefab.");
                    InventoryLease lease = controller.Inventory.AcquireLease(handle);
                    var weapon = (WeaponInstance)definition.CreateInstance(new EquipmentCreateContext(lease, controller.PlayerState.AbilitySystem));
                    GameObject root = UnityEngine.Object.Instantiate(definition.Prefab, weaponMount);
                    ApplyPresentationTransform(root.transform, definition);
                    weapon.PreparePresentation(root);
                    preparedWeapons.Add(handle, weapon);
                    created.Add(weapon);
                }
            }
            catch
            {
                for (int index = created.Count - 1; index >= 0; index--) created[index].Dispose();
                preparedWeapons.Clear();
                throw;
            }
        }

        private static void ApplyPresentationTransform(Transform root, WeaponDefinition definition)
        {
            root.localPosition = definition.PresentationLocalPosition;
            root.localRotation = definition.PresentationLocalRotation;
            root.localScale = definition.PresentationLocalScale;
        }

        private void BeginArming(WeaponInstance weapon, ItemInstanceHandle handle, long generation)
        {
            pendingArming = new PendingArming(weapon, handle, generation);
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

            animation.AnimInstance.BoneController.LinkProfile(weapon.Definition.ArmedProfile);
        }

        private void UnlinkArmedProfile()
        {
            PawnAnimationComponent animation = Owner?.GetComponent<PawnAnimationComponent>();
            animation?.AnimInstance?.BoneController.UnlinkProfile();
        }
}
}
