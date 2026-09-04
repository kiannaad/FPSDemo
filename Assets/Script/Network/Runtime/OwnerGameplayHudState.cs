using System;

namespace CGame.Network
{
    public readonly struct OwnerGameplayVitals
    {
        public OwnerGameplayVitals(long pawnId, long revision, int health, int maxHealth, bool isDead)
        {
            PawnId = pawnId;
            Revision = revision;
            Health = health;
            MaxHealth = maxHealth;
            IsDead = isDead;
        }

        public long PawnId { get; }
        public long Revision { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public bool IsDead { get; }
    }

    public readonly struct OwnerGameplayEquipment
    {
        public OwnerGameplayEquipment(long pawnId, long revision, string weaponName, int magazineAmmo, int magazineCapacity)
        {
            PawnId = pawnId;
            Revision = revision;
            WeaponName = weaponName;
            MagazineAmmo = magazineAmmo;
            MagazineCapacity = magazineCapacity;
        }

        public long PawnId { get; }
        public long Revision { get; }
        public string WeaponName { get; }
        public int MagazineAmmo { get; }
        public int MagazineCapacity { get; }
    }

    public sealed class OwnerGameplayHudState
    {
        private OwnerGameplayVitals vitals;
        private OwnerGameplayEquipment equipment;
        private long appliedVitalsRevision = -1;
        private long appliedEquipmentRevision = -1;

        public bool Visible { get; private set; }
        public string Text { get; private set; } = string.Empty;

        public void Reset()
        {
            vitals = default;
            equipment = default;
            appliedVitalsRevision = -1;
            appliedEquipmentRevision = -1;
            Visible = false;
            Text = string.Empty;
        }

        public bool Apply(OwnerGameplayVitals nextVitals, OwnerGameplayEquipment nextEquipment, bool isLocallyPossessed)
        {
            bool changed = false;
            if (nextVitals.Revision > appliedVitalsRevision)
            {
                vitals = nextVitals;
                appliedVitalsRevision = nextVitals.Revision;
                changed = true;
            }
            if (nextEquipment.Revision > appliedEquipmentRevision)
            {
                equipment = nextEquipment;
                appliedEquipmentRevision = nextEquipment.Revision;
                changed = true;
            }

            bool visible = isLocallyPossessed &&
                vitals.PawnId > 0 && vitals.PawnId == equipment.PawnId &&
                vitals.MaxHealth > 0 && vitals.Health >= 0 && vitals.Health <= vitals.MaxHealth &&
                !string.IsNullOrWhiteSpace(equipment.WeaponName) &&
                equipment.MagazineCapacity > 0 && equipment.MagazineAmmo >= 0 && equipment.MagazineAmmo <= equipment.MagazineCapacity;
            string text = visible
                ? $"{equipment.WeaponName}  HP {vitals.Health}/{vitals.MaxHealth}  Ammo {equipment.MagazineAmmo}/{equipment.MagazineCapacity}"
                : string.Empty;
            if (Visible != visible || Text != text)
            {
                Visible = visible;
                Text = text;
                changed = true;
            }
            return changed;
        }
    }
}
