using System.Collections.Generic;
using CGame.Ability;

namespace CGame.InventoryEquipment
{
    public sealed class WeaponInstance : EquipmentInstance
    {
        internal WeaponInstance(
            EquipmentCreateContext context,
            int magazineCapacity,
            IEnumerable<AbilitySet> abilitySets)
            : base(context, abilitySets)
        {
            MagazineCapacity = magazineCapacity;
        }

        public int MagazineCapacity { get; }

        public int FireCount { get; private set; }

        public int ReloadCount { get; private set; }

        public int MeleeCount { get; private set; }

        public bool Fire()
        {
            if (IsDisposed || !Item.TryConsumeMagazineAmmo())
            {
                return false;
            }

            FireCount++;
            Item.SetDurability(Item.Durability - 0.001f);
            return true;
        }

        public int Reload()
        {
            if (IsDisposed)
            {
                return 0;
            }

            int loaded = Item.ReloadMagazine(MagazineCapacity);
            if (loaded > 0)
            {
                ReloadCount++;
            }

            return loaded;
        }

        public bool Melee()
        {
            if (IsDisposed)
            {
                return false;
            }

            MeleeCount++;
            Item.SetDurability(Item.Durability - 0.01f);
            return true;
        }
    }
}
