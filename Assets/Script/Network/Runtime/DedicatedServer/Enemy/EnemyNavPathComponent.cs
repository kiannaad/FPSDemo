using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
            bool isRetryThrottled,
            bool hasFacing = false)
        {
            DesiredWorldDirection = desiredWorldDirection;
            DesiredFacing = desiredFacing;
            HasPath = hasPath;
            IsRetryThrottled = isRetryThrottled;
            HasFacing = hasPath || hasFacing;
        }

        public Vector3 DesiredWorldDirection { get; }
        public Quaternion DesiredFacing { get; }
        public bool HasPath { get; }
        public bool IsRetryThrottled { get; }
        public bool HasFacing { get; }

        public static EnemyNavigationIntent FaceTarget(Quaternion facing) =>
            new EnemyNavigationIntent(Vector3.zero, facing, false, false, hasFacing: true);

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
        private const float SampleDistance = 2f;

        public bool TrySample(Vector3 worldPoint, out Vector3 sampledPoint)
        {
            if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, SampleDistance, NavMesh.AllAreas))
            {
                sampledPoint = Vector3.zero;
                return false;
            }

            sampledPoint = hit.position;
            return true;
        }

        public bool TryCalculateCompletePath(Vector3 origin, Vector3 destination, out IReadOnlyList<Vector3> corners)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete ||
                path.corners == null ||
                path.corners.Length < 2)
            {
                corners = Array.Empty<Vector3>();
                return false;
            }

            corners = path.corners;
            return true;
        }
    }
}
