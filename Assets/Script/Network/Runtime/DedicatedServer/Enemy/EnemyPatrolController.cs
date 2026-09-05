using System;
using UnityEngine;

namespace CGame.Network
{
    public sealed class EnemyPatrolController
    {
        private readonly PatrolRouteDefinition route;

        public EnemyPatrolController(PatrolRouteDefinition route)
        {
            this.route = route ?? throw new ArgumentNullException(nameof(route));
            route.ValidateForLevel(route.LevelId);
        }

        public int PointIndex { get; private set; }
        public string RouteId => route.RouteId;
        public Vector3 CurrentTarget => route.WorldPoints[PointIndex];

        public bool AdvanceIfArrived(Vector3 currentPosition)
        {
            if (!IsAtCurrentTarget(currentPosition)) return false;
            PointIndex = (PointIndex + 1) % route.WorldPoints.Count;
            return true;
        }

        public bool IsAtCurrentTarget(Vector3 currentPosition)
        {
            Vector3 offset = CurrentTarget - currentPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= route.ArrivalRadius * route.ArrivalRadius;
        }

        public void SetNearestPoint(Vector3 currentPosition)
        {
            int nearestIndex = 0;
            float nearestDistanceSquared = float.MaxValue;
            for (int index = 0; index < route.WorldPoints.Count; index++)
            {
                Vector3 offset = route.WorldPoints[index] - currentPosition;
                offset.y = 0f;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared) continue;
                nearestDistanceSquared = distanceSquared;
                nearestIndex = index;
            }
            PointIndex = nearestIndex;
        }
    }
}
