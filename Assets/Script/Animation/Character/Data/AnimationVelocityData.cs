using UnityEngine;

namespace CGame.Animation
{
    public sealed class AnimationVelocityData
    {
        public Vector3 WorldVelocity { get; private set; }
        public Vector3 LocalVelocity { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public float VerticalVelocity { get; private set; }
        public Vector2 NormalizedMoveDirection { get; private set; }

        internal void Update(Vector3 worldVelocity, Quaternion worldRotation)
        {
            WorldVelocity = worldVelocity;
            LocalVelocity = Quaternion.Inverse(worldRotation) * worldVelocity;
            Vector2 horizontalVelocity = new Vector2(LocalVelocity.x, LocalVelocity.z);
            HorizontalSpeed = horizontalVelocity.magnitude;
            VerticalVelocity = worldVelocity.y;
            NormalizedMoveDirection = horizontalVelocity.sqrMagnitude > 0.0001f
                ? horizontalVelocity.normalized
                : Vector2.zero;
        }

        internal void Reset()
        {
            WorldVelocity = Vector3.zero;
            LocalVelocity = Vector3.zero;
            HorizontalSpeed = 0f;
            VerticalVelocity = 0f;
            NormalizedMoveDirection = Vector2.zero;
        }
    }
}
