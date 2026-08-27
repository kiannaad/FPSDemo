using System;
using System.Collections.Generic;

namespace CGame
{
    public interface IQuickBarComponent : IDisposable
    {
        IInventoryComponent Inventory { get; }

        bool IsDisposed { get; }

        Pawn BoundPawn { get; }

        IReadOnlyList<ItemInstanceHandle> Slots { get; }

        int SelectedSlot { get; }

        ItemInstanceHandle RequestedHandle { get; }

        ItemInstanceHandle EquippedHandle { get; }

        long RequestGeneration { get; }

        event Action<ItemInstanceHandle, long> EquipmentRequested;

        void Initialize(IReadOnlyList<ItemInstanceHandle> handles, int selectedSlot);

        bool SelectSlot(int slotIndex);

        bool ConfirmEquipped(ItemInstanceHandle handle, long generation);

        bool RejectRequest(ItemInstanceHandle handle, long generation);

        PawnBindingReceipt BindPawn(Pawn pawn);
    }
}
