using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public sealed class DedicatedPawnCombatState
    {
        private readonly Dictionary<string, DedicatedPawnVitalsResult> damageResultsByCause = new Dictionary<string, DedicatedPawnVitalsResult>(StringComparer.Ordinal);
        private readonly Dictionary<long, DedicatedPawnEquipmentResult> fireResultsBySequence = new Dictionary<long, DedicatedPawnEquipmentResult>();

        public DedicatedPawnCombatState(long pawnId, int maxHealth, int magazineAmmo, int magazineCapacity, string weaponName)
        {
            if (pawnId <= 0 || maxHealth <= 0 || magazineAmmo < 0 || magazineAmmo > magazineCapacity || magazineCapacity <= 0 || string.IsNullOrWhiteSpace(weaponName))
                throw new ArgumentOutOfRangeException(nameof(pawnId));
            PawnId = pawnId;
            Health = maxHealth;
            MaxHealth = maxHealth;
            MagazineAmmo = magazineAmmo;
            MagazineCapacity = magazineCapacity;
            WeaponName = weaponName;
        }

        public long PawnId { get; }
        public int Health { get; private set; }
        public int MaxHealth { get; }
        public bool IsDead => Health <= 0;
        public long VitalsRevision { get; private set; }
        public string WeaponName { get; }
        public int MagazineAmmo { get; private set; }
        public int MagazineCapacity { get; }
        public long EquipmentRevision { get; private set; }

        public DedicatedPawnVitalsResult ApplyEnemyDamage(long enemyId, long actionSequence, int damage)
        {
            if (enemyId <= 0 || actionSequence <= 0 || damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            string cause = $"{enemyId}:{actionSequence}";
            if (damageResultsByCause.TryGetValue(cause, out DedicatedPawnVitalsResult replay)) return replay.WithReplay();
            int previous = Health;
            Health = Math.Max(0, Health - damage);
            if (Health != previous) VitalsRevision = checked(VitalsRevision + 1);
            var result = new DedicatedPawnVitalsResult(PawnId, Health, MaxHealth, VitalsRevision, IsDead, previous > 0 && IsDead, false);
            damageResultsByCause.Add(cause, result);
            return result;
        }

        public DedicatedPawnEquipmentResult TryConsumeFire(long clientShotSequence)
        {
            if (clientShotSequence <= 0)
                return new DedicatedPawnEquipmentResult(false, MagazineAmmo, MagazineCapacity, EquipmentRevision, false);
            if (fireResultsBySequence.TryGetValue(clientShotSequence, out DedicatedPawnEquipmentResult replay))
                return replay.WithReplay();
            if (IsDead || MagazineAmmo <= 0)
            {
                var rejected = new DedicatedPawnEquipmentResult(false, MagazineAmmo, MagazineCapacity, EquipmentRevision, false);
                fireResultsBySequence.Add(clientShotSequence, rejected);
                return rejected;
            }
            MagazineAmmo--;
            EquipmentRevision = checked(EquipmentRevision + 1);
            var accepted = new DedicatedPawnEquipmentResult(true, MagazineAmmo, MagazineCapacity, EquipmentRevision, false);
            fireResultsBySequence.Add(clientShotSequence, accepted);
            return accepted;
        }
    }

    public readonly struct DedicatedPawnVitalsResult
    {
        public DedicatedPawnVitalsResult(long pawnId, int health, int maxHealth, long vitalsRevision, bool isDead, bool diedThisHit, bool isReplay)
        {
            PawnId = pawnId; Health = health; MaxHealth = maxHealth; VitalsRevision = vitalsRevision; IsDead = isDead; DiedThisHit = diedThisHit; IsReplay = isReplay;
        }
        public long PawnId { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public long VitalsRevision { get; }
        public bool IsDead { get; }
        public bool DiedThisHit { get; }
        public bool IsReplay { get; }
        public DedicatedPawnVitalsResult WithReplay() => new DedicatedPawnVitalsResult(PawnId, Health, MaxHealth, VitalsRevision, IsDead, DiedThisHit, true);
    }

    public readonly struct DedicatedPawnEquipmentResult
    {
        public DedicatedPawnEquipmentResult(bool accepted, int magazineAmmo, int magazineCapacity, long equipmentRevision, bool isReplay)
        {
            Accepted = accepted; MagazineAmmo = magazineAmmo; MagazineCapacity = magazineCapacity; EquipmentRevision = equipmentRevision; IsReplay = isReplay;
        }
        public bool Accepted { get; }
        public int MagazineAmmo { get; }
        public int MagazineCapacity { get; }
        public long EquipmentRevision { get; }
        public bool IsReplay { get; }
        public DedicatedPawnEquipmentResult WithReplay() => new DedicatedPawnEquipmentResult(Accepted, MagazineAmmo, MagazineCapacity, EquipmentRevision, true);
    }
}
