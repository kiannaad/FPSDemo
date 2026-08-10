using System;
using System.Collections.Generic;

namespace CGame
{
    public interface IInventoryComponent : IDisposable
    {
        bool IsDisposed { get; }

        bool IsInitialized { get; }

        IReadOnlyCollection<ItemInstance> Items { get; }

        IReadOnlyList<ItemInstanceHandle> Initialize(InitialInventorySet initialSet);

        ItemInstanceHandle Add(ItemDefinition definition);

        bool TryGet(ItemInstanceHandle handle, out ItemInstance item);

        InventoryLease AcquireLease(ItemInstanceHandle handle);

        bool Remove(ItemInstanceHandle handle);
    }
}
