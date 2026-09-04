using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public enum EnemyMoveTargetKind
    {
        PatrolRoutePoint,
        ChaseTarget,
        ReturnRoutePoint,
        CoverPosition,
        PeekPosition
    }

    public enum EnemyBrainState
    {
        Patrol,
        Chase,
        ReturnToRoute,
        Fire,
        TakeCover,
        CoverHold,
        PeekFire,
        NoAmmo
    }

    public readonly struct EnemyPerceptionCandidate
    {
        public EnemyPerceptionCandidate(long pawnId, Vector3 position, bool isPossessed, bool isAlive, Transform root = null)
        {
            PawnId = pawnId;
            Position = position;
            IsPossessed = isPossessed;
            IsAlive = isAlive;
            Root = root;
        }

        public long PawnId { get; }
        public Vector3 Position { get; }
        public bool IsPossessed { get; }
        public bool IsAlive { get; }
        public Transform Root { get; }
    }

    public interface IEnemyPerceptionQuery
    {
        bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate);
    }

    public readonly struct EnemyMovementIntent
    {
        public EnemyMovementIntent(EnemyNavigationIntent navigationIntent, EnemyMoveTargetKind targetKind)
        {
            NavigationIntent = navigationIntent;
            TargetKind = targetKind;
        }

        public EnemyNavigationIntent NavigationIntent { get; }
        public EnemyMoveTargetKind TargetKind { get; }
    }

    public readonly struct EnemyBrainOutput
    {
        private EnemyBrainOutput(EnemyMovementIntent movementIntent, EnemyBrainState state, long targetPawnId, bool hasFireRequest)
        {
            MovementIntent = movementIntent;
            State = state;
            TargetPawnId = targetPawnId;
            HasFireRequest = hasFireRequest;
            HasMovementIntent = true;
        }

        public EnemyMovementIntent MovementIntent { get; }
        public EnemyBrainState State { get; }
        public long TargetPawnId { get; }
        public bool HasMovementIntent { get; }
        public bool HasFireRequest { get; }

        public static EnemyBrainOutput ForMovement(
            EnemyMovementIntent movementIntent,
            EnemyBrainState state,
            long targetPawnId,
            bool hasFireRequest = false) => new EnemyBrainOutput(movementIntent, state, targetPawnId, hasFireRequest || state == EnemyBrainState.Fire);
    }

    public sealed class EnemyBrain
    {
        private readonly EnemyPatrolController patrol;
        private readonly EnemyNavPathComponent navigation;
        private readonly IEnemyPerceptionQuery perception;
        private readonly float engagementRange;
        private readonly EnemyCoverSelector coverSelector;
        private readonly long enemyId;
        // Patrol routes deliberately carry enemies away from their spawn point.
        // Keep perception wide enough to reacquire the possessed pawn after the
        // initial patrol window; firing remains constrained by engagementRange.
        private const float PerceptionRange = 12f;
        // The three enemies spawn close to the possessed player in SampleScene.
        // Give their replicated presentations time to spawn and visibly traverse
        // their authored routes before perception may transition into combat.
        private const long InitialPatrolTicks = 180;
        private const long LostSightGraceTicks = 120;
        private const long CoverEngagementGraceTicks = 600;
        private EnemyBrainState state;
        private long targetPawnId;
        private Vector3 lastKnownTargetPosition;
        private long lastSeenTargetTick;
        private EnemyCoverSelection coverSelection;
        private long nextCoverTransitionTick;

        public EnemyBrain(
            EnemyArchetypeCombatDefinition definition,
            IEnemyNavPathQuery navigationQuery,
            IEnemyPerceptionQuery perceptionQuery = null,
            EnemyCoverSelector coverSelector = null,
            long enemyId = 0)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            patrol = new EnemyPatrolController(definition.PatrolRoute);
            navigation = new EnemyNavPathComponent(navigationQuery, retryIntervalTicks: 30);
            perception = perceptionQuery ?? new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers);
            engagementRange = definition.FireDefinition.EngagementRange;
            this.coverSelector = coverSelector;
            this.enemyId = enemyId;
        }

        public string RouteId => patrol.RouteId;
        public int PointIndex => patrol.PointIndex;
        public EnemyBrainState State => state;
        public long TargetPawnId => targetPawnId;
        public string CoverPointId => coverSelection.CoverPointId ?? string.Empty;

        public EnemyBrainOutput TickPatrol(long serverTick, DedicatedEnemyMotorState motorState)
        {
            return Tick(serverTick, motorState, Array.Empty<EnemyPerceptionCandidate>());
        }

        public EnemyBrainOutput Tick(
            long serverTick,
            DedicatedEnemyMotorState motorState,
            IReadOnlyList<EnemyPerceptionCandidate> candidates)
        {
            if (serverTick < 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (serverTick <= InitialPatrolTicks)
            {
                state = EnemyBrainState.Patrol;
                targetPawnId = 0;
                patrol.AdvanceIfArrived(motorState.Position);
                return BuildMovement(serverTick, motorState.Position, patrol.CurrentTarget, EnemyMoveTargetKind.PatrolRoutePoint);
            }
            if (coverSelection.IsValid &&
                TryGetCandidate(targetPawnId, candidates, out EnemyPerceptionCandidate retainedTarget) &&
                serverTick - lastSeenTargetTick <= CoverEngagementGraceTicks)
            {
                return TickCover(serverTick, motorState, retainedTarget);
            }
            if (TrySelectVisibleCandidate(motorState.Position, candidates, out EnemyPerceptionCandidate target))
            {
                if (coverSelection.IsValid && targetPawnId != 0 && targetPawnId != target.PawnId)
                    ClearCoverReservation();
                targetPawnId = target.PawnId;
                lastKnownTargetPosition = target.Position;
                lastSeenTargetTick = serverTick;
                if (coverSelection.IsValid)
                    return TickCover(serverTick, motorState, target);

                if (coverSelector != null && state != EnemyBrainState.Chase)
                {
                    state = EnemyBrainState.Chase;
                    return BuildMovement(serverTick, motorState.Position, target.Position, EnemyMoveTargetKind.ChaseTarget);
                }

                EnemyCoverSelection selectedCover = coverSelector == null ? default : coverSelector.SelectAndReserve(enemyId, motorState.Position, target);
                if (selectedCover.IsValid)
                {
                    coverSelection = selectedCover;
                    state = EnemyBrainState.TakeCover;
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                }

                Vector3 planarDelta = target.Position - motorState.Position;
                planarDelta.y = 0f;
                if (planarDelta.sqrMagnitude <= engagementRange * engagementRange)
                {
                    state = EnemyBrainState.Fire;
                    return EnemyBrainOutput.ForMovement(default, state, targetPawnId);
                }
                state = EnemyBrainState.Chase;
                return BuildMovement(serverTick, motorState.Position, target.Position, EnemyMoveTargetKind.ChaseTarget);
            }

            if (state == EnemyBrainState.Chase || state == EnemyBrainState.Fire || state == EnemyBrainState.TakeCover ||
                state == EnemyBrainState.CoverHold || state == EnemyBrainState.PeekFire)
            {
                if (serverTick - lastSeenTargetTick <= LostSightGraceTicks)
                    return BuildMovement(serverTick, motorState.Position, lastKnownTargetPosition, EnemyMoveTargetKind.ChaseTarget);

                ClearCoverReservation();
                targetPawnId = 0;
                patrol.SetNearestPoint(motorState.Position);
                state = EnemyBrainState.ReturnToRoute;
            }

            if (state == EnemyBrainState.ReturnToRoute)
            {
                if (patrol.IsAtCurrentTarget(motorState.Position))
                {
                    state = EnemyBrainState.Patrol;
                    patrol.AdvanceIfArrived(motorState.Position);
                }
                else
                {
                    return BuildMovement(serverTick, motorState.Position, patrol.CurrentTarget, EnemyMoveTargetKind.ReturnRoutePoint);
                }
            }

            patrol.AdvanceIfArrived(motorState.Position);
            return BuildMovement(serverTick, motorState.Position, patrol.CurrentTarget, EnemyMoveTargetKind.PatrolRoutePoint);
        }

        public void InvalidatePathRetry(long serverTick)
        {
            navigation.InvalidatePathRetry(serverTick);
        }

        public void ReleaseCover() => ClearCoverReservation();

        private EnemyBrainOutput TickCover(long serverTick, DedicatedEnemyMotorState motorState, EnemyPerceptionCandidate target)
        {
            if (!coverSelection.IsValid)
            {
                state = EnemyBrainState.Chase;
                return BuildMovement(serverTick, motorState.Position, target.Position, EnemyMoveTargetKind.ChaseTarget);
            }

            if (state == EnemyBrainState.TakeCover)
            {
                if (!IsAt(motorState.Position, coverSelection.CoverPosition, coverSelection.ReservationRadius))
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                state = EnemyBrainState.CoverHold;
                nextCoverTransitionTick = serverTick + 20;
                return EnemyBrainOutput.ForMovement(default, state, targetPawnId);
            }
            if (state == EnemyBrainState.CoverHold)
            {
                if (serverTick < nextCoverTransitionTick)
                    return EnemyBrainOutput.ForMovement(default, state, targetPawnId);
                state = EnemyBrainState.PeekFire;
                return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.PeekPosition, EnemyMoveTargetKind.PeekPosition);
            }
            if (state == EnemyBrainState.PeekFire)
            {
                if (!IsAt(motorState.Position, coverSelection.PeekPosition, coverSelection.ReservationRadius))
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.PeekPosition, EnemyMoveTargetKind.PeekPosition);
                state = EnemyBrainState.CoverHold;
                nextCoverTransitionTick = serverTick + 20;
                return EnemyBrainOutput.ForMovement(default, EnemyBrainState.PeekFire, targetPawnId, hasFireRequest: true);
            }

            state = EnemyBrainState.TakeCover;
            return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
        }

        private void ClearCoverReservation()
        {
            if (coverSelection.IsValid) coverSelector?.ReleaseByEnemy(enemyId);
            coverSelection = default;
        }

        private static bool IsAt(Vector3 position, Vector3 destination, float radius)
        {
            Vector3 delta = destination - position;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
        }

        private EnemyBrainOutput BuildMovement(
            long serverTick,
            Vector3 origin,
            Vector3 destination,
            EnemyMoveTargetKind targetKind)
        {
            EnemyNavigationIntent navigationIntent = navigation.BuildIntent(origin, destination, serverTick);
            return EnemyBrainOutput.ForMovement(
                new EnemyMovementIntent(navigationIntent, targetKind),
                state,
                targetPawnId);
        }

        private EnemyBrainOutput BuildCoverMovement(
            long serverTick,
            Vector3 origin,
            Vector3 fallbackTarget,
            Vector3 coverTarget,
            EnemyMoveTargetKind targetKind)
        {
            EnemyBrainOutput coverOutput = BuildMovement(serverTick, origin, coverTarget, targetKind);
            if (coverOutput.MovementIntent.NavigationIntent.HasPath || coverOutput.MovementIntent.NavigationIntent.IsRetryThrottled)
                return coverOutput;

            ClearCoverReservation();
            state = EnemyBrainState.Chase;
            return BuildMovement(serverTick, origin, fallbackTarget, EnemyMoveTargetKind.ChaseTarget);
        }

        private bool TrySelectVisibleCandidate(
            Vector3 origin,
            IReadOnlyList<EnemyPerceptionCandidate> candidates,
            out EnemyPerceptionCandidate selected)
        {
            selected = default;
            bool found = false;
            float selectedDistanceSquared = 0f;
            float rangeSquared = PerceptionRange * PerceptionRange;
            if (candidates == null) return false;
            for (int index = 0; index < candidates.Count; index++)
            {
                EnemyPerceptionCandidate candidate = candidates[index];
                if (candidate.PawnId <= 0 || !candidate.IsPossessed || !candidate.IsAlive) continue;
                Vector3 delta = candidate.Position - origin;
                delta.y = 0f;
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > rangeSquared || !perception.HasLineOfSight(origin, candidate)) continue;
                if (found && (distanceSquared > selectedDistanceSquared ||
                              (Mathf.Approximately(distanceSquared, selectedDistanceSquared) && candidate.PawnId >= selected.PawnId)))
                    continue;
                selected = candidate;
                selectedDistanceSquared = distanceSquared;
                found = true;
            }
            return found;
        }

        private static bool TryGetCandidate(long pawnId, IReadOnlyList<EnemyPerceptionCandidate> candidates, out EnemyPerceptionCandidate result)
        {
            if (candidates != null)
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (candidates[index].PawnId != pawnId || !candidates[index].IsPossessed || !candidates[index].IsAlive) continue;
                    result = candidates[index];
                    return true;
                }
            }

            result = default;
            return false;
        }
    }

    public sealed class UnityEnemyPerceptionQuery : IEnemyPerceptionQuery
    {
        private readonly LayerMask obstacleMask;

        public UnityEnemyPerceptionQuery(LayerMask obstacleMask)
        {
            this.obstacleMask = obstacleMask;
        }

        public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate)
        {
            Vector3 direction = candidate.Position - origin;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= 0.001f) return true;
            RaycastHit[] hits = Physics.RaycastAll(
                origin + Vector3.up,
                direction / distance,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return true;
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            if (candidate.Root == null) return false;
            for (int index = 0; index < hits.Length; index++)
            {
                Transform hitTransform = hits[index].transform;
                if (hitTransform == candidate.Root || hitTransform.IsChildOf(candidate.Root)) return true;
                if (hitTransform.GetComponentInParent<DedicatedEnemyEntity>() != null) continue;
                if (hitTransform.GetComponentInParent<CharacterPhysicsMotor>() != null) continue;
                return false;
            }

            return true;
        }
    }
}
