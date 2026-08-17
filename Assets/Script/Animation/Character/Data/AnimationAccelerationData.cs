using UnityEngine;

namespace CGame.Animation
{
    public sealed class AnimationAccelerationData
    {
        private Vector3 previousWorldVelocity;
        private bool hasHistory;

        public Vector3 WorldAcceleration { get; private set; }
        public Vector3 LocalAcceleration { get; private set; }

        internal void Update(Vector3 worldVelocity, Quaternion worldRotation, float deltaTime)
        {
            if (!hasHistory)
            {
                Reset(worldVelocity);
                return;
            }

            WorldAcceleration = deltaTime > 0f
                ? (worldVelocity - previousWorldVelocity) / deltaTime
                : Vector3.zero;
            LocalAcceleration = Quaternion.Inverse(worldRotation) * WorldAcceleration;
            previousWorldVelocity = worldVelocity;
        }

        internal void Reset(Vector3 worldVelocity)
        {
            previousWorldVelocity = worldVelocity;
            WorldAcceleration = Vector3.zero;
            LocalAcceleration = Vector3.zero;
            hasHistory = true;
        }
    }
}
