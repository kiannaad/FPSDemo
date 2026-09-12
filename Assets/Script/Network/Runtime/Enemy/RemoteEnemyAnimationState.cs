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
        public bool IsPeeking => BrainState == EnemyBrainState.PeekFire && !IsMoving;
        public bool IsAiming => IsGrounded && !IsMoving &&
            (BrainState == EnemyBrainState.Fire || BrainState == EnemyBrainState.PeekFire);

        public static RemoteEnemyAnimationState FromSnapshot(EnemySnapshotEvent snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Vector3 velocity = snapshot.PlanarVelocity.ToValue().ToMeters();
            velocity.y = 0f;
            float speed = velocity.magnitude;
            Quaternion facing = snapshot.Rotation.ToValue().ToQuaternion();
            Vector3 localVelocity = Quaternion.Inverse(facing) * velocity;
            Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);
            return new RemoteEnemyAnimationState(
                speed,
                speed > MovingSpeedThreshold ? direction / speed : Vector2.zero,
                snapshot.IsGrounded,
                facing,
                snapshot.BrainState);
        }
    }
}
