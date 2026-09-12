using System;
using UnityEngine;

namespace CGame.Network
{
    /// <summary>Owns one firing destination and navigation retry, never actor transforms or firing permission.</summary>
    public sealed class EnemyCombatMovement
    {
        private readonly IEnemyNavPathQuery query;
        private readonly IEnemyPerceptionQuery perception;
        private readonly EnemyNavPathComponent navigation;
        private readonly float range;
        private readonly int side;
        private Vector3 destination;
        private long moveDeadline;
        private long pauseUntil;
        private bool hasDestination;
        private long failureStartedTick = -1;

        public EnemyCombatMovement(IEnemyNavPathQuery query, float engagementRange, long enemyId,
            IEnemyPerceptionQuery perception = null)
        {
            this.query = query ?? throw new ArgumentNullException(nameof(query));
            this.perception = perception;
            range = engagementRange;
            navigation = new EnemyNavPathComponent(query, 15);
            side = enemyId % 2 == 0 ? 1 : -1;
        }

        public void Reset()
        {
            hasDestination = false;
            pauseUntil = 0;
            failureStartedTick = -1;
        }

        public bool HasDestination => hasDestination;
        public Vector3 Destination => destination;
        public string LastExitReason { get; private set; } = string.Empty;
        public long LastExitTick { get; private set; } = -1;
        public bool IsAtDestination(Vector3 origin) => hasDestination &&
            Vector3.ProjectOnPlane(destination - origin, Vector3.up).sqrMagnitude <= 0.16f;

        public EnemyNavigationIntent BuildIntent(Vector3 origin, Vector3 target, long tick)
            => BuildIntent(origin, new EnemyPerceptionCandidate(1, target, true, true), tick);

        public EnemyNavigationIntent BuildIntent(Vector3 origin, EnemyPerceptionCandidate candidate, long tick)
        {
            Vector3 target = candidate.Position;
            Vector3 offset = Vector3.ProjectOnPlane(target - origin, Vector3.up);
            float distance = offset.magnitude;
            Vector3 toward = distance > 0.001f ? offset / distance : Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(toward, Vector3.up);
            float destinationRange = Vector3.ProjectOnPlane(target - destination, Vector3.up).magnitude;
            if (hasDestination && (destinationRange > range || destinationRange < range * 0.3f))
                Reset();
            if (hasDestination && perception != null && !perception.HasLineOfSight(destination, candidate))
                return WaitForRecovery(tick, facing, "DestinationOccluded");
            if (IsAtDestination(origin))
            {
                failureStartedTick = -1;
                return EnemyNavigationIntent.FaceTarget(facing);
            }
            if (hasDestination && tick >= moveDeadline)
            {
                ReleaseDestination(tick, "TravelTimeout");
                return EnemyNavigationIntent.FaceTarget(facing);
            }
            if (!hasDestination)
            {
                if (tick < pauseUntil) return EnemyNavigationIntent.FaceTarget(facing);
                if (distance >= range * 0.3f && distance <= range)
                    return EnemyNavigationIntent.FaceTarget(facing);
                float preferredDistance = range * 0.7f;
                Vector3 lateral = Vector3.Cross(Vector3.up, toward);
                for (int attempt = 0; attempt < 9; attempt++)
                {
                    Vector3 wanted;
                    if (attempt < 3)
                    {
                        float lateralOffset = attempt == 0 ? 0f : (attempt == 1 ? side : -side) * Mathf.Min(2.5f, range * 0.25f);
                        wanted = target - toward * preferredDistance + lateral * lateralOffset;
                    }
                    else
                    {
                        float angle = ((attempt - 3) / 2 + 1) * 30f * (attempt % 2 == 1 ? side : -side);
                        wanted = target - Quaternion.Euler(0f, angle, 0f) * toward * preferredDistance;
                    }
                    // The target may stand on stairs while the firing ring is on
                    // the arena floor. Sample both elevations, but still require
                    // a nearby planar point, real LOS and a complete walkable path.
                    for (int elevation = 0; elevation < 2; elevation++)
                    {
                        wanted.y = elevation == 0 ? origin.y : target.y;
                        if (query.TrySample(wanted, out Vector3 sampled) &&
                            Vector3.ProjectOnPlane(sampled - wanted, Vector3.up).magnitude < 0.8f &&
                            Vector3.ProjectOnPlane(sampled - origin, Vector3.up).sqrMagnitude > 0.25f &&
                            (perception == null || perception.HasLineOfSight(sampled, candidate)) &&
                            query.TryCalculateCompletePath(origin, sampled, out _))
                        {
                            destination = sampled;
                            moveDeadline = tick + 1200;
                            hasDestination = true;
                            navigation.InvalidatePathRetry(tick);
                            break;
                        }
                    }
                    if (hasDestination) break;
                }
                if (!hasDestination)
                {
                    pauseUntil = tick + 30;
                    return EnemyNavigationIntent.FaceTarget(facing);
                }
            }
            EnemyNavigationIntent path = navigation.BuildIntent(origin, destination, tick);
            if (!path.HasPath)
                return WaitForRecovery(tick, facing, "PathUnavailable");
            failureStartedTick = -1;
            return new EnemyNavigationIntent(path.DesiredWorldDirection, path.HasPath ? path.DesiredFacing : facing, path.HasPath,
                path.IsRetryThrottled, hasFacing: true);
        }

        private EnemyNavigationIntent WaitForRecovery(long tick, Quaternion facing, string reason)
        {
            if (failureStartedTick < 0) failureStartedTick = tick;
            if (tick - failureStartedTick >= 60) ReleaseDestination(tick, reason);
            return EnemyNavigationIntent.FaceTarget(facing);
        }

        private void ReleaseDestination(long tick, string reason)
        {
            hasDestination = false;
            failureStartedTick = -1;
            pauseUntil = tick + 30;
            LastExitReason = reason;
            LastExitTick = tick;
        }
    }
}
