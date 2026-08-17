using System;
using UnityEngine;

namespace CGame.Animation
{
    public struct FloatSpringState
    {
        public float Velocity;
        public float Error;

        public void Reset()
        {
            Velocity = 0f;
            Error = 0f;
        }
    }

    public struct VectorSpringState
    {
        public FloatSpringState X;
        public FloatSpringState Y;
        public FloatSpringState Z;

        public void Reset()
        {
            X.Reset();
            Y.Reset();
            Z.Reset();
        }
    }

    [Serializable]
    public struct VectorSpring
    {
        [SerializeField] private Vector3 damping;
        [SerializeField] private Vector3 stiffness;
        [SerializeField] private Vector3 speed;
        [SerializeField] private Vector3 scale;

        public VectorSpring(Vector3 damping, Vector3 stiffness, Vector3 speed, Vector3 scale)
        {
            this.damping = damping;
            this.stiffness = stiffness;
            this.speed = speed;
            this.scale = scale;
        }

        public Vector3 Damping => damping;
        public Vector3 Stiffness => stiffness;
        public Vector3 Speed => speed;
        public Vector3 Scale => scale;
        public static VectorSpring Identity => new VectorSpring(
            Vector3.one,
            Vector3.one,
            Vector3.one,
            Vector3.one);
    }

    public static class KSpringMath
    {
        public static float Interpolate(
            float current,
            float target,
            float speed,
            float criticalDamping,
            float stiffness,
            float scale,
            ref FloatSpringState state,
            float deltaTime)
        {
            if (!IsFinite(current)
                || !IsFinite(target)
                || !IsFinite(deltaTime)
                || deltaTime <= 0f)
            {
                return current;
            }

            float safeSpeed = Mathf.Max(0f, speed);
            float safeStiffness = Mathf.Max(0f, stiffness);
            float interpolation = Mathf.Min(deltaTime * safeSpeed, 1f);
            if (Mathf.Approximately(interpolation, 0f))
            {
                return current;
            }

            float damping = 2f * Mathf.Sqrt(safeStiffness) * Mathf.Max(0f, criticalDamping);
            float error = target * scale - current;
            float errorDerivative = error - state.Error;
            state.Velocity += error * safeStiffness * interpolation
                + errorDerivative * damping;
            state.Error = error;
            return current + state.Velocity * interpolation;
        }

        public static Vector3 Interpolate(
            Vector3 current,
            Vector3 target,
            VectorSpring spring,
            ref VectorSpringState state,
            float deltaTime)
        {
            current.x = Interpolate(
                current.x, target.x, spring.Speed.x, spring.Damping.x,
                spring.Stiffness.x, spring.Scale.x, ref state.X, deltaTime);
            current.y = Interpolate(
                current.y, target.y, spring.Speed.y, spring.Damping.y,
                spring.Stiffness.y, spring.Scale.y, ref state.Y, deltaTime);
            current.z = Interpolate(
                current.z, target.z, spring.Speed.z, spring.Damping.z,
                spring.Stiffness.z, spring.Scale.z, ref state.Z, deltaTime);
            return current;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
