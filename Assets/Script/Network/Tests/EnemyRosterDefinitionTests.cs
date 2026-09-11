using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyRosterDefinitionTests
    {
        [Test]
        public void EnemyNavPath_SourceOnlyProducesNavigationIntent()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "Script/Network/Runtime/DedicatedServer/Enemy/EnemyNavPathComponent.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Not.Contain("transform"));
            Assert.That(source, Does.Not.Contain("CharacterPhysicsMotor"));
            Assert.That(source, Does.Not.Contain("ApplyState"));
            Assert.That(source, Does.Not.Contain("Animator"));
        }

        [Test]
        public void DedicatedServerRuntime_FormalRosterDoesNotUseStepChase()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "Script/Network/Runtime/DedicatedServer/DedicatedServerRuntime.cs");

            Assert.That(File.ReadAllText(sourcePath), Does.Not.Contain("StepChase"));
        }

        [Test]
        public void EnemyMotorSimulation_MapsNavigationIntentToPawnControlIntent()
        {
            var root = new GameObject("EnemyMotorSimulationTest");
            root.AddComponent<CapsuleCollider>();
            root.AddComponent<CharacterPhysicsMotor>();
            var pawn = new Pawn(root, System.Array.Empty<ActorComponent>());
            try
            {
                var simulation = new DedicatedEnemyMotorSimulation(pawn);

                simulation.ApplyIntent(new EnemyNavigationIntent(
                    Vector3.right,
                    Quaternion.LookRotation(Vector3.right, Vector3.up),
                    hasPath: true,
                    isRetryThrottled: false));

                Assert.That(pawn.PeekingMovementInput(), Is.EqualTo(Vector3.forward));
                Assert.That(Vector3.Angle(pawn.ControlRotation * Vector3.forward, Vector3.right), Is.LessThan(0.1f));
                Assert.That(simulation.Capture().PlanarVelocity, Is.EqualTo(Vector3.zero));
                simulation.ApplyIntent(EnemyNavigationIntent.FaceTarget(Quaternion.LookRotation(Vector3.left)));
                Assert.That(pawn.PeekingMovementInput(), Is.EqualTo(Vector3.zero));
                Assert.That(Vector3.Angle(pawn.ControlRotation * Vector3.forward, Vector3.left), Is.LessThan(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnemyNavPath_RejectsRouteWithUnsampleableWorldPoint()
        {
            PatrolRouteDefinition route = CreateRoute();
            try
            {
                var query = new RecordingNavigationQuery { SampleSucceeds = false };
                var navigation = new EnemyNavPathComponent(query, retryIntervalTicks: 4);

                Assert.Throws<System.InvalidOperationException>(() => navigation.ValidateRoute(route, "SampleScene"));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void EnemyNavPath_UsesFirstPathCornerAndReturnsZeroForIncompletePath()
        {
            var query = new RecordingNavigationQuery
            {
                PathCorners = new[] { Vector3.zero, new Vector3(3f, 0f, 0f) }
            };
            var navigation = new EnemyNavPathComponent(query, retryIntervalTicks: 4);

            EnemyNavigationIntent complete = navigation.BuildIntent(Vector3.zero, new Vector3(10f, 0f, 0f), 7);

            Assert.That(complete.HasPath, Is.True);
            Assert.That(complete.DesiredWorldDirection, Is.EqualTo(Vector3.right));
            Assert.That(Vector3.Dot(complete.DesiredFacing * Vector3.forward, Vector3.right), Is.GreaterThan(0.999f));

            query.PathSucceeds = false;
            EnemyNavigationIntent incomplete = navigation.BuildIntent(Vector3.zero, new Vector3(10f, 0f, 0f), 8);

            Assert.That(incomplete.HasPath, Is.False);
            Assert.That(incomplete.DesiredWorldDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void UnityEnemyNavPathQuery_UsesCompleteUnityNavMeshQueries()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "Script/Network/Runtime/DedicatedServer/Enemy/EnemyNavPathComponent.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("NavMesh.SamplePosition"));
            Assert.That(source, Does.Contain("NavMesh.CalculatePath"));
            Assert.That(source, Does.Contain("NavMeshPathStatus.PathComplete"));
            Assert.That(source, Does.Not.Contain("V1 deliberately has no NavMesh"));
        }

        [Test]
        public void CoverPointCatalog_RejectsDuplicateIdsAndMismatchedLevels()
        {
            CoverPointDefinition first = CreateCoverPoint("Cover.A", "SampleScene");
            CoverPointDefinition duplicate = CreateCoverPoint("Cover.A", "SampleScene");
            CoverPointDefinition wrongLevel = CreateCoverPoint("Cover.B", "OtherScene");
            CoverPointCatalog catalog = ScriptableObject.CreateInstance<CoverPointCatalog>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => catalog.Configure(first, duplicate));
                catalog.Configure(first, wrongLevel);
                Assert.Throws<System.InvalidOperationException>(() => catalog.ValidateForLevel("SampleScene", new RecordingNavigationQuery()));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(duplicate);
                Object.DestroyImmediate(wrongLevel);
            }
        }

        [Test]
        public void CoverPointDefinition_RejectsUnsampleablePositionsAndIncompletePath()
        {
            CoverPointDefinition point = CreateCoverPoint("Cover.A", "SampleScene");
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => point.ValidateStatic(
                    new RecordingNavigationQuery { SampleSucceeds = false }));
                Assert.Throws<System.InvalidOperationException>(() => point.ValidateStatic(
                    new RecordingNavigationQuery { PathSucceeds = false }));
            }
            finally
            {
                Object.DestroyImmediate(point);
            }
        }

        [Test]
        public void EnemyNavPath_ThrottlesRepeatedPathFailuresUntilRetryTick()
        {
            var query = new RecordingNavigationQuery { PathSucceeds = false };
            var navigation = new EnemyNavPathComponent(query, retryIntervalTicks: 4);

            EnemyNavigationIntent first = navigation.BuildIntent(Vector3.zero, Vector3.right, 10);
            EnemyNavigationIntent throttled = navigation.BuildIntent(Vector3.zero, Vector3.right, 11);
            EnemyNavigationIntent retried = navigation.BuildIntent(Vector3.zero, Vector3.right, 14);

            Assert.That(first.IsRetryThrottled, Is.False);
            Assert.That(throttled.IsRetryThrottled, Is.True);
            Assert.That(retried.IsRetryThrottled, Is.False);
            Assert.That(query.PathQueryCount, Is.EqualTo(2));
        }

        [Test]
        public void PatrolRoute_AdvancesAtArrivalRadiusAndWrapsToFirstPoint()
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            try
            {
                route.Configure(
                    "Rifle.Route.A",
                    "SampleScene",
                    new[] { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f) },
                    0.5f);
                var patrol = new EnemyPatrolController(route);

                Assert.That(patrol.CurrentTarget, Is.EqualTo(new Vector3(0f, 0f, 0f)));
                Assert.That(patrol.AdvanceIfArrived(new Vector3(0.4f, 0f, 0f)), Is.True);
                Assert.That(patrol.PointIndex, Is.EqualTo(1));
                Assert.That(patrol.AdvanceIfArrived(new Vector3(10f, 0f, 0f)), Is.True);
                Assert.That(patrol.PointIndex, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void PatrolRoute_RejectsInsufficientPointsNonPositiveRadiusAndMismatchedLevel()
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => route.Configure(
                    "Rifle.Route.A",
                    "SampleScene",
                    new[] { Vector3.zero },
                    0.5f));
                Assert.Throws<System.InvalidOperationException>(() => route.Configure(
                    "Rifle.Route.A",
                    "SampleScene",
                    new[] { Vector3.zero, Vector3.right },
                    0f));

                route.Configure(
                    "Rifle.Route.A",
                    "SampleScene",
                    new[] { Vector3.zero, Vector3.right },
                    0.5f);

                Assert.Throws<System.InvalidOperationException>(() => route.ValidateForLevel("OtherScene"));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void Configure_RequiresExactlyThreeUniqueStableEnemyIdsAndSpawnPoints()
        {
            EnemyRosterDefinition definition = ScriptableObject.CreateInstance<EnemyRosterDefinition>();
            try
            {
                definition.Configure(
                    new EnemyRosterEntry(501, "Enemy.Pistol", "EnemyPoint 1"),
                    new EnemyRosterEntry(502, "Enemy.Rifle", "EnemyPoint 2"),
                    new EnemyRosterEntry(503, "Enemy.Ak", "EnemyPoint 3"));

                Assert.That(definition.Entries.Count, Is.EqualTo(3));
                Assert.That(definition.Entries[2].ArchetypeId, Is.EqualTo("Enemy.Ak"));
                Assert.Throws<System.InvalidOperationException>(() => definition.Configure(
                    new EnemyRosterEntry(501, "Enemy.Pistol", "EnemyPoint 1"),
                    new EnemyRosterEntry(501, "Enemy.Rifle", "EnemyPoint 2"),
                    new EnemyRosterEntry(503, "Enemy.Ak", "EnemyPoint 3")));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static PatrolRouteDefinition CreateRoute()
        {
            PatrolRouteDefinition route = ScriptableObject.CreateInstance<PatrolRouteDefinition>();
            route.Configure(
                "Rifle.Route.A",
                "SampleScene",
                new[] { Vector3.zero, Vector3.right },
                0.5f);
            return route;
        }

        private static CoverPointDefinition CreateCoverPoint(string coverPointId, string levelId)
        {
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure(coverPointId, levelId, Vector3.zero, Vector3.right, 0.75f);
            return point;
        }

        private sealed class RecordingNavigationQuery : IEnemyNavPathQuery
        {
            public bool SampleSucceeds { get; set; } = true;
            public bool PathSucceeds { get; set; } = true;
            public Vector3[] PathCorners { get; set; } = new[] { Vector3.zero, Vector3.right };
            public int PathQueryCount { get; private set; }

            public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
            {
                sampledPoint = worldPoint;
                return SampleSucceeds;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                PathQueryCount++;
                corners = PathCorners;
                return PathSucceeds;
            }
        }
    }
}
