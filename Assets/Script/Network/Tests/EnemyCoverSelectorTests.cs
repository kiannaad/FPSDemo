using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyCoverSelectorTests
    {
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
    }
}
