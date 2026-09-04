using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public enum EnemyMoveTargetKind
    {
        PatrolRoutePoint,
        ChaseTarget,
        ReturnRoutePoint
    }

    public enum EnemyBrainState
    {
        Patrol,
        Chase,
        ReturnToRoute,
        Fire,
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
        private EnemyBrainOutput(EnemyMovementIntent movementIntent, EnemyBrainState state, long targetPawnId)
        {
            MovementIntent = movementIntent;
            State = state;
            TargetPawnId = targetPawnId;
            HasMovementIntent = true;
        }

        public EnemyMovementIntent MovementIntent { get; }
        public EnemyBrainState State { get; }
        public long TargetPawnId { get; }
        public bool HasMovementIntent { get; }
        public bool HasFireRequest => State == EnemyBrainState.Fire;

        public static EnemyBrainOutput ForMovement(
            EnemyMovementIntent movementIntent,
            EnemyBrainState state,
            long targetPawnId) => new EnemyBrainOutput(movementIntent, state, targetPawnId);
    }

    public sealed class EnemyBrain
    {
        private readonly EnemyPatrolController patrol;
        private readonly EnemyNavPathComponent navigation;
        private readonly IEnemyPerceptionQuery perception;
        private readonly float engagementRange;
        // Patrol routes deliberately carry enemies away from their spawn point.
        // Keep perception wide enough to reacquire the possessed pawn after the
        // initial patrol window; firing remains constrained by engagementRange.
        private const float PerceptionRange = 12f;
        // The three enemies spawn close to the possessed player in SampleScene.
        // Give their replicated presentations time to spawn and visibly traverse
        // their authored routes before perception may transition into combat.
        private const long InitialPatrolTicks = 180;
        private const long LostSightGraceTicks = 120;
        private EnemyBrainState state;
        private long targetPawnId;
        private Vector3 lastKnownTargetPosition;
        private long lastSeenTargetTick;

        public EnemyBrain(
            EnemyArchetypeCombatDefinition definition,
            IEnemyNavPathQuery navigationQuery,
            IEnemyPerceptionQuery perceptionQuery = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            patrol = new EnemyPatrolController(definition.PatrolRoute);
            navigation = new EnemyNavPathComponent(navigationQuery, retryIntervalTicks: 30);
            perception = perceptionQuery ?? new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers);
            engagementRange = definition.FireDefinition.EngagementRange;
        }

        public string RouteId => patrol.RouteId;
        public int PointIndex => patrol.PointIndex;
        public EnemyBrainState State => state;
        public long TargetPawnId => targetPawnId;

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
            if (TrySelectVisibleCandidate(motorState.Position, candidates, out EnemyPerceptionCandidate target))
            {
                targetPawnId = target.PawnId;
                lastKnownTargetPosition = target.Position;
                lastSeenTargetTick = serverTick;
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

            if (state == EnemyBrainState.Chase || state == EnemyBrainState.Fire)
            {
                if (serverTick - lastSeenTargetTick <= LostSightGraceTicks)
                    return BuildMovement(serverTick, motorState.Position, lastKnownTargetPosition, EnemyMoveTargetKind.ChaseTarget);

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
            Transform hitTransform = hits[0].transform;
            return hitTransform == candidate.Root || hitTransform.IsChildOf(candidate.Root);
        }
    }
}
