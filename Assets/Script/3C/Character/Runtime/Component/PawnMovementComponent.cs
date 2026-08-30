using System;
using UnityEngine;

namespace CGame
{
    public sealed class PawnMovementComponent : ActorComponent
    {
        private readonly CharacterPhysicsMotor motor;
        private MovementComp movement;

        public PawnMovementComponent(CharacterPhysicsMotor motor = null)
        {
            this.motor = motor;
        }

        public CharacterPhysicsMotor Motor => motor;

        public int TickCount { get; private set; }

        protected override void OnInitialize()
        {
            if (!(Owner is Pawn pawn))
            {
                throw new InvalidOperationException("PawnMovementComponent requires a Pawn owner.");
            }

            if (motor != null)
            {
                movement = new MovementComp();
                movement.BindingMotor(motor);
                movement.InitializingComponent(pawn);
                motor.CharacterController = movement;
            }

            AddTickTask("Pawn.Movement", TickGroup.TG_PhysicsMovement, Tick);
        }

        protected override void OnShutdown()
        {
            if (motor != null && ReferenceEquals(motor.CharacterController, movement))
            {
                motor.CharacterController = null;
            }

            movement?.ShuttingDownComponent();
            movement = null;
        }

        private void Tick(float deltaTime)
        {
            TickCount++;
            if (Owner is Pawn pawn)
            {
                Vector3 horizontalVelocity = motor != null
                    ? Vector3.ProjectOnPlane(motor.Velocity, Vector3.up)
                    : Vector3.zero;
                pawn.SetShotMovementSnapshot(
                    motor == null || motor.GroundingStatus.IsStableOnGround,
                    horizontalVelocity.magnitude);
            }
        }
    }
}
