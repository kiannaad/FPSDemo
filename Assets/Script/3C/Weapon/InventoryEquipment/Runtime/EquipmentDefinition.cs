using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    public abstract class EquipmentDefinition : ScriptableObject
    {
        public abstract int LoadTicks { get; }

        public abstract bool SimulateLoadFailure { get; }

        public abstract EquipmentInstance CreateInstance(EquipmentCreateContext context);
    }

    public sealed class EquipmentCreateContext
    {
        public EquipmentCreateContext(
            InventoryLease inventoryLease,
            AbilitySystemComponent abilitySystem,
            IDisposable presentationReceipt = null,
            IReadOnlyList<AbilityGrantReceipt> replacedAbilityReceipts = null)
        {
            InventoryLease = inventoryLease ?? throw new ArgumentNullException(nameof(inventoryLease));
            AbilitySystem = abilitySystem ?? throw new ArgumentNullException(nameof(abilitySystem));
            PresentationReceipt = presentationReceipt;
            ReplacedAbilityReceipts = replacedAbilityReceipts;
        }

        public InventoryLease InventoryLease { get; }

        public AbilitySystemComponent AbilitySystem { get; }

        public IDisposable PresentationReceipt { get; }
        public IReadOnlyList<AbilityGrantReceipt> ReplacedAbilityReceipts { get; }
    }
}
