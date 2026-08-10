using System;
using CGame.InventoryEquipment;

namespace CGame
{
    public sealed class EquipmentManagerComponent : PawnFeatureComponent, IEquipmentActionTarget
    {
        private PlayerController controller;
        private PawnExtensionComponent extension;
        private PendingEquipmentRequest pendingRequest;

        public EquipmentManagerComponent() : base("Equipment")
        {
        }

        public EquipmentInstance CurrentEquipment { get; private set; }

        public WeaponInstance CurrentWeapon => CurrentEquipment as WeaponInstance;

        public bool IsUnarmed => CurrentEquipment == null;

        public string LastFailure { get; private set; } = string.Empty;

        public PawnBindingReceipt Bind(
            PlayerController playerController,
            Pawn pawn,
            PawnExtensionComponent pawnExtension)
        {
            if (controller != null)
            {
                throw new InvalidOperationException("EquipmentManager is already bound.");
            }

            controller = playerController ?? throw new ArgumentNullException(nameof(playerController));
            extension = pawnExtension ?? throw new ArgumentNullException(nameof(pawnExtension));
            controller.QuickBar.EquipmentRequested += OnEquipmentRequested;
            return new PawnBindingReceipt(() =>
            {
                if (controller == null)
                {
                    return;
                }

                controller.QuickBar.EquipmentRequested -= OnEquipmentRequested;
                controller = null;
                extension = null;
                pendingRequest = null;
                CurrentEquipment?.Dispose();
                CurrentEquipment = null;
                SetReady(true);
            });
        }

        public void Tick()
        {
            if (pendingRequest == null)
            {
                return;
            }

            if (pendingRequest.RemainingTicks > 0)
            {
                pendingRequest.RemainingTicks--;
                return;
            }

            PendingEquipmentRequest request = pendingRequest;
            pendingRequest = null;
            CompleteRequest(request);
        }

        public bool Fire() => CurrentWeapon?.Fire() == true;

        public int Reload() => CurrentWeapon?.Reload() ?? 0;

        public bool Melee() => CurrentWeapon?.Melee() == true;

        public override void Shutdown()
        {
            if (controller != null)
            {
                controller.QuickBar.EquipmentRequested -= OnEquipmentRequested;
            }

            controller = null;
            extension = null;
            pendingRequest = null;
            CurrentEquipment?.Dispose();
            CurrentEquipment = null;
            base.Shutdown();
        }

        private void OnEquipmentRequested(ItemInstanceHandle handle, long generation)
        {
            LastFailure = string.Empty;
            if (controller == null || !controller.Inventory.TryGet(handle, out ItemInstance item)
                || !(item.Definition is EquippableItemDefinition equippable)
                || equippable.EquipmentDefinition == null)
            {
                Reject(handle, generation, "Requested item has no equipment definition.");
                return;
            }

            pendingRequest = new PendingEquipmentRequest(
                handle,
                generation,
                equippable.EquipmentDefinition,
                equippable.EquipmentDefinition.LoadTicks);
            SetReady(false);
            extension?.RequestInitializationCheck();
        }

        private void CompleteRequest(PendingEquipmentRequest request)
        {
            if (controller == null || request.Generation != controller.QuickBar.RequestGeneration
                || request.Handle != controller.QuickBar.RequestedHandle)
            {
                SetReady(pendingRequest == null);
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
                if (candidate == null)
                {
                    throw new InvalidOperationException("EquipmentDefinition returned null.");
                }

                if (!controller.QuickBar.ConfirmEquipped(request.Handle, request.Generation))
                {
                    candidate.Dispose();
                    return;
                }

                EquipmentInstance oldEquipment = CurrentEquipment;
                CurrentEquipment = candidate;
                candidate = null;
                oldEquipment?.Dispose();
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
                return;
            }

            SetReady(true);
            extension?.RequestInitializationCheck();
        }

        private void Reject(ItemInstanceHandle handle, long generation, string failure)
        {
            LastFailure = failure;
            controller?.QuickBar.RejectRequest(handle, generation);
            SetReady(true);
            extension?.RequestInitializationCheck();
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
    }
}
