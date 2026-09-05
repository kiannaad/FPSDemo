using System;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedRiflePatrolAgent
    {
        private readonly DedicatedEnemyEntity entity;
        private readonly EnemyPatrolController patrol;
        private readonly EnemyNavPathComponent navigation;
        private readonly DedicatedEnemyMotorSimulation motor;
        private EnemyNavigationIntent pendingIntent;
        private int stationaryTicks;

        public DedicatedRiflePatrolAgent(DedicatedEnemyEntity entity, PatrolRouteDefinition route)
        {
            this.entity = entity ?? throw new ArgumentNullException(nameof(entity));
            if (entity.MotorPawn == null) throw new InvalidOperationException("Rifle patrol requires a motor Pawn.");
            patrol = new EnemyPatrolController(route);
            navigation = new EnemyNavPathComponent(new UnityEnemyNavPathQuery(), retryIntervalTicks: 30);
            motor = new DedicatedEnemyMotorSimulation(entity.MotorPawn);
        }

        public string RouteId => patrol.RouteId;
        public long EnemyId => entity.EnemyId;
        public int PointIndex => patrol.PointIndex;

        public void PrepareFixedStep(long serverTick)
        {
            DedicatedEnemyMotorState state = motor.Capture();
            patrol.AdvanceIfArrived(state.Position);
            pendingIntent = navigation.BuildIntent(state.Position, patrol.CurrentTarget, serverTick);
            motor.ApplyIntent(pendingIntent);
        }

        public DedicatedEnemyMotorState CompleteFixedStep(long serverTick)
        {
            DedicatedEnemyMotorState state = motor.Capture();
            entity.ApplyMotorState(state);
            if (pendingIntent.HasPath && state.PlanarVelocity.sqrMagnitude < 0.0004f)
            {
                stationaryTicks++;
                if (stationaryTicks >= 30)
                {
                    navigation.InvalidatePathRetry(serverTick);
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
    }
}
