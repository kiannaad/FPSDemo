using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedEnemyPatrolAgent
    {
        // Matches the standing aim clips (rifle/AK ~1.45m, pistol ~1.52m).
        // Keep the ray at the root horizontally so it cannot start through a wall.
        private const float muzzleHeight = 1.45f;
        private readonly DedicatedEnemyEntity entity;
        private readonly EnemyBrain brain;
        private readonly DedicatedEnemyMotorSimulation motor;
        private readonly DedicatedEnemyFireResolver fireResolver;
        private readonly UnityEnemyPerceptionQuery firingPerception;
        private EnemyBrainOutput pendingOutput;
        private int stationaryTicks;
        private bool isNoAmmo;

        public DedicatedEnemyPatrolAgent(
            DedicatedEnemyEntity entity,
            EnemyArchetypeCombatDefinition definition,
            IReadOnlyList<CoverPointDefinition> coverPoints = null,
            ICoverReservationRegistry coverReservations = null)
        {
            this.entity = entity ?? throw new ArgumentNullException(nameof(entity));
            if (entity.MotorPawn == null) throw new InvalidOperationException("Dedicated patrol requires a motor Pawn.");
            var navigation = new UnityEnemyNavPathQuery();
            var perception = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers, 1.6f, includeUpperTarget: true);
            firingPerception = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers, muzzleHeight,
                includeUpperTarget: true);
            EnemyCoverSelector selector = coverPoints == null || coverReservations == null
                ? null
                : new EnemyCoverSelector(coverPoints, navigation,
                    new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers, 1f, includeUpperTarget: true), coverReservations,
                    firingPerception);
            brain = new EnemyBrain(definition, navigation, perception, selector, entity.EnemyId);
            motor = new DedicatedEnemyMotorSimulation(entity.MotorPawn);
            fireResolver = new DedicatedEnemyFireResolver(definition.FireDefinition);
        }

        public long EnemyId => entity.EnemyId;
        public string RouteId => brain.RouteId;
        public int PointIndex => brain.PointIndex;
        public EnemyBrainState State => isNoAmmo ? EnemyBrainState.NoAmmo : brain.State;
        public long TargetPawnId => brain.TargetPawnId;
        public int MagazineAmmo => fireResolver.MagazineAmmo;
        public string CoverPointId => brain.CoverPointId;
        public EnemyCoverValidationFailure LastCoverValidationFailure => brain.LastCoverValidationFailure;
        public bool WantsToFire => !isNoAmmo && pendingOutput.HasFireRequest;

        public void PrepareFixedStep(long serverTick, IReadOnlyList<EnemyPerceptionCandidate> candidates)
        {
            DedicatedEnemyMotorState state = motor.Capture();
            EnemyBrainState previousState = brain.State;
            string previousCover = brain.CoverPointId;
            long previousExitTick = brain.LastCoverExitTick;
            string previousTactic = brain.TacticalIntent;
            Vector3? previousDestination = brain.FiringDestination;
            long previousCombatExitTick = brain.LastCombatExitTick;
            pendingOutput = isNoAmmo
                ? EnemyBrainOutput.ForMovement(default, EnemyBrainState.NoAmmo, TargetPawnId)
                : brain.Tick(serverTick, state, candidates);
            if (previousState != brain.State || previousCover != brain.CoverPointId || previousExitTick != brain.LastCoverExitTick)
                Debug.Log($"[EnemyCover075] tick={serverTick} enemy={EnemyId} from={previousState} to={brain.State} cover={brain.CoverPointId} previousCover={previousCover} pos={state.Position:F3} speed={state.PlanarVelocity.magnitude:F4} grounded={state.IsGrounded} exitTick={brain.LastCoverExitTick} exitReason={brain.LastCoverExitReason}");
            if (previousTactic != brain.TacticalIntent || previousDestination != brain.FiringDestination ||
                previousCombatExitTick != brain.LastCombatExitTick)
                Debug.Log($"[EnemyTactic076] tick={serverTick} enemy={EnemyId} tactic={brain.TacticalIntent} state={brain.State} target={brain.TargetPawnId} destination={brain.FiringDestination?.ToString("F3") ?? "none"} pos={state.Position:F3} speed={state.PlanarVelocity.magnitude:F4} fireRequest={pendingOutput.HasFireRequest} exitTick={brain.LastCombatExitTick} exitReason={brain.LastCombatExitReason}");
            motor.ApplyIntent(pendingOutput.MovementIntent.NavigationIntent);
        }

        public DedicatedEnemyMotorState CompleteFixedStep(long serverTick)
        {
            DedicatedEnemyMotorState state = motor.Capture();
            entity.ApplyMotorState(state);
            if (pendingOutput.MovementIntent.NavigationIntent.HasPath && state.PlanarVelocity.sqrMagnitude < 0.0004f)
            {
                stationaryTicks++;
                if (stationaryTicks >= 30)
                {
                    brain.InvalidatePathRetry(serverTick);
                    stationaryTicks = 0;
                }
            }
            else
            {
                stationaryTicks = 0;
            }

            motor.ClearIntent();
            return state;
        }

        public EnemyFireResolution TryResolveFire(
            long serverTick,
            EnemyPerceptionCandidate target,
            IEnemyHitscanQuery hitscanQuery)
        {
            if (!WantsToFire || target.PawnId != TargetPawnId) return default;
            DedicatedEnemyMotorState state = motor.Capture();
            Vector3 muzzleOrigin = state.Position + Vector3.up * muzzleHeight;
            if (!firingPerception.TryGetVisibleTargetPoint(state.Position, target, out Vector3 aimPoint)) return default;
            EnemyFireResolution result = fireResolver.TryResolve(
                serverTick,
                muzzleOrigin,
                state.Rotation * Vector3.forward,
                aimPoint,
                target.PawnId,
                hitscanQuery);
            if (result.Fired)
            {
                brain.NotifyFireConfirmed();
                Debug.Log($"[EnemyFire074] tick={serverTick} enemy={EnemyId} state={State} speed={state.PlanarVelocity.magnitude:F4} grounded={state.IsGrounded} cover={CoverPointId}");
            }
            return result;
        }

        public void MarkNoAmmo()
        {
            isNoAmmo = true;
            brain.ReleaseCover();
        }

        public void ReleaseCover() => brain.ReleaseCover();
    }
}
