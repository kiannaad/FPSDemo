namespace CGame.Animation
{
    public sealed class AnimationCharacterStateData
    {
        internal const float MovingThreshold = 0.1f;
        internal const float SprintEnterThreshold = 4f;
        internal const float SprintExitThreshold = 3.2f;

        public bool IsGrounded { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsFalling { get; private set; }

        internal void Update(bool isGrounded, float horizontalSpeed, float verticalVelocity)
        {
            IsGrounded = isGrounded;
            // These thresholds classify animation presentation; they do not limit gameplay speed.
            IsMoving = horizontalSpeed > MovingThreshold;
            IsSprinting = isGrounded
                && (IsSprinting
                    ? horizontalSpeed > SprintExitThreshold
                    : horizontalSpeed > SprintEnterThreshold);
            IsJumping = !isGrounded && verticalVelocity > 0.01f;
            IsFalling = !isGrounded && verticalVelocity <= 0.01f;
        }

        internal void Reset(bool isGrounded)
        {
            IsGrounded = isGrounded;
            IsMoving = false;
            IsSprinting = false;
            IsJumping = false;
            IsFalling = !isGrounded;
        }
    }
}
