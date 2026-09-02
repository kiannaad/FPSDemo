using UnityEngine;

namespace CGame.Network
{
    public sealed class RemoteSnapshotPresentation
    {
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;
        private readonly PawnAnimationComponent animationComponent;
        private readonly RemoteAnimationSnapshotSource animationSource;

        public RemoteSnapshotPresentation(Pawn pawn)
        {
            this.pawn = pawn;
            motor = pawn.GetComponent<PawnMovementComponent>().Motor;
            animationComponent = pawn.GetComponent<PawnAnimationComponent>();
            animationSource = animationComponent.CharacterSource as RemoteAnimationSnapshotSource;
        }

        public AuthorityState LastAppliedState { get; private set; }
        public int AnimationAppliedCount => animationSource?.AppliedCount ?? 0;
        public int AnimationDiscontinuityCount => animationSource?.DiscontinuityCount ?? 0;
        public int AnimationAirborneAppliedCount => animationSource?.AirborneAppliedCount ?? 0;
        public int AnimationMovingAppliedCount => animationSource?.MovingAppliedCount ?? 0;
        public int AnimationGroundedTransitionCount => animationSource?.GroundedTransitionCount ?? 0;

        public void Apply(AuthorityState state)
        {
            LastAppliedState = state;
            bool discontinuity = animationSource?.Apply(state) == true;
            if (discontinuity)
            {
                animationComponent.AnimInstance?.MarkDiscontinuity();
                Debug.Log($"[Network][040] AnimationDiscontinuity ServerTick={state.ServerTick}");
            }
            if (motor == null)
            {
                pawn.Transform.SetPositionAndRotation(state.Position.ToMeters(), state.Rotation.ToQuaternion());
                return;
            }

            motor.SetPositionAndRotation(state.Position.ToMeters(), state.Rotation.ToQuaternion());
            motor.BaseVelocity = state.BaseVelocity.ToMeters();
            motor.PhysicsState = (MovementState)state.MovementState;
            if (state.ServerTick % 60 == 0)
                Debug.Log($"[Network][040] RemoteSnapshotAnimationApplied ServerTick={state.ServerTick} Grounded={state.Grounded} MovementState={state.MovementState} BaseVelocity={state.BaseVelocity.XMillimeters},{state.BaseVelocity.YMillimeters},{state.BaseVelocity.ZMillimeters}");
        }
    }
}
