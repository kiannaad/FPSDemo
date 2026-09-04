using System;
using UnityEngine;

namespace CGame.Network
{
    [Serializable]
    public struct EnemyFireDefinition
    {
        [SerializeField] private float engagementRange;
        [SerializeField] private float hitscanRange;
        [SerializeField] private float minimumFacingDot;
        [SerializeField] private int cooldownTicks;
        [SerializeField] private int magazineCapacity;
        [SerializeField] private int damage;

        public static EnemyFireDefinition Default => new EnemyFireDefinition(2.25f, 12f, 0.25f, 30, 3, 8);

        public EnemyFireDefinition(float engagementRange, float hitscanRange, float minimumFacingDot, int cooldownTicks, int magazineCapacity, int damage)
        {
            this.engagementRange = engagementRange;
            this.hitscanRange = hitscanRange;
            this.minimumFacingDot = minimumFacingDot;
            this.cooldownTicks = cooldownTicks;
            this.magazineCapacity = magazineCapacity;
            this.damage = damage;
        }

        public float EngagementRange => engagementRange;
        public float HitscanRange => hitscanRange;
        public float MinimumFacingDot => minimumFacingDot;
        public int CooldownTicks => cooldownTicks;
        public int MagazineCapacity => magazineCapacity;
        public int Damage => damage;
        public bool IsValid => engagementRange > 0f && hitscanRange >= engagementRange &&
            minimumFacingDot >= -1f && minimumFacingDot <= 1f && cooldownTicks > 0 &&
            magazineCapacity > 0 && damage > 0;

        public void Validate()
        {
            if (!IsValid) throw new InvalidOperationException("Enemy fire definition contains invalid values.");
        }
    }

    public interface IEnemyHitscanQuery
    {
        bool TryHitPawn(Vector3 muzzleOrigin, Vector3 direction, float range, out long pawnId);
    }

    public readonly struct EnemyFireResolution
    {
        public EnemyFireResolution(bool fired, bool hitTarget, bool enteredNoAmmo, long actionSequence, int damage)
        {
            Fired = fired;
            HitTarget = hitTarget;
            EnteredNoAmmo = enteredNoAmmo;
            ActionSequence = actionSequence;
            Damage = damage;
        }

        public bool Fired { get; }
        public bool HitTarget { get; }
        public bool EnteredNoAmmo { get; }
        public long ActionSequence { get; }
        public int Damage { get; }
    }

    public sealed class DedicatedEnemyFireResolver
    {
        private readonly EnemyFireDefinition definition;
        private long nextFireTick;
        private long actionSequence;

        public DedicatedEnemyFireResolver(EnemyFireDefinition definition)
        {
            definition.Validate();
            this.definition = definition;
            MagazineAmmo = definition.MagazineCapacity;
        }

        public int MagazineAmmo { get; private set; }
        public bool IsOutOfAmmo => MagazineAmmo <= 0;

        public EnemyFireResolution TryResolve(
            long serverTick,
            Vector3 muzzleOrigin,
            Vector3 forward,
            Vector3 aimPoint,
            long targetPawnId,
            IEnemyHitscanQuery query)
        {
            if (serverTick <= 0 || targetPawnId <= 0 || query == null)
                return default;
            if (IsOutOfAmmo)
                return new EnemyFireResolution(false, false, false, actionSequence, 0);
            if (serverTick < nextFireTick) return default;

            Vector3 aimVector = aimPoint - muzzleOrigin;
            float distance = aimVector.magnitude;
            if (distance <= Mathf.Epsilon || distance > definition.HitscanRange) return default;
            Vector3 direction = aimVector / distance;
            if (forward.sqrMagnitude > Mathf.Epsilon && Vector3.Dot(forward.normalized, direction) < definition.MinimumFacingDot)
                return default;

            MagazineAmmo--;
            actionSequence = checked(actionSequence + 1);
            nextFireTick = checked(serverTick + definition.CooldownTicks);
            bool hitTarget = query.TryHitPawn(muzzleOrigin, direction, definition.HitscanRange, out long hitPawnId) && hitPawnId == targetPawnId;
            return new EnemyFireResolution(true, hitTarget, MagazineAmmo == 0, actionSequence, hitTarget ? definition.Damage : 0);
        }
    }
}
