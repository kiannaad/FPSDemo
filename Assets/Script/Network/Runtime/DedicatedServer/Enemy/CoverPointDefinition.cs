using System;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "CoverPoint", menuName = "CGame/Network/Dedicated Cover Point")]
    public sealed class CoverPointDefinition : ScriptableObject
    {
        [SerializeField] private string coverPointId;
        [SerializeField] private string levelId;
        [SerializeField] private Vector3 coverPosition;
        [SerializeField] private Vector3 peekPosition;
        [SerializeField] private float reservationRadius = 0.75f;

        public string CoverPointId => coverPointId;
        public string LevelId => levelId;
        public Vector3 CoverPosition => coverPosition;
        public Vector3 PeekPosition => peekPosition;
        public float ReservationRadius => reservationRadius;

        public void Configure(string coverPointId, string levelId, Vector3 coverPosition, Vector3 peekPosition, float reservationRadius)
        {
            ValidateValues(coverPointId, levelId, coverPosition, peekPosition, reservationRadius);
            this.coverPointId = coverPointId;
            this.levelId = levelId;
            this.coverPosition = coverPosition;
            this.peekPosition = peekPosition;
            this.reservationRadius = reservationRadius;
        }

        public void ValidateStatic(IEnemyNavPathQuery navigation)
        {
            if (navigation == null) throw new ArgumentNullException(nameof(navigation));
            ValidateValues(coverPointId, levelId, coverPosition, peekPosition, reservationRadius);
            if (!navigation.TrySample(coverPosition, out _))
                throw new InvalidOperationException($"Cover point {coverPointId} cover position cannot be sampled.");
            if (!navigation.TrySample(peekPosition, out _))
                throw new InvalidOperationException($"Cover point {coverPointId} peek position cannot be sampled.");
            if (!navigation.TryCalculateCompletePath(coverPosition, peekPosition, out _))
                throw new InvalidOperationException($"Cover point {coverPointId} has no complete cover-to-peek path.");
        }

        private static void ValidateValues(string coverPointId, string levelId, Vector3 coverPosition, Vector3 peekPosition, float reservationRadius)
        {
            if (string.IsNullOrWhiteSpace(coverPointId)) throw new InvalidOperationException("Cover point requires a CoverPointId.");
            if (string.IsNullOrWhiteSpace(levelId)) throw new InvalidOperationException("Cover point requires a LevelId.");
            if (!IsFinite(coverPosition) || !IsFinite(peekPosition)) throw new InvalidOperationException("Cover point positions must be finite.");
            if (float.IsNaN(reservationRadius) || float.IsInfinity(reservationRadius) || reservationRadius <= 0f)
                throw new InvalidOperationException("Cover point ReservationRadius must be finite and positive.");
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
