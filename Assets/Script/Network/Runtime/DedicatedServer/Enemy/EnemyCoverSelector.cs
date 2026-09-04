using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CGame.Network
{
    public enum EnemyCoverValidationFailure
    {
        None,
        ReservationLost,
        CoverVisible,
        PeekOccluded,
        DestinationPathMissing
    }

    public readonly struct EnemyCoverValidationResult
    {
        public EnemyCoverValidationResult(EnemyCoverValidationFailure failure)
        {
            Failure = failure;
        }

        public bool IsValid => Failure == EnemyCoverValidationFailure.None;
        public EnemyCoverValidationFailure Failure { get; }
    }

    public readonly struct EnemyCoverSelection
    {
        public EnemyCoverSelection(string coverPointId, Vector3 coverPosition, Vector3 peekPosition, float reservationRadius)
        {
            CoverPointId = coverPointId;
            CoverPosition = coverPosition;
            PeekPosition = peekPosition;
            ReservationRadius = reservationRadius;
        }

        public string CoverPointId { get; }
        public Vector3 CoverPosition { get; }
        public Vector3 PeekPosition { get; }
        public float ReservationRadius { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(CoverPointId);
    }

    public sealed class EnemyCoverSelector
    {
        private readonly IReadOnlyList<CoverPointDefinition> definitions;
        private readonly IEnemyNavPathQuery navigation;
        private readonly IEnemyPerceptionQuery perception;
        private readonly ICoverReservationRegistry reservations;

        public EnemyCoverSelector(
            IReadOnlyList<CoverPointDefinition> definitions,
            IEnemyNavPathQuery navigation,
            IEnemyPerceptionQuery perception,
            ICoverReservationRegistry reservations)
        {
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            this.reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
        }

        public EnemyCoverSelection SelectAndReserve(long enemyId, Vector3 enemyPosition, EnemyPerceptionCandidate target)
        {
            if (enemyId <= 0) throw new ArgumentOutOfRangeException(nameof(enemyId));
            if (target.PawnId <= 0 || !target.IsAlive || !target.IsPossessed) return default;

            var candidates = new List<Candidate>();
            foreach (CoverPointDefinition definition in definitions)
            {
                if (definition == null || reservations.IsReservedBy(definition.CoverPointId, enemyId)) continue;
                bool hasPath = navigation.TryCalculateCompletePath(enemyPosition, definition.CoverPosition, out IReadOnlyList<Vector3> corners);
                bool coverVisible = hasPath && perception.HasLineOfSight(definition.CoverPosition, target);
                bool peekVisible = hasPath && perception.HasLineOfSight(definition.PeekPosition, target);
                if (!hasPath || coverVisible || !peekVisible) continue;
                candidates.Add(new Candidate(definition, CalculateLength(enemyPosition, corners)));
            }

            foreach (Candidate candidate in candidates.OrderBy(value => value.PathLength).ThenBy(value => value.Definition.CoverPointId, StringComparer.Ordinal))
            {
                if (!reservations.TryReserve(candidate.Definition.CoverPointId, enemyId)) continue;
                CoverPointDefinition definition = candidate.Definition;
                return new EnemyCoverSelection(definition.CoverPointId, definition.CoverPosition, definition.PeekPosition, definition.ReservationRadius);
            }

            return default;
        }

        public void ReleaseByEnemy(long enemyId) => reservations.ReleaseByEnemy(enemyId);

        public EnemyCoverValidationResult ValidateSelection(
            long enemyId,
            EnemyCoverSelection selection,
            Vector3 enemyPosition,
            EnemyPerceptionCandidate target,
            Vector3 destination)
        {
            if (!selection.IsValid || !reservations.IsReservedBy(selection.CoverPointId, enemyId))
                return new EnemyCoverValidationResult(EnemyCoverValidationFailure.ReservationLost);
            if (perception.HasLineOfSight(selection.CoverPosition, target))
                return new EnemyCoverValidationResult(EnemyCoverValidationFailure.CoverVisible);
            if (!perception.HasLineOfSight(selection.PeekPosition, target))
                return new EnemyCoverValidationResult(EnemyCoverValidationFailure.PeekOccluded);
            if (!navigation.TryCalculateCompletePath(enemyPosition, destination, out _))
                return new EnemyCoverValidationResult(EnemyCoverValidationFailure.DestinationPathMissing);
            return default;
        }

        private static float CalculateLength(Vector3 origin, IReadOnlyList<Vector3> corners)
        {
            float total = 0f;
            Vector3 previous = origin;
            foreach (Vector3 corner in corners)
            {
                total += Vector3.Distance(previous, corner);
                previous = corner;
            }
            return total;
        }

        private readonly struct Candidate
        {
            public Candidate(CoverPointDefinition definition, float pathLength)
            {
                Definition = definition;
                PathLength = pathLength;
            }

            public CoverPointDefinition Definition { get; }
            public float PathLength { get; }
        }
    }
}
