using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedMotorMoveSimulation : IAuthorityMoveSimulation
    {
        private readonly World world;
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;

        public DedicatedMotorMoveSimulation(World world, Pawn pawn)
        {
            this.world = world;
            this.pawn = pawn;
            motor = pawn.GetComponent<PawnMovementComponent>().Motor;
        }

        public AuthorityState Simulate(PawnMove move, long serverTick)
        {
            Vector2 input = move.MovementInput.ToVector2();
            pawn.ApplyingControlRotation(Quaternion.Euler(move.View.PitchDegrees, move.View.YawDegrees, 0f));
            pawn.SubmitControlIntent(new CharacterControlIntent(
                new Vector3(input.x, 0f, input.y),
                (move.Flags & PawnMoveFlags.Jump) != 0,
                (move.Flags & PawnMoveFlags.Sprint) != 0));
            try
            {
                world.FixedTick(CharacterPhysicsSubSystem.FixedStepSeconds);
            }
            finally
            {
                pawn.ClearingControlIntent();
            }
            Debug.Log($"[DedicatedServer][038] AuthorityMoveSimulated PawnId={move.PawnId} Sequence={move.Sequence} ServerTick={serverTick}");
            return Capture(serverTick);
        }

        public AuthorityState Capture(long serverTick)
        {
            CharacterPhysicsMotorState state = motor.GetState();
            return new AuthorityState(
                serverTick,
                QuantizedVector3.FromMeters(state.Position),
                QuantizedQuaternion.FromQuaternion(state.Rotation),
                QuantizedVector3.FromMeters(state.BaseVelocity),
                (byte)motor.PhysicsState,
                state.GroundingStatus.IsStableOnGround,
                QuantizedVector3.FromMeters(state.GroundingStatus.GroundNormal),
                state.AttachedRigidbody != null ? state.AttachedRigidbody.GetInstanceID() : 0);
        }
    }
}
