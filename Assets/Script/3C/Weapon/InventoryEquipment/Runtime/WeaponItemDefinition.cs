using System;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [CreateAssetMenu(fileName = "WeaponItemDefinition", menuName = "CGame/Inventory/Weapon Item")]
    public sealed class WeaponItemDefinition : EquippableItemDefinition
    {
        [SerializeField] private WeaponDefinition weaponDefinition;
        [SerializeField] private int initialMagazineAmmo = 30;
        [SerializeField] private int initialReserveAmmo = 90;
        [NonSerialized] private WeaponDefinition runtimeWeaponDefinition;

        public WeaponDefinition WeaponDefinition => runtimeWeaponDefinition ?? weaponDefinition;

        public override EquipmentDefinition EquipmentDefinition => WeaponDefinition;

        public void ValidateCatalog(WeaponCatalogSubSystem catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (WeaponDefinition == null || !catalog.ContainsExact(WeaponDefinition))
            {
                throw new InvalidOperationException("Weapon Item Definition must reference its exact registered Weapon Definition.");
            }
        }

        public override ItemInstance CreateInstance(ItemInstanceHandle handle)
        {
            ItemInstance item = base.CreateInstance(handle);
            item.SetAmmo(initialMagazineAmmo, initialReserveAmmo);
            return item;
        }

        public void Configure(WeaponDefinition definition, int magazineAmmo, int reserveAmmo)
        {
            weaponDefinition = definition ?? throw new ArgumentNullException(nameof(definition));
            initialMagazineAmmo = Math.Max(0, magazineAmmo);
            initialReserveAmmo = Math.Max(0, reserveAmmo);
        }

        public static WeaponItemDefinition CreateRuntime(
            WeaponDefinition weaponDefinition,
            int magazineAmmo,
            int reserveAmmo)
        {
            if (weaponDefinition == null)
            {
                throw new ArgumentNullException(nameof(weaponDefinition));
            }

            WeaponItemDefinition definition = CreateInstance<WeaponItemDefinition>();
            definition.runtimeWeaponDefinition = weaponDefinition;
            definition.initialMagazineAmmo = Math.Max(0, magazineAmmo);
            definition.initialReserveAmmo = Math.Max(0, reserveAmmo);
            return definition;
        }
    }
}
