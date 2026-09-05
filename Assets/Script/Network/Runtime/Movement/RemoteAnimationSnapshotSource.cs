using CGame.Animation;
using UnityEngine;

namespace CGame.Network
{
    public sealed class RemoteAnimationSnapshotSource : IAnimationCharacterSource
    {
        private const int HardJumpThresholdMillimeters = 1500;
        private bool initialized;
        private QuantizedVector3 lastPosition;

        public RemoteAnimationSnapshotSource(Transform transform)
        {
            Transform = transform;
        }

        public Transform Transform { get; }
        public Vector3 Velocity { get; private set; }
        public bool IsGrounded { get; private set; }
        public byte MovementState { get; private set; }
        public int AppliedCount { get; private set; }
        public int DiscontinuityCount { get; private set; }
        public int AirborneAppliedCount { get; private set; }
        public int MovingAppliedCount { get; private set; }
        public int GroundedTransitionCount { get; private set; }

        public bool Apply(AuthorityState state)
        {
            bool hardJump = initialized && SquaredDistance(lastPosition, state.Position) >
                HardJumpThresholdMillimeters * HardJumpThresholdMillimeters;
            lastPosition = state.Position;
            Velocity = state.BaseVelocity.ToMeters();
            if (initialized && IsGrounded != state.Grounded) GroundedTransitionCount++;
            IsGrounded = state.Grounded;
            MovementState = state.MovementState;
            initialized = true;
            AppliedCount++;
            if (!state.Grounded) AirborneAppliedCount++;
            if (state.BaseVelocity.XMillimeters != 0 ||
                state.BaseVelocity.YMillimeters != 0 ||
                state.BaseVelocity.ZMillimeters != 0) MovingAppliedCount++;
            if (hardJump) DiscontinuityCount++;
            return hardJump;
        }

        private static long SquaredDistance(QuantizedVector3 left, QuantizedVector3 right)
        {
            long x = (long)left.XMillimeters - right.XMillimeters;
            long y = (long)left.YMillimeters - right.YMillimeters;
            long z = (long)left.ZMillimeters - right.ZMillimeters;
            return x * x + y * y + z * z;
        }
    }
}
