using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyCombatMovementTests
    {
        [Test]
        public void Reposition_FacesTravelAndHoldsFiringDestinationAfterArrival()
        {
            var query = new PathQuery();
            var movement = new EnemyCombatMovement(query, 10f, 101);
            var target = Vector3.forward * 14f;
            var intent = movement.BuildIntent(Vector3.zero, target, 200);
            Vector3 destination = query.LastDestination;
            Assert.That(intent.HasPath, Is.True);
            Assert.That(intent.DesiredWorldDirection.z, Is.GreaterThan(0.9f));
            Assert.That(Vector3.Angle(intent.DesiredFacing * Vector3.forward, intent.DesiredWorldDirection), Is.LessThan(0.01f));
            movement.BuildIntent(Vector3.right * -0.2f, target, 220);
            Assert.That(query.LastDestination, Is.EqualTo(destination));
            Assert.That(movement.BuildIntent(destination, target, 240).HasPath, Is.False);
            Assert.That(movement.BuildIntent(destination, target, 265).HasPath, Is.False,
                "Arrival is a stable firing position, not a short pause before another lateral move.");
        }

        [Test]
        public void ClosePressure_Retreats_AndBlockedPathsNeverProduceMotion()
        {
            var query = new PathQuery();
            var movement = new EnemyCombatMovement(query, 10f, 102);
            var intent = movement.BuildIntent(Vector3.zero, Vector3.forward, 200);
            Assert.That(intent.DesiredWorldDirection.z, Is.LessThan(-0.5f));
            Assert.That(Vector3.Angle(intent.DesiredFacing * Vector3.forward, intent.DesiredWorldDirection), Is.LessThan(0.01f));
            query.Available = false;
            movement.Reset();
            intent = movement.BuildIntent(Vector3.zero, Vector3.forward, 201);
            Assert.That(intent.HasPath, Is.False);
            Assert.That(intent.HasFacing, Is.True);
        }

        [Test]
        public void RealArenaNavMesh_ArrivedPointIsACompletePath()
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(
                "Assets/Scenes/TacticalArenaNavMesh.asset");
            var instance = UnityEngine.AI.NavMesh.AddNavMeshData(data);
            try
            {
                var query = new UnityEnemyNavPathQuery();
                Assert.That(query.TrySample(new Vector3(0, 0, 3), out Vector3 point), Is.True);
                Assert.That(query.TryCalculateCompletePath(point, point, out var corners), Is.True);
                Assert.That(corners.Count, Is.GreaterThanOrEqualTo(1));
            }
            finally { instance.Remove(); }
        }

        [Test]
        public void Reposition_KeepsSmallTargetChangesButReplansOutsideFiringRange()
        {
            var query = new PathQuery();
            var movement = new EnemyCombatMovement(query, 10f, 101);
            movement.BuildIntent(Vector3.zero, Vector3.forward * 14f, 200);
            Vector3 committed = movement.Destination;
            movement.BuildIntent(Vector3.forward, Vector3.forward * 14.5f, 201);
            Assert.That(movement.Destination, Is.EqualTo(committed));
            movement.BuildIntent(Vector3.forward, Vector3.forward * 25f, 202);
            Assert.That(movement.Destination, Is.Not.EqualTo(committed));
            Assert.That(Vector3.Distance(movement.Destination, Vector3.forward * 25f), Is.InRange(3f, 10f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Reposition_PathFailureRecoversOrReleasesWithBackoff(bool recovers)
        {
            var query = new PathQuery();
            var movement = new EnemyCombatMovement(query, 10f, 101);
            var target = Vector3.forward * 14f;
            movement.BuildIntent(Vector3.zero, target, 200);
            Vector3 committed = movement.Destination;
            query.Available = false;
            Assert.That(movement.BuildIntent(Vector3.zero, target, 201).HasPath, Is.False);
            Assert.That(movement.HasDestination, Is.True);
            query.Available = recovers;
            if (recovers)
            {
                Assert.That(movement.BuildIntent(Vector3.zero, target, 216).HasPath, Is.True);
                Assert.That(movement.Destination, Is.EqualTo(committed));
                return;
            }
            for (long tick = 202; tick <= 261; tick++)
                Assert.That(movement.BuildIntent(Vector3.zero, target, tick).HasPath, Is.False);
            Assert.That(movement.HasDestination, Is.False, "Persistent failure cannot hold a firing position indefinitely.");
            query.Available = true;
            Assert.That(movement.BuildIntent(Vector3.zero, target, 290).HasPath, Is.False);
            Assert.That(movement.BuildIntent(Vector3.zero, target, 291).HasPath, Is.True);
        }

        private sealed class PathQuery : IEnemyNavPathQuery
        {
            public bool Available = true;
            public Vector3 LastDestination;
            public bool TrySample(Vector3 point, out Vector3 sampled) { sampled = point; return Available; }
            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                LastDestination = destination;
                corners = new[] { origin, destination };
                return Available;
            }
        }

        [Test]
        public void Reposition_DestinationOcclusionWaitsThenRecoversOrReleases()
        {
            var perception = new MutablePerception();
            var movement = new EnemyCombatMovement(new PathQuery(), 10f, 101, perception);
            var target = Vector3.forward * 14f;
            movement.BuildIntent(Vector3.zero, target, 200);
            Vector3 committed = movement.Destination;
            perception.Visible = false;
            Assert.That(movement.BuildIntent(Vector3.zero, target, 201).HasPath, Is.False);
            Assert.That(movement.HasDestination, Is.True);
            perception.Visible = true;
            Assert.That(movement.BuildIntent(Vector3.zero, target, 216).HasPath, Is.True);
            Assert.That(movement.Destination, Is.EqualTo(committed));
            perception.Visible = false;
            for (long tick = 217; tick <= 277; tick++)
                movement.BuildIntent(Vector3.zero, target, tick);
            Assert.That(movement.HasDestination, Is.False);
            Assert.That(movement.LastExitReason, Is.EqualTo("DestinationOccluded"));
        }

        private sealed class MutablePerception : IEnemyPerceptionQuery
        {
            public bool Visible = true;
            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) => Visible;
        }

        [TestCase(0f, 2.2f)]
        [TestCase(2.2f, 0f)]
        public void Reposition_ElevationDifferenceUsesReachableGroundAndWideFiringFlank(float originHeight, float targetHeight)
        {
            var query = new GroundPathQuery();
            var movement = new EnemyCombatMovement(query, 18f, 102, new FlankPerception());
            var origin = new Vector3(0f, originHeight, 0f);
            var target = new Vector3(0f, targetHeight, 22f);

            EnemyNavigationIntent intent = movement.BuildIntent(origin, target, 200);

            Assert.That(intent.HasPath, Is.True, "An elevated target must not strand the enemy in Chase when a reachable firing flank exists.");
            Assert.That(movement.HasDestination, Is.True);
            Assert.That(movement.Destination.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(Mathf.Abs(movement.Destination.x), Is.GreaterThan(5f));
            Assert.That(Vector3.ProjectOnPlane(target - movement.Destination, Vector3.up).magnitude, Is.InRange(5.4f, 18f));
            Assert.That(Vector3.Angle(intent.DesiredFacing * Vector3.forward, intent.DesiredWorldDirection), Is.LessThan(.01f));
            Vector3 committed = movement.Destination;
            Assert.That(movement.BuildIntent(committed, target, 260).HasPath, Is.False);
            Assert.That(movement.Destination, Is.EqualTo(committed), "The extra search must still commit to one destination.");
        }

        [Test]
        public void Reposition_WideFlankStillRejectsIncompletePaths()
        {
            var query = new GroundPathQuery { Connected = false };
            var movement = new EnemyCombatMovement(query, 18f, 102, new FlankPerception());
            Assert.That(movement.BuildIntent(Vector3.zero, new Vector3(0f, 2.2f, 22f), 200).HasPath, Is.False);
            Assert.That(movement.HasDestination, Is.False);
        }

        private sealed class GroundPathQuery : IEnemyNavPathQuery
        {
            public bool Connected = true;
            public float HorizontalOffset;
            public bool TrySample(Vector3 point, out Vector3 sampled)
            {
                sampled = new Vector3(point.x + HorizontalOffset, 0f, point.z);
                return Mathf.Abs(point.y) <= 2f;
            }
            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                corners = new[] { origin, destination };
                return Connected;
            }
        }

        private sealed class FlankPerception : IEnemyPerceptionQuery
        {
            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) => Mathf.Abs(origin.x) > 5f;
        }

        [Test]
        public void Reposition_WideSearchRejectsDistantPlanarNavMeshSnaps()
        {
            var movement = new EnemyCombatMovement(new GroundPathQuery { HorizontalOffset = 2f }, 18f, 102, new FlankPerception());
            Assert.That(movement.BuildIntent(Vector3.zero, new Vector3(0f, 2.2f, 22f), 200).HasPath, Is.False);
            Assert.That(movement.HasDestination, Is.False);
        }
    }
}
