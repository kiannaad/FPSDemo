using UnityEngine;

namespace CGame.Network
{
    public sealed class LocalPawnMoveReplayTarget : ILocalMoveReplayTarget
    {
        private const float FixedDeltaTime = 1f / 60f;
        private const float MoveSpeed = 5f;
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;

        public LocalPawnMoveReplayTarget(Pawn pawn)
        {
            this.pawn = pawn;
            motor = pawn.GetComponent<PawnMovementComponent>().Motor;
        }

        public void ApplyAuthorityState(AuthorityState state)
        {
            if (motor != null) motor.SetPosition(state.Position.ToMeters());
            else pawn.Transform.position = state.Position.ToMeters();
        }

        public void Replay(PawnMove move)
        {
            Vector2 input = move.MovementInput.ToVector2();
            Vector3 direction = Quaternion.Euler(0f, move.View.YawDegrees, 0f) * new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            Vector3 position = pawn.Transform.position + direction * (MoveSpeed * FixedDeltaTime);
            if (motor != null) motor.SetPosition(position);
            else pawn.Transform.position = position;
        }
    }
}
