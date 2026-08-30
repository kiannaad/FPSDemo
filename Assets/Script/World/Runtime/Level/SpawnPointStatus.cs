using System;
using UnityEngine;

namespace CGame
{
    public readonly struct SpawnPointStatus
    {
        public SpawnPointStatus(
            string id,
            SpawnPointKind kind,
            Transform transform,
            SpawnPointState state,
            Guid registrationId)
        {
            PointId = id;
            Kind = kind;
            Transform = transform;
            State = state;
            RegistrationId = registrationId;
        }

        public string PointId { get; }
        public SpawnPointKind Kind { get; }
        public Transform Transform { get; }
        public SpawnPointState State { get; }
        public Guid RegistrationId { get; }
        public bool HasOccupiedTag => State == SpawnPointState.Occupied;
    }
}
