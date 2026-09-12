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
        ReturnToCover,
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
            bool hasFireRequest = false) => new EnemyBrainOutput(movementIntent, state, targetPawnId,
                hasFireRequest && !movementIntent.NavigationIntent.HasPath &&
                (state == EnemyBrainState.Fire || state == EnemyBrainState.PeekFire));
    }

    public sealed class EnemyBrain
    {
        private readonly EnemyPatrolController patrol;
        private readonly EnemyNavPathComponent navigation;
        private readonly EnemyCombatMovement combatMovement;
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
        private const long CoverReselectionCooldownTicks = 30;
        private EnemyBrainState state;
        private long targetPawnId;
        private Vector3 lastKnownTargetPosition;
        private Transform targetRoot;
        private long lastSeenTargetTick;
        private EnemyCoverSelection coverSelection;
        private long nextCoverTransitionTick;
        private long nextCoverSelectionTick;
        private long peekFireConfirmedTick = -1;
        private int peekConfirmedShots;
        private bool UsesStationaryCover => coverSelection.IsValid &&
            (coverSelection.PeekPosition - coverSelection.CoverPosition).sqrMagnitude <= 0.0001f;
        private long CoverHoldTicks => UsesStationaryCover ? 60 : 20;
        private long aimStartedTick = -1;
        private const long AimPreparationTicks = 12;
        private const long ShotRecoveryTicks = 12;
        private long peekArrivalTick = -1;
        private EnemyCoverValidationFailure lastCoverValidationFailure;
        private long coverFailureStartedTick = -1;
        private const long CoverFailureGraceTicks = 15;
        private const long CoverTravelTimeoutTicks = 1200;
        private long coverTravelStartedTick;
        private long lastTick;

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
            combatMovement = new EnemyCombatMovement(navigationQuery, definition.FireDefinition.EngagementRange, enemyId, perception);
            engagementRange = definition.FireDefinition.EngagementRange;
            this.coverSelector = coverSelector;
            this.enemyId = enemyId;
        }

        public string RouteId => patrol.RouteId;
        public int PointIndex => patrol.PointIndex;
        public EnemyBrainState State => state;
        public long TargetPawnId => targetPawnId;
        public string CoverPointId => coverSelection.CoverPointId ?? string.Empty;
        public EnemyCoverValidationFailure LastCoverValidationFailure => lastCoverValidationFailure;
        public string LastCoverExitReason { get; private set; } = string.Empty;
        public string LastExitedCoverPointId { get; private set; } = string.Empty;
        public long LastCoverExitTick { get; private set; } = -1;
        // Derive diagnostics from the actual commitment instead of maintaining a second FSM.
        public string TacticalIntent => coverSelection.IsValid ? "UseCover" :
            state == EnemyBrainState.Fire ? "StationaryFire" :
            state == EnemyBrainState.Chase && lastSeenTargetTick == lastTick ? "RepositionForFire" : "None";
        public Vector3? FiringDestination => combatMovement.HasDestination ? combatMovement.Destination : (Vector3?)null;
        public string LastCombatExitReason => combatMovement.LastExitReason;
        public long LastCombatExitTick => combatMovement.LastExitTick;

        public EnemyBrainOutput TickPatrol(long serverTick, DedicatedEnemyMotorState motorState)
        {
            return Tick(serverTick, motorState, Array.Empty<EnemyPerceptionCandidate>());
        }

        public EnemyBrainOutput Tick(
            long serverTick,
            DedicatedEnemyMotorState motorState,
            IReadOnlyList<EnemyPerceptionCandidate> candidates)
        {
            long previousTarget = targetPawnId;
            EnemyBrainState previousState = state;
            EnemyBrainOutput output = EvaluateTactics(serverTick, motorState, candidates);
            if (!output.HasFireRequest)
            {
                aimStartedTick = -1;
                return output;
            }
            // The tactical output has already checked arrival, real velocity,
            // grounding, facing and sight. All must remain valid during raising.
            if (aimStartedTick < 0 || previousTarget != targetPawnId || previousState != state)
                aimStartedTick = serverTick;
            return serverTick - aimStartedTick >= AimPreparationTicks ? output :
                EnemyBrainOutput.ForMovement(output.MovementIntent, output.State, output.TargetPawnId);
        }

        private EnemyBrainOutput EvaluateTactics(
            long serverTick,
            DedicatedEnemyMotorState motorState,
            IReadOnlyList<EnemyPerceptionCandidate> candidates)
        {
            if (serverTick < 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            lastTick = serverTick;
            lastCoverValidationFailure = EnemyCoverValidationFailure.None;
            if (serverTick <= InitialPatrolTicks)
            {
                state = EnemyBrainState.Patrol;
                targetPawnId = 0;
                patrol.AdvanceIfArrived(motorState.Position);
                return BuildMovement(serverTick, motorState.Position, patrol.CurrentTarget, EnemyMoveTargetKind.PatrolRoutePoint);
            }
            if (coverSelection.IsValid && !coverSelector.HasReservation(enemyId, coverSelection))
                return FallBackFromInvalidCover(serverTick, motorState.Position, lastKnownTargetPosition,
                    EnemyCoverValidationFailure.ReservationLost);
            if (coverSelection.IsValid &&
                TryGetCandidate(targetPawnId, candidates, out EnemyPerceptionCandidate retainedTarget) &&
                serverTick - lastSeenTargetTick <= CoverEngagementGraceTicks)
            {
                // A stopped, raised weapon sees over low cover; concealed or
                // travelling enemies must still use their normal sight query.
                bool seesFromFiringPose = state == EnemyBrainState.PeekFire && IsStopped(motorState) &&
                    IsAt(motorState.Position, coverSelection.PeekPosition, coverSelection.ReservationRadius) &&
                    coverSelector.HasFiringLineOfSight(motorState.Position, retainedTarget);
                if (seesFromFiringPose || perception.HasLineOfSight(motorState.Position, retainedTarget))
                {
                    lastKnownTargetPosition = retainedTarget.Position;
                    lastSeenTargetTick = serverTick;
                }
                var observedTarget = new EnemyPerceptionCandidate(retainedTarget.PawnId,
                    lastKnownTargetPosition, true, true, retainedTarget.Root);
                targetRoot = retainedTarget.Root;
                return TickCover(serverTick, motorState, observedTarget);
            }
            if (coverSelection.IsValid)
            {
                if (serverTick - lastSeenTargetTick <= LostSightGraceTicks)
                    return BuildStationaryFacing(motorState.Position, lastKnownTargetPosition, state);
                ClearCoverReservation("TargetLost");
                nextCoverSelectionTick = serverTick + CoverReselectionCooldownTicks;
            }
            if (TrySelectVisibleCandidate(motorState.Position, candidates, out EnemyPerceptionCandidate target))
            {
                if (coverSelection.IsValid && targetPawnId != 0 && targetPawnId != target.PawnId)
                    ClearCoverReservation();
                if (targetPawnId != 0 && targetPawnId != target.PawnId)
                {
                    combatMovement.Reset();
                    state = EnemyBrainState.Chase;
                }
                targetPawnId = target.PawnId;
                targetRoot = target.Root;
                lastKnownTargetPosition = target.Position;
                lastSeenTargetTick = serverTick;
                if (coverSelection.IsValid)
                    return TickCover(serverTick, motorState, target);

                if (combatMovement.HasDestination && !combatMovement.IsAtDestination(motorState.Position))
                {
                    state = EnemyBrainState.Chase;
                    return BuildCombatMovement(serverTick, motorState.Position, target.Position);
                }

                bool canChooseCover = state != EnemyBrainState.Fire && !combatMovement.HasDestination &&
                    coverSelector != null && serverTick >= nextCoverSelectionTick;
                EnemyCoverSelection selectedCover = !canChooseCover
                    ? default
                    : coverSelector.SelectAndReserve(enemyId, motorState.Position, target, engagementRange);
                if (canChooseCover)
                    nextCoverSelectionTick = serverTick + CoverReselectionCooldownTicks;
                if (selectedCover.IsValid)
                {
                    coverSelection = selectedCover;
                    combatMovement.Reset();
                    state = EnemyBrainState.TakeCover;
                    coverTravelStartedTick = serverTick;
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                }

                Vector3 planarDelta = target.Position - motorState.Position;
                planarDelta.y = 0f;
                if (planarDelta.sqrMagnitude <= engagementRange * engagementRange &&
                    planarDelta.sqrMagnitude >= engagementRange * engagementRange * 0.09f)
                {
                    state = EnemyBrainState.Fire;
                    combatMovement.Reset();
                    return BuildStationaryFacing(motorState.Position, target.Position, state,
                        CanFireFromRest(motorState, target.Position));
                }
                state = EnemyBrainState.Chase;
                return BuildCombatMovement(serverTick, motorState.Position, target.Position);
            }

            if (state == EnemyBrainState.Chase || state == EnemyBrainState.Fire || state == EnemyBrainState.TakeCover ||
                state == EnemyBrainState.CoverHold || state == EnemyBrainState.PeekFire || state == EnemyBrainState.ReturnToCover)
            {
                if (serverTick - lastSeenTargetTick <= LostSightGraceTicks)
                {
                    combatMovement.Reset();
                    state = EnemyBrainState.Chase;
                    return BuildMovement(serverTick, motorState.Position, lastKnownTargetPosition, EnemyMoveTargetKind.ChaseTarget);
                }

                ClearCoverReservation();
                combatMovement.Reset();
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

        public void NotifyFireConfirmed()
        {
            if (state != EnemyBrainState.PeekFire || peekFireConfirmedTick == lastTick) return;
            peekFireConfirmedTick = lastTick;
            peekConfirmedShots++;
        }

        private EnemyBrainOutput BuildStationaryFacing(
            Vector3 origin, Vector3 target, EnemyBrainState outputState, bool hasFireRequest = false)
        {
            Vector3 direction = Vector3.ProjectOnPlane(target - origin, Vector3.up);
            EnemyNavigationIntent facing = direction.sqrMagnitude > 0.0001f
                ? EnemyNavigationIntent.FaceTarget(Quaternion.LookRotation(direction, Vector3.up))
                : default;
            return EnemyBrainOutput.ForMovement(
                new EnemyMovementIntent(facing, EnemyMoveTargetKind.ChaseTarget),
                outputState, targetPawnId, hasFireRequest);
        }

        private EnemyBrainOutput TickCover(long serverTick, DedicatedEnemyMotorState motorState, EnemyPerceptionCandidate target)
        {
            if (!coverSelection.IsValid)
            {
                state = EnemyBrainState.Chase;
                return BuildMovement(serverTick, motorState.Position, target.Position, EnemyMoveTargetKind.ChaseTarget);
            }

            Vector3 destination = state == EnemyBrainState.PeekFire
                ? coverSelection.PeekPosition
                : coverSelection.CoverPosition;
            if (state != EnemyBrainState.CoverHold &&
                (state != EnemyBrainState.PeekFire || peekArrivalTick < 0) &&
                serverTick - coverTravelStartedTick >= CoverTravelTimeoutTicks)
                return FallBackFromInvalidCover(serverTick, motorState.Position, target.Position,
                    EnemyCoverValidationFailure.TravelTimeout);
            EnemyCoverValidationResult validation = coverSelector.ValidateSelection(
                enemyId, coverSelection, motorState.Position, target, destination);
            if (!validation.IsValid)
                return HandleCoverFailure(serverTick, motorState.Position, target.Position, validation.Failure);

            if (state == EnemyBrainState.TakeCover)
            {
                if (!IsAt(motorState.Position, coverSelection.CoverPosition, coverSelection.ReservationRadius))
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                if (!IsStopped(motorState))
                    return BuildStationaryFacing(motorState.Position, target.Position, state);
                coverFailureStartedTick = -1;
                state = EnemyBrainState.CoverHold;
                nextCoverTransitionTick = serverTick + CoverHoldTicks;
                return BuildStationaryFacing(motorState.Position, target.Position, state);
            }
            if (state == EnemyBrainState.CoverHold)
            {
                if (!IsAt(motorState.Position, coverSelection.CoverPosition, coverSelection.ReservationRadius))
                {
                    state = EnemyBrainState.TakeCover;
                    coverTravelStartedTick = serverTick;
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                }
                if (!IsStopped(motorState))
                {
                    nextCoverTransitionTick = serverTick + CoverHoldTicks;
                    return BuildStationaryFacing(motorState.Position, target.Position, state);
                }
                if (serverTick < nextCoverTransitionTick)
                {
                    coverFailureStartedTick = -1;
                    return BuildStationaryFacing(motorState.Position, target.Position, state);
                }
                state = EnemyBrainState.PeekFire;
                coverTravelStartedTick = serverTick;
                peekArrivalTick = -1;
                return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.PeekPosition, EnemyMoveTargetKind.PeekPosition);
            }
            if (state == EnemyBrainState.PeekFire)
            {
                if (!IsAt(motorState.Position, coverSelection.PeekPosition, coverSelection.ReservationRadius))
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.PeekPosition, EnemyMoveTargetKind.PeekPosition);
                coverFailureStartedTick = -1;
                if (peekArrivalTick < 0) peekArrivalTick = serverTick;
                bool burstComplete = peekConfirmedShots >= (UsesStationaryCover ? 3 : 1);
                if (burstComplete && serverTick - peekFireConfirmedTick < ShotRecoveryTicks)
                    return BuildStationaryFacing(motorState.Position, target.Position, state);
                if (burstComplete || serverTick - peekArrivalTick >= (UsesStationaryCover ? 360 : 150))
                {
                    state = EnemyBrainState.ReturnToCover;
                    coverTravelStartedTick = serverTick;
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                }
                return BuildStationaryFacing(motorState.Position, target.Position,
                    EnemyBrainState.PeekFire, CanFireFromRest(motorState, target.Position));
            }
            if (state == EnemyBrainState.ReturnToCover)
            {
                if (!IsAt(motorState.Position, coverSelection.CoverPosition, coverSelection.ReservationRadius))
                    return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
                if (!IsStopped(motorState))
                    return BuildStationaryFacing(motorState.Position, target.Position, state);
                coverFailureStartedTick = -1;
                state = EnemyBrainState.CoverHold;
                peekFireConfirmedTick = -1;
                peekConfirmedShots = 0;
                nextCoverTransitionTick = serverTick + CoverHoldTicks;
                return BuildStationaryFacing(motorState.Position, target.Position, state);
            }

            state = EnemyBrainState.TakeCover;
            coverTravelStartedTick = serverTick;
            return BuildCoverMovement(serverTick, motorState.Position, target.Position, coverSelection.CoverPosition, EnemyMoveTargetKind.CoverPosition);
        }

        private void ClearCoverReservation(string reason = "Released")
        {
            if (coverSelection.IsValid)
            {
                LastCoverExitReason = reason;
                LastExitedCoverPointId = coverSelection.CoverPointId;
                LastCoverExitTick = lastTick;
                coverSelector?.ReleaseByEnemy(enemyId);
            }
            coverSelection = default;
            peekFireConfirmedTick = -1;
            peekConfirmedShots = 0;
            peekArrivalTick = -1;
            coverFailureStartedTick = -1;
        }

        private EnemyBrainOutput HandleCoverFailure(long tick, Vector3 origin, Vector3 target,
            EnemyCoverValidationFailure failure)
        {
            lastCoverValidationFailure = failure;
            if (coverFailureStartedTick < 0) coverFailureStartedTick = tick;
            if (failure == EnemyCoverValidationFailure.ReservationLost ||
                tick - coverFailureStartedTick >= CoverFailureGraceTicks)
                return FallBackFromInvalidCover(tick, origin, target, failure);
            return BuildStationaryFacing(origin, target, state);
        }

        private EnemyBrainOutput FallBackFromInvalidCover(
            long serverTick,
            Vector3 origin,
            Vector3 targetPosition,
            EnemyCoverValidationFailure failure)
        {
            lastCoverValidationFailure = failure;
            ClearCoverReservation(failure.ToString());
            peekFireConfirmedTick = -1;
            nextCoverSelectionTick = serverTick + CoverReselectionCooldownTicks;
            state = EnemyBrainState.Chase;
            combatMovement.Reset();
            return BuildCombatMovement(serverTick, origin, targetPosition);
        }

        private EnemyBrainOutput BuildCombatMovement(long tick, Vector3 origin, Vector3 target)
        {
            EnemyNavigationIntent intent = combatMovement.BuildIntent(origin,
                new EnemyPerceptionCandidate(targetPawnId, target, true, true, targetRoot), tick);
            return EnemyBrainOutput.ForMovement(new EnemyMovementIntent(intent, EnemyMoveTargetKind.ChaseTarget),
                state, targetPawnId);
        }

        private static bool CanFireFromRest(DedicatedEnemyMotorState motorState, Vector3 target)
        {
            if (!IsStopped(motorState)) return false;
            Vector3 direction = Vector3.ProjectOnPlane(target - motorState.Position, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(motorState.Rotation * Vector3.forward, Vector3.up);
            return direction.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f &&
                Vector3.Dot(forward.normalized, direction.normalized) >= 0.99f;
        }

        private static bool IsStopped(DedicatedEnemyMotorState motorState) =>
            motorState.IsGrounded && motorState.PlanarVelocity.sqrMagnitude <= 0.0025f;

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
            // Low cover changes crouch/aim pose at one protected location.
            // An already reached destination must not create a movement path.
            if (IsAt(origin, coverTarget, coverSelection.ReservationRadius))
                return BuildStationaryFacing(origin, fallbackTarget, state);
            EnemyBrainOutput coverOutput = BuildMovement(serverTick, origin, coverTarget, targetKind);
            if (coverOutput.MovementIntent.NavigationIntent.HasPath)
            {
                coverFailureStartedTick = -1;
                EnemyNavigationIntent path = coverOutput.MovementIntent.NavigationIntent;
                return EnemyBrainOutput.ForMovement(new EnemyMovementIntent(path, targetKind), state, targetPawnId);
            }

            // Retry during the cover grace window, not after the general navigation backoff expires.
            if (!coverOutput.MovementIntent.NavigationIntent.IsRetryThrottled)
                navigation.InvalidatePathRetry(serverTick + 5);
            return HandleCoverFailure(
                serverTick,
                origin,
                fallbackTarget,
                EnemyCoverValidationFailure.DestinationPathMissing);
        }

        private bool TrySelectVisibleCandidate(
            Vector3 origin,
            IReadOnlyList<EnemyPerceptionCandidate> candidates,
            out EnemyPerceptionCandidate selected)
        {
            selected = default;
            bool found = false;
            float selectedDistanceSquared = 0f;
            float perceptionRange = Mathf.Max(PerceptionRange, engagementRange + 4f);
            float rangeSquared = perceptionRange * perceptionRange;
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
        private readonly float originHeight;
        private readonly float targetHeight;

        public UnityEnemyPerceptionQuery(LayerMask obstacleMask, float originHeight = 1f, float targetHeight = 1f)
        {
            this.obstacleMask = obstacleMask;
            if (float.IsNaN(originHeight) || float.IsInfinity(originHeight) || originHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(originHeight));
            if (float.IsNaN(targetHeight) || float.IsInfinity(targetHeight) || targetHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(targetHeight));
            this.originHeight = originHeight;
            this.targetHeight = targetHeight;
        }

        public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate)
        {
            Vector3 rayOrigin = origin + Vector3.up * originHeight;
            Vector3 direction = candidate.Position + Vector3.up * targetHeight - rayOrigin;
            float distance = direction.magnitude;
            if (distance <= 0.001f) return true;
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
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
