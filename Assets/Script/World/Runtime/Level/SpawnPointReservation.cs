using System;
using UnityEngine;

namespace CGame
{
    public sealed class SpawnPointReservation : IDisposable
    {
        private LevelRuntime owner;

        internal SpawnPointReservation(
            LevelRuntime runtime,
            string pointId,
            SpawnPointKind kind,
            Transform transform)
        {
            owner = runtime;
            PointId = pointId;
            Kind = kind;
            Transform = transform;
        }

        public string PointId { get; }
        public SpawnPointKind Kind { get; }
        public Transform Transform { get; }

        public void Commit(Guid registrationId)
        {
            if (owner == null) throw new ObjectDisposedException(nameof(SpawnPointReservation));
            owner.Commit(PointId, registrationId);
            owner = null;
        }

        public void Dispose()
        {
            owner?.Rollback(PointId);
            owner = null;
        }
    }
}
