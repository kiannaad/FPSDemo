using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    public sealed class CharacterPhysicsWorld :
        ICharacterPhysicsWorld,
        ICharacterPhysicsEventSink
    {
        private readonly struct DiscreteCollisionEvent
        {
            public DiscreteCollisionEvent(ICharacterPhysicsController controller, Collider collider)
            {
                Controller = controller;
                Collider = collider;
            }

            public ICharacterPhysicsController Controller { get; }

            public Collider Collider { get; }
        }

        private readonly List<CharacterPhysicsMotor> motors;
        private readonly List<CharacterPhysicsMover> movers;
        private readonly CharacterPhysicsSettings settings;
        private readonly bool ownsSettings;
        private readonly Queue<DiscreteCollisionEvent> collisionEvents = new Queue<DiscreteCollisionEvent>();
        private float interpolationStartTime = -1f;
        private float interpolationDeltaTime = -1f;
        private bool isDisposed;

        public CharacterPhysicsWorld(CharacterPhysicsSettings settings, bool ownsSettings = false)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.ownsSettings = ownsSettings;
            motors = new List<CharacterPhysicsMotor>(settings.MotorsListInitialCapacity);
            movers = new List<CharacterPhysicsMover>(settings.MoversListInitialCapacity);
        }

        public IPhysicsRegistration Register(CharacterPhysicsMotor motor)
        {
            if (motor == null) throw new ArgumentNullException(nameof(motor));
            if (!motors.Contains(motor)) motors.Add(motor);
            motor.SetPhysicsEventSink(this);
            return new Registration<CharacterPhysicsMotor>(
                motors,
                motor,
                registeredMotor => registeredMotor.SetPhysicsEventSink(null));
        }

        public IPhysicsRegistration Register(CharacterPhysicsMover mover)
        {
            if (mover == null) throw new ArgumentNullException(nameof(mover));
            if (!movers.Contains(mover)) movers.Add(mover);
            mover.Rigidbody.interpolation = RigidbodyInterpolation.None;
            return new Registration<CharacterPhysicsMover>(movers, mover, null);
        }

        public void Step(float deltaTime, float currentTime)
        {
            if (isDisposed || !settings.AutoSimulation || deltaTime <= 0f) return;
            if (settings.Interpolate) PrepareInterpolation();
            for (int i = 0; i < movers.Count; i++) movers[i].VelocityUpdate(deltaTime);
            for (int i = 0; i < motors.Count; i++) motors[i].UpdatePhase1(deltaTime);
            for (int i = 0; i < movers.Count; i++)
            {
                CharacterPhysicsMover mover = movers[i];
                mover.Transform.SetPositionAndRotation(mover.TransientPosition, mover.TransientRotation);
                mover.Rigidbody.position = mover.TransientPosition;
                mover.Rigidbody.rotation = mover.TransientRotation;
            }
            for (int i = 0; i < motors.Count; i++)
            {
                CharacterPhysicsMotor motor = motors[i];
                motor.UpdatePhase2(deltaTime);
                motor.Transform.SetPositionAndRotation(motor.TransientPosition, motor.TransientRotation);
            }
            if (settings.Interpolate) CompleteInterpolation(deltaTime, currentTime);
        }

        public void Step(float deltaTime)
        {
            Step(deltaTime, Time.time);
        }

        public void ConsumePostPhysicsEvents(float deltaTime)
        {
            int eventCount = collisionEvents.Count;
            for (int index = 0; index < eventCount; index++)
            {
                DiscreteCollisionEvent collisionEvent = collisionEvents.Dequeue();
                if (collisionEvent.Controller == null || collisionEvent.Collider == null)
                {
                    continue;
                }

                try
                {
                    collisionEvent.Controller.OnDiscreteCollisionDetected(collisionEvent.Collider);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public void Enqueue(ICharacterPhysicsController controller, Collider collider)
        {
            if (!isDisposed && controller != null && collider != null)
            {
                collisionEvents.Enqueue(new DiscreteCollisionEvent(controller, collider));
            }
        }

        public void Present(float currentTime)
        {
            if (isDisposed || !settings.Interpolate || interpolationDeltaTime <= 0f) return;
            float factor = Mathf.Clamp01((currentTime - interpolationStartTime) / interpolationDeltaTime);
            for (int i = 0; i < motors.Count; i++)
            {
                CharacterPhysicsMotor motor = motors[i];
                motor.Transform.SetPositionAndRotation(Vector3.Lerp(motor.InitialTickPosition, motor.TransientPosition, factor), Quaternion.Slerp(motor.InitialTickRotation, motor.TransientRotation, factor));
            }
            for (int i = 0; i < movers.Count; i++)
            {
                CharacterPhysicsMover mover = movers[i];
                mover.Transform.SetPositionAndRotation(Vector3.Lerp(mover.InitialTickPosition, mover.TransientPosition, factor), Quaternion.Slerp(mover.InitialTickRotation, mover.TransientRotation, factor));
                Vector3 position = mover.Transform.position;
                Quaternion rotation = mover.Transform.rotation;
                mover.PositionDeltaFromInterpolation = position - mover.LatestInterpolationPosition;
                mover.RotationDeltaFromInterpolation = Quaternion.Inverse(mover.LatestInterpolationRotation) * rotation;
                mover.LatestInterpolationPosition = position;
                mover.LatestInterpolationRotation = rotation;
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            for (int index = 0; index < motors.Count; index++)
            {
                motors[index]?.SetPhysicsEventSink(null);
            }

            motors.Clear();
            movers.Clear();
            collisionEvents.Clear();
            if (!ownsSettings)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(settings);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private void PrepareInterpolation()
        {
            for (int i = 0; i < motors.Count; i++)
            {
                CharacterPhysicsMotor motor = motors[i];
                motor.InitialTickPosition = motor.TransientPosition;
                motor.InitialTickRotation = motor.TransientRotation;
                motor.Transform.SetPositionAndRotation(motor.TransientPosition, motor.TransientRotation);
            }
            for (int i = 0; i < movers.Count; i++)
            {
                CharacterPhysicsMover mover = movers[i];
                mover.InitialTickPosition = mover.TransientPosition;
                mover.InitialTickRotation = mover.TransientRotation;
                mover.Transform.SetPositionAndRotation(mover.TransientPosition, mover.TransientRotation);
                mover.Rigidbody.position = mover.TransientPosition;
                mover.Rigidbody.rotation = mover.TransientRotation;
            }
        }

        private void CompleteInterpolation(float deltaTime, float currentTime)
        {
            interpolationStartTime = currentTime;
            interpolationDeltaTime = deltaTime;
            for (int i = 0; i < motors.Count; i++) motors[i].Transform.SetPositionAndRotation(motors[i].InitialTickPosition, motors[i].InitialTickRotation);
            for (int i = 0; i < movers.Count; i++)
            {
                CharacterPhysicsMover mover = movers[i];
                if (mover.MoveWithPhysics)
                {
                    mover.Rigidbody.position = mover.InitialTickPosition;
                    mover.Rigidbody.rotation = mover.InitialTickRotation;
                    mover.Rigidbody.MovePosition(mover.TransientPosition);
                    mover.Rigidbody.MoveRotation(mover.TransientRotation);
                }
                else
                {
                    mover.Rigidbody.position = mover.TransientPosition;
                    mover.Rigidbody.rotation = mover.TransientRotation;
                }
            }
        }

        private sealed class Registration<T> : IPhysicsRegistration where T : class
        {
            private readonly List<T> members;
            private readonly Action<T> onDispose;
            private T member;
            public Registration(List<T> members, T member, Action<T> onDispose)
            {
                this.members = members;
                this.member = member;
                this.onDispose = onDispose;
            }
            public bool IsActive => member != null;
            public void Dispose()
            {
                if (member == null) return;
                T removed = member;
                members.Remove(removed);
                member = null;
                onDispose?.Invoke(removed);
            }
        }
    }
}
