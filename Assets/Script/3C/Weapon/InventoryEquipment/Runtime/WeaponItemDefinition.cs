using System;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [CreateAssetMenu(fileName = "WeaponItemDefinition", menuName = "CGame/Inventory/Weapon Item")]
    public sealed class WeaponItemDefinition : EquippableItemDefinition
    {
        [SerializeField] private WeaponEquipmentDefinition equipmentDefinition;
        [SerializeField] private int initialMagazineAmmo = 30;
        [SerializeField] private int initialReserveAmmo = 90;
        [NonSerialized] private WeaponEquipmentDefinition runtimeEquipmentDefinition;

        public override EquipmentDefinition EquipmentDefinition => runtimeEquipmentDefinition ?? equipmentDefinition;

        public override ItemInstance CreateInstance(ItemInstanceHandle handle)
        {
            ItemInstance item = base.CreateInstance(handle);
            item.SetAmmo(initialMagazineAmmo, initialReserveAmmo);
            return item;
        }

        public static WeaponItemDefinition CreateRuntime(
            WeaponEquipmentDefinition equipmentDefinition,
            int magazineAmmo,
            int reserveAmmo)
        {
            if (equipmentDefinition == null)
            {
                throw new ArgumentNullException(nameof(equipmentDefinition));
            }

            WeaponItemDefinition definition = CreateInstance<WeaponItemDefinition>();
            definition.runtimeEquipmentDefinition = equipmentDefinition;
            definition.initialMagazineAmmo = Math.Max(0, magazineAmmo);
            definition.initialReserveAmmo = Math.Max(0, reserveAmmo);
            return definition;
        }
    }
}
