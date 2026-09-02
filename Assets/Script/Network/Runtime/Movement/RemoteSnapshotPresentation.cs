using UnityEngine;

namespace CGame.Network
{
    public sealed class RemoteSnapshotPresentation
    {
        private readonly Pawn pawn;
        private readonly CharacterPhysicsMotor motor;
        private readonly PawnAnimationComponent animationComponent;
        private readonly RemoteAnimationSnapshotSource animationSource;
        private readonly NetworkLoopAnimationPhaseSynchronizer loopPhaseSynchronizer =
            new NetworkLoopAnimationPhaseSynchronizer();
        private bool hasAppliedControlRotation;

        public RemoteSnapshotPresentation(Pawn pawn)
        {
            this.pawn = pawn;
            motor = pawn.TryGetComponent(out PawnMovementComponent movement)
                ? movement.Motor
                : null;
            animationComponent = pawn.TryGetComponent(out PawnAnimationComponent animation)
                ? animation
                : null;
            animationSource = animationComponent?.CharacterSource as RemoteAnimationSnapshotSource;
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
            Quaternion authorityRotation = state.Rotation.ToQuaternion();
            Quaternion controlRotation = state.ControlRotation.ToQuaternion();
            Vector2 viewDelta = hasAppliedControlRotation
                ? CalculateViewDelta(pawn.ControlRotation, controlRotation)
                : Vector2.zero;
            pawn.ApplyingControlRotation(controlRotation);
            pawn.ApplyingViewDelta(viewDelta);
            hasAppliedControlRotation = true;
            pawn.SetAimingFromAbility(state.IsAiming);
            bool discontinuity = animationSource?.Apply(state) == true;
            bool isMoving = state.BaseVelocity.XMillimeters != 0 || state.BaseVelocity.ZMillimeters != 0;
            loopPhaseSynchronizer.Synchronize(animationComponent?.Animator, state.ServerTick, isMoving);
            if (discontinuity)
            {
                animationComponent.AnimInstance?.MarkDiscontinuity();
                Debug.Log($"[Network][040] AnimationDiscontinuity ServerTick={state.ServerTick}");
            }
            if (motor == null)
            {
                pawn.Transform.SetPositionAndRotation(state.Position.ToMeters(), authorityRotation);
                return;
            }

            motor.SetPositionAndRotation(state.Position.ToMeters(), authorityRotation);
            motor.BaseVelocity = state.BaseVelocity.ToMeters();
            motor.PhysicsState = (MovementState)state.MovementState;
            if (state.ServerTick % 60 == 0)
                Debug.Log($"[Network][040] RemoteSnapshotAnimationApplied ServerTick={state.ServerTick} Grounded={state.Grounded} MovementState={state.MovementState} BaseVelocity={state.BaseVelocity.XMillimeters},{state.BaseVelocity.YMillimeters},{state.BaseVelocity.ZMillimeters}");
        }

        private static Vector2 CalculateViewDelta(Quaternion previousControlRotation, Quaternion nextControlRotation)
        {
            Vector3 previous = previousControlRotation.eulerAngles;
            Vector3 next = nextControlRotation.eulerAngles;
            return new Vector2(
                Mathf.DeltaAngle(previous.y, next.y),
                -Mathf.DeltaAngle(previous.x, next.x));
        }
    }
}
