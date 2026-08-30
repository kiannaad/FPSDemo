using UnityEngine;

namespace CGame
{
    public readonly struct PawnShotContext
    {
        public PawnShotContext(Vector3 forward, Vector3 right, Vector3 up, bool isAiming, bool isGrounded, float horizontalSpeed)
        {
            Forward = forward;
            Right = right;
            Up = up;
            IsAiming = isAiming;
            IsGrounded = isGrounded;
            HorizontalSpeed = horizontalSpeed;
        }

        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public Vector3 Up { get; }
        public bool IsAiming { get; }
        public bool IsGrounded { get; }
        public float HorizontalSpeed { get; }
    }
}
