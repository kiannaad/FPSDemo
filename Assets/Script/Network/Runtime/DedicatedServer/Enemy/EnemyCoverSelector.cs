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
        DestinationPathMissing,
        TravelTimeout
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
        private const float maximumApproachDistance = 8f;
        private readonly IReadOnlyList<CoverPointDefinition> definitions;
        private readonly IEnemyNavPathQuery navigation;
        private readonly IEnemyPerceptionQuery perception;
        private readonly IEnemyPerceptionQuery firingPerception;
        private readonly ICoverReservationRegistry reservations;

        public EnemyCoverSelector(
            IReadOnlyList<CoverPointDefinition> definitions,
            IEnemyNavPathQuery navigation,
            IEnemyPerceptionQuery perception,
            ICoverReservationRegistry reservations,
            IEnemyPerceptionQuery firingPerception = null)
        {
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            this.firingPerception = firingPerception ?? perception;
            this.reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
        }

        public EnemyCoverSelection SelectAndReserve(long enemyId, Vector3 enemyPosition, EnemyPerceptionCandidate target,
            float engagementRange = float.PositiveInfinity)
        {
            if (enemyId <= 0) throw new ArgumentOutOfRangeException(nameof(enemyId));
            if (target.PawnId <= 0 || !target.IsAlive || !target.IsPossessed) return default;

            var candidates = new List<Candidate>();
            foreach (CoverPointDefinition definition in definitions)
            {
                if (definition == null || reservations.IsReservedBy(definition.CoverPointId, enemyId)) continue;
                bool hasPath = navigation.TryCalculateCompletePath(enemyPosition, definition.CoverPosition, out IReadOnlyList<Vector3> corners);
                float pathLength = hasPath ? CalculateLength(enemyPosition, corners) : float.PositiveInfinity;
                if (pathLength > maximumApproachDistance) continue;
                bool coverVisible = hasPath && perception.HasLineOfSight(definition.CoverPosition, target);
                if (!hasPath || coverVisible || !TryFindFiringPosition(definition, target, out Vector3 firingPosition)) continue;
                if (!TryFindConcealedPosition(definition, target, firingPosition, out Vector3 coverPosition)) continue;
                if (coverPosition != definition.CoverPosition)
                {
                    if (!navigation.TryCalculateCompletePath(enemyPosition, coverPosition, out corners)) continue;
                    pathLength = CalculateLength(enemyPosition, corners);
                    if (pathLength > maximumApproachDistance) continue;
                }
                if (Vector3.Distance(firingPosition, target.Position) > engagementRange) continue;
                bool peekVisible = hasPath && firingPerception.HasLineOfSight(firingPosition, target);
                if (!hasPath || coverVisible || !peekVisible) continue;
                candidates.Add(new Candidate(definition, pathLength, coverPosition, firingPosition));
            }

            foreach (Candidate candidate in candidates.OrderBy(value => value.PathLength).ThenBy(value => value.Definition.CoverPointId, StringComparer.Ordinal))
            {
                if (!reservations.TryReserve(candidate.Definition.CoverPointId, enemyId)) continue;
                CoverPointDefinition definition = candidate.Definition;
                return new EnemyCoverSelection(definition.CoverPointId, candidate.CoverPosition, candidate.FiringPosition, definition.ReservationRadius);
            }

            return default;
        }

        public void ReleaseByEnemy(long enemyId) => reservations.ReleaseByEnemy(enemyId);

        private bool TryFindConcealedPosition(CoverPointDefinition definition, EnemyPerceptionCandidate target,
            Vector3 firingPosition, out Vector3 coverPosition)
        {
            coverPosition = definition.CoverPosition;
            if (Vector3.Distance(coverPosition, firingPosition) <= 1f) return true;
            Vector3 returnDirection = (coverPosition - firingPosition).normalized;
            for (float retreat = Mathf.Max(.6f, definition.ReservationRadius * 2f); retreat <= 1.001f; retreat += .1f)
            {
                Vector3 candidate = firingPosition + returnDirection * retreat;
                if (!navigation.TrySample(candidate, out Vector3 sampled) ||
                    (sampled - candidate).sqrMagnitude > .0225f ||
                    Vector3.Distance(sampled, firingPosition) > 1f ||
                    perception.HasLineOfSight(sampled, target) ||
                    perception.HasLineOfSight(sampled - returnDirection * definition.ReservationRadius, target) ||
                    !navigation.TryCalculateCompletePath(sampled, firingPosition, out _)) continue;
                coverPosition = sampled;
                return true;
            }
            // A centre-of-wall point without a protected nearby firing edge is
            // not a usable peek position. Let the caller choose another cover.
            return false;
        }

        private bool TryFindFiringPosition(CoverPointDefinition definition, EnemyPerceptionCandidate target,
            out Vector3 firingPosition)
        {
            firingPosition = definition.CoverPosition;
            if (firingPerception.HasLineOfSight(firingPosition, target)) return true;

            Vector3 offset = definition.PeekPosition - definition.CoverPosition;
            float distance = offset.magnitude;
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / .1f), 1, 64);
            for (int step = 1; step <= steps; step++)
            {
                Vector3 candidate = definition.CoverPosition + offset * (step / (float)steps);
                if (!firingPerception.HasLineOfSight(candidate, target)) continue;
                // Leave room for the motor's arrival tolerance without walking
                // all the way to an unnecessarily exposed authored endpoint.
                Vector3 clearance = Vector3.MoveTowards(candidate, definition.PeekPosition, definition.ReservationRadius);
                if (firingPerception.HasLineOfSight(clearance, target)) candidate = clearance;
                if (!navigation.TrySample(candidate, out Vector3 sampled) ||
                    (sampled - candidate).sqrMagnitude > .0225f ||
                    !firingPerception.HasLineOfSight(sampled, target) ||
                    !navigation.TryCalculateCompletePath(definition.CoverPosition, sampled, out _)) continue;
                firingPosition = sampled;
                return true;
            }
            return false;
        }

        public bool HasFiringLineOfSight(Vector3 origin, EnemyPerceptionCandidate target) =>
            firingPerception.HasLineOfSight(origin, target);

        public bool HasConcealedLineOfSight(Vector3 origin, EnemyPerceptionCandidate target) =>
            perception.HasLineOfSight(origin, target);

        public bool HasReservation(long enemyId, EnemyCoverSelection selection) =>
            selection.IsValid && reservations.IsReservedBy(selection.CoverPointId, enemyId);

        public EnemyCoverValidationResult ValidateSelection(
            long enemyId,
            EnemyCoverSelection selection,
            Vector3 enemyPosition,
            EnemyPerceptionCandidate target,
            Vector3 destination)
        {
            if (!HasReservation(enemyId, selection))
                return new EnemyCoverValidationResult(EnemyCoverValidationFailure.ReservationLost);
            if (perception.HasLineOfSight(selection.CoverPosition, target))
                return new EnemyCoverValidationResult(EnemyCoverValidationFailure.CoverVisible);
            if (!firingPerception.HasLineOfSight(selection.PeekPosition, target))
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
            public Candidate(CoverPointDefinition definition, float pathLength, Vector3 coverPosition, Vector3 firingPosition)
            {
                Definition = definition;
                PathLength = pathLength;
                CoverPosition = coverPosition;
                FiringPosition = firingPosition;
            }

            public CoverPointDefinition Definition { get; }
            public float PathLength { get; }
            public Vector3 CoverPosition { get; }
            public Vector3 FiringPosition { get; }
        }
    }
}
