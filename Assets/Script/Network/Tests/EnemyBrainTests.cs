using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyBrainTests
    {
        [Test]
        public void CoverBrain_TransitionsThroughTakeCoverHoldAndPeekFire()
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), 0.75f);
            try
            {
                var navigation = new CompletePathQuery();
                var perception = new CoverPerceptionQuery();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, new CoverReservationRegistry());
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, new Vector3(10f, 0f, 0f), true, true) };

                EnemyBrainOutput chase = brain.Tick(181, Motor(Vector3.zero), target);
                EnemyBrainOutput takeCover = brain.Tick(182, Motor(Vector3.zero), target);
                EnemyBrainOutput hold = brain.Tick(220, Motor(point.CoverPosition), target);
                EnemyBrainOutput peek = brain.Tick(241, Motor(point.CoverPosition), target);
                EnemyBrainOutput fire = brain.Tick(260, Motor(point.PeekPosition), target);

                Assert.That(chase.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(takeCover.State, Is.EqualTo(EnemyBrainState.TakeCover));
                Assert.That(hold.State, Is.EqualTo(EnemyBrainState.CoverHold));
                Assert.That(peek.State, Is.EqualTo(EnemyBrainState.PeekFire));
                Assert.That(fire.State, Is.EqualTo(EnemyBrainState.PeekFire));
                Assert.That(fire.HasFireRequest, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CoverBrain_PathFailure_ReleasesReservationAndFallsBackToChase()
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Cover", 0);
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure("Cover.A", "SampleScene", new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), 0.75f);
            try
            {
                var navigation = new FailsAfterFirstPathQuery();
                var perception = new CoverPerceptionQuery();
                var reservations = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(new[] { point }, navigation, perception, reservations);
                var brain = new EnemyBrain(definition, navigation, perception, selector, 101);
                var target = new[] { new EnemyPerceptionCandidate(100, new Vector3(10f, 0f, 0f), true, true) };

                brain.Tick(181, Motor(Vector3.zero), target);
                EnemyBrainOutput fallback = brain.Tick(182, Motor(Vector3.zero), target);

                Assert.That(fallback.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(fallback.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ChaseTarget));
                Assert.That(brain.CoverPointId, Is.Empty);
                Assert.That(reservations.IsReservedBy("Cover.A", 101), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        private static DedicatedEnemyMotorState Motor(Vector3 position) =>
            new DedicatedEnemyMotorState(position, Quaternion.identity, Vector3.zero, true);

        private sealed class CoverPerceptionQuery : IEnemyPerceptionQuery
        {
            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) => origin.x != 4f;
        }
        [Test]
        public void CombatCatalog_RequiresThreeUniqueArchetypeDefinitions()
        {
            EnemyArchetypeCombatCatalog catalog = ScriptableObject.CreateInstance<EnemyArchetypeCombatCatalog>();
            EnemyArchetypeCombatDefinition[] definitions = new EnemyArchetypeCombatDefinition[3];
            try
            {
                for (int index = 0; index < definitions.Length; index++)
                    definitions[index] = CreateDefinition($"Enemy.{index}", index);

                catalog.Configure(definitions);

                Assert.That(catalog.GetRequired("Enemy.1"), Is.SameAs(definitions[1]));
                Assert.Throws<System.InvalidOperationException>(() => catalog.Configure(definitions[0], definitions[0], definitions[2]));
            }
            finally
            {
                foreach (EnemyArchetypeCombatDefinition definition in definitions)
                {
                    if (definition == null) continue;
                    Object.DestroyImmediate(definition.PatrolRoute);
                    Object.DestroyImmediate(definition);
                }
                Object.DestroyImmediate(catalog);
            }
        }

        [TestCase("Enemy.Pistol")]
        [TestCase("Enemy.Rifle")]
        [TestCase("Enemy.Ak")]
        public void Patrol_ThreeArchetypesProduceMotorIntentAndNeverFire(string archetypeId)
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            EnemyArchetypeCombatDefinition definition = ScriptableObject.CreateInstance<EnemyArchetypeCombatDefinition>();
            try
            {
                route.Configure(
                    $"{archetypeId}.Route",
                    "SampleScene",
                    new[] { Vector3.zero, Vector3.right * 4f },
                    0.5f);
                definition.Configure(archetypeId, route);
                var brain = new EnemyBrain(definition, new CompletePathQuery());

                EnemyBrainOutput output = brain.TickPatrol(
                    30,
                    new DedicatedEnemyMotorState(Vector3.zero, Quaternion.identity, Vector3.zero, true));

                Assert.That(output.HasMovementIntent, Is.True);
                Assert.That(output.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.PatrolRoutePoint));
                Assert.That(output.HasFireRequest, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void Perception_SelectsNearestVisiblePawnThenReturnsToNearestRoutePointAfterGrace()
        {
            EnemyArchetypeCombatDefinition definition = CreateDefinition("Enemy.Rifle", 0);
            try
            {
                var brain = new EnemyBrain(definition, new CompletePathQuery(), new VisibleOnlyPerceptionQuery(2));
                var motorState = new DedicatedEnemyMotorState(
                    Vector3.zero, Quaternion.identity, Vector3.zero, true);
                var candidates = new[]
                {
                    new EnemyPerceptionCandidate(7, new Vector3(4f, 0f, 0f), true, true),
                    new EnemyPerceptionCandidate(2, new Vector3(4f, 0f, 0f), true, true),
                    new EnemyPerceptionCandidate(1, new Vector3(1f, 0f, 0f), false, true)
                };

                EnemyBrainOutput chase = brain.Tick(181, motorState, candidates);

                Assert.That(chase.State, Is.EqualTo(EnemyBrainState.Chase));
                Assert.That(chase.TargetPawnId, Is.EqualTo(2));
                Assert.That(chase.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ChaseTarget));

                EnemyBrainOutput grace = brain.Tick(182, motorState, System.Array.Empty<EnemyPerceptionCandidate>());
                Assert.That(grace.State, Is.EqualTo(EnemyBrainState.Chase));

                var returnMotorState = new DedicatedEnemyMotorState(
                    new Vector3(2.6f, 0f, 0f), Quaternion.identity, Vector3.zero, true);
                EnemyBrainOutput returning = brain.Tick(302, returnMotorState, System.Array.Empty<EnemyPerceptionCandidate>());
                Assert.That(returning.State, Is.EqualTo(EnemyBrainState.ReturnToRoute));
                Assert.That(returning.MovementIntent.TargetKind, Is.EqualTo(EnemyMoveTargetKind.ReturnRoutePoint));
                Assert.That(returning.TargetPawnId, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition.PatrolRoute);
                Object.DestroyImmediate(definition);
            }
        }

        private sealed class CompletePathQuery : IEnemyNavPathQuery
        {
            public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
            {
                sampledPoint = worldPoint;
                return true;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                corners = new[] { origin, destination };
                return true;
            }
        }

        private sealed class FailsAfterFirstPathQuery : IEnemyNavPathQuery
        {
            private int pathRequests;

            public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
            {
                sampledPoint = worldPoint;
                return true;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                pathRequests++;
                corners = pathRequests == 1 ? new[] { origin, destination } : System.Array.Empty<Vector3>();
                return pathRequests == 1;
            }
        }

        private sealed class VisibleOnlyPerceptionQuery : IEnemyPerceptionQuery
        {
            private readonly long visiblePawnId;

            public VisibleOnlyPerceptionQuery(long visiblePawnId)
            {
                this.visiblePawnId = visiblePawnId;
            }

            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) => candidate.PawnId == visiblePawnId;
        }

        private static EnemyArchetypeCombatDefinition CreateDefinition(string archetypeId, int index)
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            route.Configure($"{archetypeId}.Route", "SampleScene", new[]
            {
                new Vector3(index, 0f, 0f),
                new Vector3(index + 1f, 0f, 0f)
            }, 0.5f);
            EnemyArchetypeCombatDefinition definition = ScriptableObject.CreateInstance<EnemyArchetypeCombatDefinition>();
            definition.Configure(archetypeId, route);
            return definition;
        }
    }
}
