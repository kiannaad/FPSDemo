using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyCoverSelectorTests
    {
        [Test]
        public void SelectAndReserve_WideCoverUsesProtectedCornerInsteadOfLongOutAndBackTrip()
        {
            Vector3 origin = new Vector3(1000f, 0f, 1000f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cover = ScriptableObject.CreateInstance<CoverPointDefinition>();
            try
            {
                wall.transform.position = origin + new Vector3(0f, 1f, 1f);
                wall.transform.localScale = new Vector3(2.4f, 2f, .3f);
                Physics.SyncTransforms();
                cover.Configure("Cover.Wide", "SampleScene", origin, origin + Vector3.right * 2.15f, .3f);
                var body = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers);
                var muzzle = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers, 1.45f);
                var selector = new EnemyCoverSelector(new[] { cover }, new CompletePathQuery(), body,
                    new CoverReservationRegistry(), muzzle);
                var target = new EnemyPerceptionCandidate(100, origin + Vector3.forward * 10f, true, true);
                var selection = selector.SelectAndReserve(101, origin, target, 18f);
                Assert.That(selection.IsValid, Is.True);
                Assert.That(body.HasLineOfSight(selection.CoverPosition, target), Is.False);
                Assert.That(muzzle.HasLineOfSight(selection.PeekPosition, target), Is.True);
                Assert.That(Vector3.Distance(selection.CoverPosition, selection.PeekPosition), Is.LessThanOrEqualTo(1f),
                    "Establish a protected position at the usable corner before peeking, not a long shuttle from the wall centre.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(cover);
            }
        }

        [Test]
        public void SelectAndReserve_StopsAtFirstClearFiringPositionInsteadOfFarAuthoredPeek()
        {
            Vector3 origin = new Vector3(1000f, 0f, 1000f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cover = ScriptableObject.CreateInstance<CoverPointDefinition>();
            try
            {
                wall.transform.position = origin + new Vector3(0f, 1f, 1f);
                wall.transform.localScale = new Vector3(0.8f, 2f, 0.3f);
                Physics.SyncTransforms();
                cover.Configure("Cover.Edge", "SampleScene", origin, origin + Vector3.right * 2.15f, .1f);
                var sight = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers, 1.45f);
                var selector = new EnemyCoverSelector(new[] { cover }, new CompletePathQuery(),
                    new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers), new CoverReservationRegistry(), sight);
                var target = new EnemyPerceptionCandidate(100, origin + Vector3.forward * 10f, true, true);
                EnemyCoverSelection selection = selector.SelectAndReserve(101, origin, target, 18f);
                Assert.That(selection.IsValid, Is.True);
                Assert.That(sight.HasLineOfSight(selection.PeekPosition, target), Is.True);
                Assert.That(Vector3.Distance(selection.CoverPosition, selection.PeekPosition), Is.LessThan(.8f),
                    "The near edge already provides a shot; do not run two metres out into the open.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(cover);
            }
        }

        [Test]
        public void SelectAndReserve_RejectsNearbyCoverWithLongDetour()
        {
            var cover = CreatePoint("Cover.Detour", 2f, 3f);
            try
            {
                var registry = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(new[] { cover }, new DetourPathQuery(),
                    new CoverVisibilityQuery(), registry);
                Assert.That(selector.SelectAndReserve(101, Vector3.zero,
                    new EnemyPerceptionCandidate(100, Vector3.zero, true, true)).IsValid, Is.False);
                Assert.That(registry.IsReservedBy(cover.CoverPointId, 101), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(cover); }
        }

        private sealed class DetourPathQuery : IEnemyNavPathQuery
        {
            public bool TrySample(Vector3 point, out Vector3 sampledPoint)
            {
                sampledPoint = point;
                return true;
            }

            public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
            {
                corners = new[] { origin, origin + Vector3.forward * 5f, destination + Vector3.forward * 5f, destination };
                return true;
            }
        }

        [TestCase(1.2f, true)]
        [TestCase(2f, false)]
        public void RealCoverGeometry_HidesBodyButOnlyLowCoverAllowsMuzzleSight(float height, bool muzzleVisible)
        {
            Vector3 origin = new Vector3(1000f, 0f, 1000f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = origin + new Vector3(0f, height * .5f, 1f);
                wall.transform.localScale = new Vector3(4f, height, .3f);
                Physics.SyncTransforms();
                var target = new EnemyPerceptionCandidate(100, origin + Vector3.forward * 10f, true, true);
                var hiddenSight = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers);
                var muzzleSight = new UnityEnemyPerceptionQuery(Physics.DefaultRaycastLayers, 1.45f);
                Assert.That(hiddenSight.HasLineOfSight(origin, target), Is.False);
                Assert.That(muzzleSight.HasLineOfSight(origin, target), Is.EqualTo(muzzleVisible),
                    "A clear muzzle ray must pass over low cover and still be blocked by a high wall.");
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
        }

        [TestCase(true, 2f)]
        [TestCase(false, 3f)]
        public void SelectAndReserve_PrefersProtectedSamePositionWhenMuzzleCanFire(bool muzzleClearsCover, float expectedPeekX)
        {
            CoverPointDefinition cover = CreatePoint("Cover.A", 2f, 3f);
            try
            {
                var firing = new MutableCoverVisibilityQuery { IsCoverVisible = muzzleClearsCover };
                var selector = new EnemyCoverSelector(new[] { cover }, new CompletePathQuery(),
                    new CoverVisibilityQuery(), new CoverReservationRegistry(), firing);
                var target = new EnemyPerceptionCandidate(100, Vector3.zero, true, true);
                var selection = selector.SelectAndReserve(101, Vector3.zero, target);
                Assert.That(selection.IsValid, Is.True);
                Assert.That(selection.PeekPosition.x, Is.EqualTo(expectedPeekX),
                    "A protected low-cover position with a clear muzzle needs no trip to the outside peek point.");
                Assert.That(selector.ValidateSelection(101, selection, selection.CoverPosition,
                    target, selection.PeekPosition).IsValid, Is.True);
                if (muzzleClearsCover)
                {
                    firing.IsCoverVisible = false;
                    Assert.That(selector.ValidateSelection(101, selection, selection.CoverPosition,
                        target, selection.PeekPosition).Failure, Is.EqualTo(EnemyCoverValidationFailure.PeekOccluded));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cover); }
        }

        [TestCase(-6f, true)]
        [TestCase(-6.1f, false)]
        public void SelectAndReserve_OnlyApproachesNearbyCover(float originX, bool expected)
        {
            CoverPointDefinition cover = CreatePoint("Cover.A", 2f, 3f);
            try
            {
                var registry = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(new[] { cover }, new CompletePathQuery(),
                    new CoverVisibilityQuery(), registry);
                Assert.That(selector.SelectAndReserve(101, Vector3.right * originX,
                    new EnemyPerceptionCandidate(100, Vector3.zero, true, true), 18f).IsValid, Is.EqualTo(expected),
                    "Do not cross the arena just because a distant cover point is technically reachable.");
                Assert.That(registry.IsReservedBy("Cover.A", 101), Is.EqualTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(cover); }
        }

        [Test]
        public void SelectAndReserve_RejectsVisiblePeekOutsideWeaponRange()
        {
            CoverPointDefinition cover = CreatePoint("Cover.A", 2f, 3f);
            try
            {
                var registry = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(new[] { cover }, new CompletePathQuery(),
                    new CoverVisibilityQuery(), registry);
                Assert.That(selector.SelectAndReserve(101, Vector3.zero,
                    new EnemyPerceptionCandidate(100, Vector3.zero, true, true), 2f).IsValid, Is.False);
                Assert.That(registry.IsReservedBy("Cover.A", 101), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(cover); }
        }

        [Test]
        public void SelectAndReserve_UsesShortestThenLexicographicVisiblePeekCandidate()
        {
            CoverPointDefinition coverA = CreatePoint("Cover.A", 2f, 3f);
            CoverPointDefinition coverB = CreatePoint("Cover.B", 2f, 4f);
            CoverPointDefinition invalid = CreatePoint("Cover.C", 8f, 9f);
            try
            {
                var registry = new CoverReservationRegistry();
                var selector = new EnemyCoverSelector(
                    new[] { coverB, invalid, coverA }, new CompletePathQuery(), new CoverVisibilityQuery(), registry);
                var target = new EnemyPerceptionCandidate(100, Vector3.zero, true, true);

                EnemyCoverSelection first = selector.SelectAndReserve(101, Vector3.zero, target);
                EnemyCoverSelection second = selector.SelectAndReserve(102, Vector3.zero, target);

                Assert.That(first.CoverPointId, Is.EqualTo("Cover.A"));
                Assert.That(second.CoverPointId, Is.EqualTo("Cover.B"));
                Assert.That(registry.IsReservedBy("Cover.A", 101), Is.True);
                Assert.That(registry.IsReservedBy("Cover.B", 102), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coverA);
                UnityEngine.Object.DestroyImmediate(coverB);
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }

        [Test]
        public void ValidateSelection_ReportsVisibleCoverAndKeepsReasonExplicit()
        {
            CoverPointDefinition cover = CreatePoint("Cover.A", 2f, 3f);
            try
            {
                var registry = new CoverReservationRegistry();
                var perception = new MutableCoverVisibilityQuery();
                var selector = new EnemyCoverSelector(new[] { cover }, new CompletePathQuery(), perception, registry);
                var target = new EnemyPerceptionCandidate(100, Vector3.zero, true, true);
                EnemyCoverSelection selection = selector.SelectAndReserve(101, Vector3.zero, target);
                perception.IsCoverVisible = true;

                EnemyCoverValidationResult result = selector.ValidateSelection(101, selection, Vector3.zero, target, selection.CoverPosition);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Failure, Is.EqualTo(EnemyCoverValidationFailure.CoverVisible));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cover);
            }
        }

        private static CoverPointDefinition CreatePoint(string id, float coverX, float peekX)
        {
            CoverPointDefinition point = ScriptableObject.CreateInstance<CoverPointDefinition>();
            point.Configure(id, "SampleScene", new Vector3(coverX, 0f, 0f), new Vector3(peekX, 0f, 0f), 0.75f);
            return point;
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

        private sealed class CoverVisibilityQuery : IEnemyPerceptionQuery
        {
            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate) =>
                Math.Abs(origin.x - 3f) < 0.01f || Math.Abs(origin.x - 4f) < 0.01f;
        }

        private sealed class MutableCoverVisibilityQuery : IEnemyPerceptionQuery
        {
            public bool IsCoverVisible { get; set; }

            public bool HasLineOfSight(Vector3 origin, EnemyPerceptionCandidate candidate)
            {
                if (Math.Abs(origin.x - 2f) < 0.01f) return IsCoverVisible;
                return Math.Abs(origin.x - 3f) < 0.01f;
            }
        }
    }
}
