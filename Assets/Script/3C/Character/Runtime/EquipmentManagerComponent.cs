using System;
using CGame.InventoryEquipment;

namespace CGame
{
    public sealed class EquipmentManagerComponent : ActorComponent, IEquipmentActionTarget
    {
        private PlayerController controller;
        private PawnBindingReceipt actionBinding;
        private PendingEquipmentRequest pendingRequest;

        public EquipmentInstance CurrentEquipment { get; private set; }

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
            controller.QuickBar.EquipmentRequested += OnEquipmentRequested;
            actionBinding = controller.BindEquipmentActionTarget(this);
        }

        protected override void OnEndPlay() => ReleaseBindings();

        protected override void OnShutdown()
        {
            ReleaseBindings();
            pendingRequest = null;
            CurrentEquipment?.Dispose();
            CurrentEquipment = null;
        }

        public bool Fire() => CurrentWeapon?.Fire() == true;

        public int Reload() => CurrentWeapon?.Reload() ?? 0;

        public bool Melee() => CurrentWeapon?.Melee() == true;

        private void Tick(float deltaTime)
        {
            if (pendingRequest == null)
            {
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
    }
}
