using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class QuickBarComponent : IQuickBarComponent
    {
        private readonly List<ItemInstanceHandle> slots = new List<ItemInstanceHandle>();

        public QuickBarComponent(IInventoryComponent inventory)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public IInventoryComponent Inventory { get; }

        public bool IsDisposed { get; private set; }

        public Pawn BoundPawn { get; private set; }

        public IReadOnlyList<ItemInstanceHandle> Slots => slots;

        public int SelectedSlot { get; private set; } = -1;

        public ItemInstanceHandle RequestedHandle { get; private set; }

        public ItemInstanceHandle EquippedHandle { get; private set; }

        public long RequestGeneration { get; private set; }

        public event Action<ItemInstanceHandle, long> EquipmentRequested;

        public void Initialize(IReadOnlyList<ItemInstanceHandle> handles, int selectedSlot)
        {
            ThrowIfDisposed();
            if (slots.Count != 0 || SelectedSlot >= 0)
            {
                throw new InvalidOperationException("QuickBar can only be initialized once per Controller session.");
            }

            if (handles != null)
            {
                for (int index = 0; index < handles.Count; index++)
                {
                    slots.Add(handles[index]);
                }
            }

            if (slots.Count == 0)
            {
                return;
            }

            if (selectedSlot < 0 || selectedSlot >= slots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(selectedSlot));
            }

            SelectSlot(selectedSlot);
        }

        public bool SelectSlot(int slotIndex)
        {
            ThrowIfDisposed();
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            SelectedSlot = slotIndex;
            RequestedHandle = slots[slotIndex];
            long generation = ++RequestGeneration;
            EquipmentRequested?.Invoke(RequestedHandle, generation);
            return true;
        }

        public bool ConfirmEquipped(ItemInstanceHandle handle, long generation)
        {
            if (generation != RequestGeneration || handle != RequestedHandle)
            {
                return false;
            }

            EquippedHandle = handle;
            return true;
        }

        public bool RejectRequest(ItemInstanceHandle handle, long generation)
        {
            return generation == RequestGeneration && handle == RequestedHandle;
        }

        public PawnBindingReceipt BindPawn(Pawn pawn)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(QuickBarComponent));
            }

            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            if (BoundPawn != null)
            {
                throw new InvalidOperationException("QuickBar is already bound to a Pawn.");
            }

            BoundPawn = pawn;
            if (RequestedHandle.IsValid)
            {
                EquipmentRequested?.Invoke(RequestedHandle, RequestGeneration);
            }

            return new PawnBindingReceipt(() =>
            {
                if (ReferenceEquals(BoundPawn, pawn))
                {
                    BoundPawn = null;
                    EquippedHandle = default;
                }
            });
        }

        public void Dispose()
        {
            BoundPawn = null;
            EquipmentRequested = null;
            IsDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(QuickBarComponent));
            }
        }
    }
}
