using System;

namespace CGame.InventoryEquipment
{
    public abstract class EquippableItemDefinition : ItemDefinition
    {
        public abstract EquipmentDefinition EquipmentDefinition { get; }
    }
}
