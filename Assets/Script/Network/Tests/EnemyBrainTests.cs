using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyBrainTests
    {
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
