using System;
using System.Collections.Generic;

namespace CGame
{
    public sealed class InventoryComponent : IInventoryComponent
    {
        private readonly Dictionary<ItemInstanceHandle, ItemInstance> items =
            new Dictionary<ItemInstanceHandle, ItemInstance>();
        private readonly Dictionary<ItemInstanceHandle, int> leaseCounts =
            new Dictionary<ItemInstanceHandle, int>();
        private long nextItemInstanceId;

        public bool IsDisposed { get; private set; }

        public bool IsInitialized { get; private set; }

        public IReadOnlyCollection<ItemInstance> Items => items.Values;

        public IReadOnlyList<ItemInstanceHandle> Initialize(InitialInventorySet initialSet)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("InitialInventorySet can only be applied once per Controller session.");
            }

            IsInitialized = true;
            var handles = new List<ItemInstanceHandle>();
            if (initialSet == null)
            {
                return handles;
            }

            foreach (ItemDefinition definition in initialSet.ItemDefinitions)
            {
                handles.Add(Add(definition));
            }

            return handles;
        }

        public ItemInstanceHandle Add(ItemDefinition definition)
        {
            ThrowIfDisposed();
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var handle = new ItemInstanceHandle(++nextItemInstanceId);
            ItemInstance item = definition.CreateInstance(handle)
                ?? throw new InvalidOperationException("ItemDefinition returned null.");
            if (item.Handle != handle || !ReferenceEquals(item.Definition, definition))
            {
                throw new InvalidOperationException("ItemDefinition returned an instance with invalid ownership identity.");
            }

            items.Add(handle, item);
            return handle;
        }

        public bool TryGet(ItemInstanceHandle handle, out ItemInstance item)
        {
            return items.TryGetValue(handle, out item);
        }

        public InventoryLease AcquireLease(ItemInstanceHandle handle)
        {
            ThrowIfDisposed();
            if (!items.TryGetValue(handle, out ItemInstance item))
            {
                return null;
            }

            leaseCounts.TryGetValue(handle, out int count);
            leaseCounts[handle] = count + 1;
            return new InventoryLease(this, item);
        }

        public bool Remove(ItemInstanceHandle handle)
        {
            ThrowIfDisposed();
            return !leaseCounts.ContainsKey(handle) && items.Remove(handle);
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            leaseCounts.Clear();
            items.Clear();
        }

        internal void ReleaseLease(ItemInstanceHandle handle)
        {
            if (!leaseCounts.TryGetValue(handle, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                leaseCounts.Remove(handle);
            }
            else
            {
                leaseCounts[handle] = count - 1;
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(InventoryComponent));
            }
        }
    }
}
