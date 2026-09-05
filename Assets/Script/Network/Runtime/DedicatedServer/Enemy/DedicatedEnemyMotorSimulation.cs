using System;
using UnityEngine;

namespace CGame.Network
{
    public readonly struct DedicatedEnemyMotorState
    {
        public DedicatedEnemyMotorState(Vector3 position, Quaternion rotation, Vector3 planarVelocity, bool isGrounded)
        {
            Position = position;
            Rotation = rotation;
            PlanarVelocity = planarVelocity;
            IsGrounded = isGrounded;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 PlanarVelocity { get; }
        public bool IsGrounded { get; }
    }

    public sealed class DedicatedEnemyMotorSimulation
    {
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;

        public DedicatedEnemyMotorSimulation(Pawn pawn)
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            CharacterPhysicsMotor pawnMotor = null;
            if (pawn.TryGetComponent(out PawnMovementComponent movement)) pawnMotor = movement.Motor;
            motor = pawnMotor ?? pawn.Root?.GetComponent<CharacterPhysicsMotor>()
                ?? throw new InvalidOperationException("Dedicated enemy Pawn requires a CharacterPhysicsMotor.");
        }

        public void ApplyIntent(EnemyNavigationIntent intent)
        {
            if (!intent.HasPath)
            {
                pawn.ClearingControlIntent();
                return;
            }

            pawn.ApplyingControlRotation(intent.DesiredFacing);
            pawn.SubmitControlIntent(new CharacterControlIntent(Vector3.forward, jumpRequested: false, sprintRequested: false));
        }

        public void ClearIntent() => pawn.ClearingControlIntent();

        public DedicatedEnemyMotorState Capture()
        {
            CharacterPhysicsMotorState state = motor.GetState();
            return new DedicatedEnemyMotorState(
                state.Position,
                state.Rotation,
                Vector3.ProjectOnPlane(state.BaseVelocity, Vector3.up),
                state.GroundingStatus.IsStableOnGround);
        }
    }
}
