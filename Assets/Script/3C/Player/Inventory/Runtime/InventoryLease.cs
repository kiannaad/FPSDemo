using System;

namespace CGame
{
    public sealed class InventoryLease : IDisposable
    {
        private InventoryComponent inventory;

        internal InventoryLease(InventoryComponent inventory, ItemInstance item)
        {
            this.inventory = inventory;
            Item = item;
        }

        public ItemInstance Item { get; }

        public ItemInstanceHandle Handle => Item?.Handle ?? default;

        public bool IsActive => inventory != null;

        public void Dispose()
        {
            InventoryComponent current = inventory;
            inventory = null;
            current?.ReleaseLease(Handle);
        }
    }
}
