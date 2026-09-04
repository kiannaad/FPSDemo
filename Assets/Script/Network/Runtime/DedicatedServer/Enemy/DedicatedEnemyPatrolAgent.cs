using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedEnemyPatrolAgent
    {
        private readonly DedicatedEnemyEntity entity;
        private readonly EnemyBrain brain;
        private readonly DedicatedEnemyMotorSimulation motor;
        private readonly DedicatedEnemyFireResolver fireResolver;
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
            var perception = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers);
            EnemyCoverSelector selector = coverPoints == null || coverReservations == null
                ? null
                : new EnemyCoverSelector(coverPoints, navigation, perception, coverReservations);
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
        public bool WantsToFire => !isNoAmmo && pendingOutput.HasFireRequest;

        public void PrepareFixedStep(long serverTick, IReadOnlyList<EnemyPerceptionCandidate> candidates)
        {
            DedicatedEnemyMotorState state = motor.Capture();
            pendingOutput = isNoAmmo
                ? EnemyBrainOutput.ForMovement(default, EnemyBrainState.NoAmmo, TargetPawnId)
                : brain.Tick(serverTick, state, candidates);
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
            Vector3 muzzleOrigin = state.Position + Vector3.up * 1.2f;
            Vector3 aimPoint = target.Position + Vector3.up;
            return fireResolver.TryResolve(
                serverTick,
                muzzleOrigin,
                state.Rotation * Vector3.forward,
                aimPoint,
                target.PawnId,
                hitscanQuery);
        }

        public void MarkNoAmmo()
        {
            isNoAmmo = true;
            brain.ReleaseCover();
        }

        public void ReleaseCover() => brain.ReleaseCover();
    }
}
