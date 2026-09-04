using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "PatrolRoute", menuName = "CGame/Network/Dedicated Patrol Route")]
    public sealed class PatrolRouteDefinition : ScriptableObject
    {
        [SerializeField] private string routeId;
        [SerializeField] private string levelId;
        [SerializeField] private Vector3[] worldPoints = Array.Empty<Vector3>();
        [SerializeField] private float arrivalRadius = 0.5f;

        public string RouteId => routeId;
        public string LevelId => levelId;
        public IReadOnlyList<Vector3> WorldPoints => worldPoints;
        public float ArrivalRadius => arrivalRadius;

        public void Configure(string routeId, string levelId, IReadOnlyList<Vector3> worldPoints, float arrivalRadius)
        {
            ValidateValues(routeId, levelId, worldPoints, arrivalRadius);
            this.routeId = routeId;
            this.levelId = levelId;
            this.arrivalRadius = arrivalRadius;
            this.worldPoints = new Vector3[worldPoints.Count];
            for (int index = 0; index < worldPoints.Count; index++) this.worldPoints[index] = worldPoints[index];
        }

        public void ValidateForLevel(string expectedLevelId)
        {
            ValidateValues(routeId, levelId, worldPoints, arrivalRadius);
            if (!string.Equals(levelId, expectedLevelId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Patrol route LevelId does not match: {levelId}.");
        }

        private static void ValidateValues(
            string routeId,
            string levelId,
            IReadOnlyList<Vector3> worldPoints,
            float arrivalRadius)
        {
            if (string.IsNullOrWhiteSpace(routeId)) throw new InvalidOperationException("Patrol route RouteId is required.");
            if (string.IsNullOrWhiteSpace(levelId)) throw new InvalidOperationException("Patrol route LevelId is required.");
            if (worldPoints == null || worldPoints.Count < 2)
                throw new InvalidOperationException("Patrol route requires at least two world points.");
            if (arrivalRadius <= 0f || float.IsNaN(arrivalRadius) || float.IsInfinity(arrivalRadius))
                throw new InvalidOperationException("Patrol route ArrivalRadius must be positive and finite.");
            for (int index = 0; index < worldPoints.Count; index++)
            {
                Vector3 point = worldPoints[index];
                if (!IsFinite(point)) throw new InvalidOperationException($"Patrol route point is not finite: {index}.");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
