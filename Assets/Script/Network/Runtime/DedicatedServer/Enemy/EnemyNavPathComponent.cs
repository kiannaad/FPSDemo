using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    public interface IEnemyNavPathQuery
    {
        bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint);
        bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners);
    }

    public readonly struct EnemyNavigationIntent
    {
        public EnemyNavigationIntent(
            Vector3 desiredWorldDirection,
            Quaternion desiredFacing,
            bool hasPath,
            bool isRetryThrottled)
        {
            DesiredWorldDirection = desiredWorldDirection;
            DesiredFacing = desiredFacing;
            HasPath = hasPath;
            IsRetryThrottled = isRetryThrottled;
        }

        public Vector3 DesiredWorldDirection { get; }
        public Quaternion DesiredFacing { get; }
        public bool HasPath { get; }
        public bool IsRetryThrottled { get; }

        public static EnemyNavigationIntent NoPath(bool isRetryThrottled) =>
            new EnemyNavigationIntent(Vector3.zero, Quaternion.identity, false, isRetryThrottled);
    }

    public sealed class EnemyNavPathComponent
    {
        private readonly IEnemyNavPathQuery query;
        private readonly long retryIntervalTicks;
        private long nextPathQueryTick;

        public EnemyNavPathComponent(IEnemyNavPathQuery query, long retryIntervalTicks)
        {
            this.query = query ?? throw new ArgumentNullException(nameof(query));
            if (retryIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(retryIntervalTicks));
            this.retryIntervalTicks = retryIntervalTicks;
        }

        public void ValidateRoute(PatrolRouteDefinition route, string levelId)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            route.ValidateForLevel(levelId);
            for (int index = 0; index < route.WorldPoints.Count; index++)
            {
                if (!query.TrySample(route.WorldPoints[index], out _))
                    throw new InvalidOperationException($"Patrol route point cannot be sampled: {index}.");
            }
        }

        public EnemyNavigationIntent BuildIntent(Vector3 currentPosition, Vector3 targetPosition, long currentTick)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (currentTick < nextPathQueryTick) return EnemyNavigationIntent.NoPath(true);
            if (!query.TryCalculateCompletePath(currentPosition, targetPosition, out IReadOnlyList<Vector3> corners) ||
                !TryGetNextCorner(currentPosition, corners, out Vector3 nextCorner))
            {
                nextPathQueryTick = checked(currentTick + retryIntervalTicks);
                return EnemyNavigationIntent.NoPath(false);
            }

            nextPathQueryTick = currentTick;
            Vector3 desiredDirection = nextCorner - currentPosition;
            desiredDirection.y = 0f;
            desiredDirection.Normalize();
            return new EnemyNavigationIntent(
                desiredDirection,
                Quaternion.LookRotation(desiredDirection, Vector3.up),
                true,
                false);
        }

        public void InvalidatePathRetry(long currentTick)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            nextPathQueryTick = currentTick;
        }

        private static bool TryGetNextCorner(Vector3 currentPosition, IReadOnlyList<Vector3> corners, out Vector3 nextCorner)
        {
            if (corners != null)
            {
                for (int index = 0; index < corners.Count; index++)
                {
                    Vector3 offset = corners[index] - currentPosition;
                    offset.y = 0f;
                    if (offset.sqrMagnitude <= 0.0001f) continue;
                    nextCorner = corners[index];
                    return true;
                }
            }

            nextCorner = default;
            return false;
        }
    }

    public sealed class UnityEnemyNavPathQuery : IEnemyNavPathQuery
    {
        public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
        {
            // V1 deliberately has no NavMesh.  Patrol routes are authored as
            // world-space points and movement is resolved by the authoritative
            // CharacterPhysics motor, so sampling is an identity operation.
            sampledPoint = worldPoint;
            return true;
        }

        public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
        {
            Vector3 planarOffset = destination - origin;
            planarOffset.y = 0f;
            if (planarOffset.sqrMagnitude <= 0.0001f)
            {
                corners = Array.Empty<Vector3>();
                return false;
            }

            corners = new[] { origin, destination };
            return true;
        }
    }
}
