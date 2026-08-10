using System;

namespace CGame
{
    public sealed class PawnMovementComponent : PawnFeatureComponent
    {
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;
        private readonly MovementComp movement;

        public PawnMovementComponent(Pawn pawn) : base("Movement")
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
        }

        public PawnMovementComponent(Pawn pawn, CharacterPhysicsMotor motor) : base("Movement")
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            this.motor = motor ?? throw new ArgumentNullException(nameof(motor));
            movement = new MovementComp();
            movement.BindingMotor(motor);
            motor.CharacterController = movement;
        }

        public CharacterPhysicsMotor Motor => motor;

        public override void EnterState(PawnInitState nextState, PawnInitContext context)
        {
            base.EnterState(nextState, context);
            if (nextState == PawnInitState.DataAvailable)
            {
                movement?.InitializingComponent(pawn);
            }
        }

        public override void Shutdown()
        {
            if (motor != null && ReferenceEquals(motor.CharacterController, movement))
            {
                motor.CharacterController = null;
            }

            movement?.ShuttingDownComponent();
            base.Shutdown();
        }
    }
}
