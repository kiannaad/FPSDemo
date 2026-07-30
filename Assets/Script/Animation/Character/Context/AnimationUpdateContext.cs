using System;

namespace CGame.Animation
{
    public sealed class AnimationUpdateContext
    {
        private readonly IAnimationCharacterSource source;
        private bool discontinuityPending = true;

        public AnimationUpdateContext(IAnimationCharacterSource source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            Location = new AnimationLocationData();
            Rotation = new AnimationRotationData();
            Velocity = new AnimationVelocityData();
            Acceleration = new AnimationAccelerationData();
            CharacterState = new AnimationCharacterStateData();
        }

        public AnimationLocationData Location { get; }
        public AnimationRotationData Rotation { get; }
        public AnimationVelocityData Velocity { get; }
        public AnimationAccelerationData Acceleration { get; }
        public AnimationCharacterStateData CharacterState { get; }

        internal void Update(float deltaTime)
        {
            if (source.Transform == null)
            {
                return;
            }

            if (discontinuityPending)
            {
                Reset();
                discontinuityPending = false;
                return;
            }

            Location.Update(source.Transform.position, deltaTime);
            Rotation.Update(source.Transform.rotation, deltaTime);
            Velocity.Update(source.Velocity, Rotation.WorldRotation);
            Acceleration.Update(
                Velocity.WorldVelocity,
                Rotation.WorldRotation,
                deltaTime);
            CharacterState.Update(
                source.IsGrounded,
                Velocity.HorizontalSpeed,
                Velocity.VerticalVelocity);
        }

        internal void MarkDiscontinuity()
        {
            discontinuityPending = true;
        }

        internal void Reset()
        {
            if (source.Transform == null)
            {
                return;
            }

            Location.Reset(source.Transform.position);
            Rotation.Reset(source.Transform.rotation);
            Velocity.Update(source.Velocity, Rotation.WorldRotation);
            Acceleration.Reset(Velocity.WorldVelocity);
            CharacterState.Update(
                source.IsGrounded,
                Velocity.HorizontalSpeed,
                Velocity.VerticalVelocity);
        }
    }
}
