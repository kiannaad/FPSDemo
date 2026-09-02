using UnityEngine;

namespace CGame.Network
{
    public sealed class LocalPawnMoveReplayTarget : ILocalMoveReplayTarget
    {
        private const float FixedDeltaTime = 1f / 60f;
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;

        public LocalPawnMoveReplayTarget(Pawn pawn)
        {
            this.pawn = pawn;
            motor = pawn.GetComponent<PawnMovementComponent>().Motor;
        }

        public void ApplyAuthorityState(AuthorityState state)
        {
            if (motor == null)
            {
                pawn.Transform.SetPositionAndRotation(
                    state.Position.ToMeters(),
                    state.Rotation.ToQuaternion());
                pawn.ApplyingControlRotation(state.ControlRotation.ToQuaternion());
                return;
            }

            CharacterPhysicsMotorState motorState = motor.GetState();
            motorState.Position = state.Position.ToMeters();
            motorState.Rotation = state.Rotation.ToQuaternion();
            motorState.BaseVelocity = state.BaseVelocity.ToMeters();
            motorState.MustUnground = false;
            motorState.MustUngroundTime = 0f;
            motorState.LastMovementIterationFoundAnyGround = state.Grounded;
            motorState.GroundingStatus = CreateGroundingStatus(state);
            motorState.AttachedRigidbody = null;
            motorState.AttachedRigidbodyVelocity = Vector3.zero;
            motor.ApplyState(motorState);
            pawn.ApplyingControlRotation(state.ControlRotation.ToQuaternion());
        }

        public void Replay(PawnMove move)
        {
            Vector2 input = move.MovementInput.ToVector2();
            pawn.ApplyingControlRotation(Quaternion.Euler(move.View.PitchDegrees, move.View.YawDegrees, 0f));
            pawn.SubmitControlIntent(new CharacterControlIntent(
                new Vector3(input.x, 0f, input.y),
                (move.Flags & PawnMoveFlags.Jump) != 0,
                (move.Flags & PawnMoveFlags.Sprint) != 0));
            try
            {
                if (motor != null)
                {
                    motor.UpdatePhase1(FixedDeltaTime);
                    motor.UpdatePhase2(FixedDeltaTime);
                    motor.Transform.SetPositionAndRotation(
                        motor.TransientPosition,
                        motor.TransientRotation);
                }
            }
            finally
            {
                pawn.ClearingControlIntent();
            }
        }

        private static CharacterTransientGroundingReport CreateGroundingStatus(AuthorityState state)
        {
            Vector3 normal = state.GroundNormal.ToMeters();
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            else normal.Normalize();
            return new CharacterTransientGroundingReport
            {
                FoundAnyGround = state.Grounded,
                IsStableOnGround = state.Grounded,
                SnappingPrevented = false,
                GroundNormal = normal,
                InnerGroundNormal = normal,
                OuterGroundNormal = normal
            };
        }
    }
}
