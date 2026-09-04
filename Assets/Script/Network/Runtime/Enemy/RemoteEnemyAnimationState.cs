using System;
using UnityEngine;

namespace CGame.Network
{
    public readonly struct RemoteEnemyAnimationState
    {
        private const float MovingSpeedThreshold = 0.05f;

        public RemoteEnemyAnimationState(
            float speed,
            Vector2 moveDirection,
            bool isGrounded,
            Quaternion facing,
            EnemyBrainState brainState = EnemyBrainState.Patrol)
        {
            Speed = speed;
            MoveDirection = moveDirection;
            IsGrounded = isGrounded;
            Facing = facing;
            BrainState = brainState;
        }

        public float Speed { get; }
        public Vector2 MoveDirection { get; }
        public bool IsGrounded { get; }
        public Quaternion Facing { get; }
        public EnemyBrainState BrainState { get; }
        public bool IsMoving => Speed > MovingSpeedThreshold;
        public bool IsInCover => BrainState == EnemyBrainState.CoverHold ||
            BrainState == EnemyBrainState.PeekFire || BrainState == EnemyBrainState.ReturnToCover;
        public bool IsPeeking => BrainState == EnemyBrainState.PeekFire;

        public static RemoteEnemyAnimationState FromSnapshot(EnemySnapshotEvent snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Vector3 velocity = snapshot.PlanarVelocity.ToValue().ToMeters();
            Vector2 direction = new Vector2(velocity.x, velocity.z);
            float speed = direction.magnitude;
            return new RemoteEnemyAnimationState(
                speed,
                speed > Mathf.Epsilon ? direction / speed : Vector2.zero,
                snapshot.IsGrounded,
                snapshot.Rotation.ToValue().ToQuaternion(),
                snapshot.BrainState);
        }
    }
}
