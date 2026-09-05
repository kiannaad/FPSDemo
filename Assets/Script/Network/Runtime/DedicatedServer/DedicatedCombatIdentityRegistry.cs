using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public enum DedicatedCombatIdentityKind : byte
    {
        None = 0,
        Pawn = 1,
        Enemy = 2
    }

    public readonly struct DedicatedCombatIdentity : IEquatable<DedicatedCombatIdentity>
    {
        public DedicatedCombatIdentity(DedicatedCombatIdentityKind kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        public DedicatedCombatIdentityKind Kind { get; }
        public long Id { get; }
        public bool Equals(DedicatedCombatIdentity other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is DedicatedCombatIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Id);
        public static bool operator ==(DedicatedCombatIdentity left, DedicatedCombatIdentity right) => left.Equals(right);
        public static bool operator !=(DedicatedCombatIdentity left, DedicatedCombatIdentity right) => !left.Equals(right);
    }

    public sealed class DedicatedCombatIdentityRegistry
    {
        private readonly Dictionary<Collider, DedicatedCombatIdentity> identitiesByCollider =
            new Dictionary<Collider, DedicatedCombatIdentity>();

        public void RegisterEnemy(long enemyId, GameObject root) => Register(new DedicatedCombatIdentity(DedicatedCombatIdentityKind.Enemy, enemyId), root);
        public void RegisterPawn(long pawnId, GameObject root) => Register(new DedicatedCombatIdentity(DedicatedCombatIdentityKind.Pawn, pawnId), root);

        public DedicatedCombatIdentity Resolve(Collider collider) =>
            collider != null && identitiesByCollider.TryGetValue(collider, out DedicatedCombatIdentity identity)
                ? identity
                : default;

        private void Register(DedicatedCombatIdentity identity, GameObject root)
        {
            if (identity.Id <= 0) throw new ArgumentOutOfRangeException(nameof(identity));
            if (root == null) throw new ArgumentNullException(nameof(root));
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) throw new InvalidOperationException($"Dedicated {identity.Kind} requires a hit Collider.");
            foreach (Collider collider in colliders)
            {
                if (identitiesByCollider.ContainsKey(collider))
                    throw new InvalidOperationException($"Dedicated hit Collider is already registered: {collider.name}.");
                identitiesByCollider.Add(collider, identity);
            }
        }
    }
}
