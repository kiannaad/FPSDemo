using System;
using System.Collections.Generic;
using CGame.Ability;

namespace CGame.InventoryEquipment
{
    public abstract class EquipmentInstance : IDisposable
    {
        private readonly List<AbilityGrantReceipt> abilityReceipts = new List<AbilityGrantReceipt>();
        private InventoryLease inventoryLease;
        private IDisposable presentationReceipt;

        protected EquipmentInstance(
            EquipmentCreateContext context,
            IEnumerable<AbilitySet> abilitySets)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            inventoryLease = context.InventoryLease;
            presentationReceipt = context.PresentationReceipt;
            AbilitySystem = context.AbilitySystem;
            Item = inventoryLease.Item;
            var uniqueSets = new HashSet<AbilitySet>();
            try
            {
                if (abilitySets == null)
                {
                    return;
                }

                foreach (AbilitySet abilitySet in abilitySets)
                {
                    if (abilitySet == null)
                    {
                        throw new ArgumentException("Equipment ability sets cannot contain null.", nameof(abilitySets));
                    }

                    if (uniqueSets.Add(abilitySet))
                    {
                        abilityReceipts.Add(AbilitySystem.GiveAbilitySet(abilitySet, this, context.ReplacedAbilityReceipts));
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public AbilitySystemComponent AbilitySystem { get; }

        public ItemInstance Item { get; }

        public ItemInstanceHandle ItemHandle => Item.Handle;

        public IReadOnlyList<AbilityGrantReceipt> AbilityReceipts => abilityReceipts;

        public bool IsDisposed { get; private set; }

        public virtual void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            for (int index = abilityReceipts.Count - 1; index >= 0; index--)
            {
                abilityReceipts[index].Revoke();
            }

            abilityReceipts.Clear();
            presentationReceipt?.Dispose();
            presentationReceipt = null;
            inventoryLease?.Dispose();
            inventoryLease = null;
        }
    }
}
