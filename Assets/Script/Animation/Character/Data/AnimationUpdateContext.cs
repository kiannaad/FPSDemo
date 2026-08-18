using System;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class AnimationUpdateContext
    {
        private readonly Pawn pawn;
        private readonly IAnimationCharacterSource source;
        private bool discontinuityPending = true;

        public AnimationUpdateContext(Pawn pawn, IAnimationCharacterSource source)
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
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
        public bool IsAiming { get; private set; }
        public float AimingWeight { get; private set; }
        public KTransform AimPointOffset { get; private set; } = KTransform.Identity;
        public Vector2 ViewAnglesDegrees { get; private set; }
        public Vector2 ViewDeltaDegrees { get; private set; }
        public Quaternion PresentationRotation { get; private set; } = Quaternion.identity;
        public float LookLayerWeight { get; private set; }
        public float TurnOffsetDegrees { get; private set; }
        public float LeanAngleDegrees { get; private set; }
        public Vector2 MoveInput { get; private set; }
        public bool UseFreeAim { get; private set; }
        public KTransform RecoilOffset { get; private set; } = KTransform.Identity;
        public bool WeaponCollisionHasHit { get; private set; }
        public float WeaponCollisionDistance { get; private set; }
        internal Pawn Pawn => pawn;

        internal void Update(float deltaTime)
        {
            UpdateProceduralData();
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

        internal void SetAimingWeight(float value)
        {
            AimingWeight = IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        internal void SetTurnOffsetDegrees(float value)
        {
            TurnOffsetDegrees = IsFinite(value) ? value : 0f;
        }

        internal float GetCurveValue(string curveName)
        {
            switch (curveName)
            {
                case "AimingWeight":
                    return AimingWeight;
                case "IsAiming":
                    return IsAiming ? 1f : 0f;
                case "UseFreeAim":
                    return UseFreeAim ? 1f : 0f;
                case "LookLayerWeight":
                    return LookLayerWeight;
                case "WeaponCollisionHasHit":
                    return WeaponCollisionHasHit ? 1f : 0f;
                case "WeaponCollisionDistance":
                    return WeaponCollisionDistance;
                default:
                    return 0f;
            }
        }

        internal void Reset()
        {
            UpdateProceduralData();
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

        private void UpdateProceduralData()
        {
            IsAiming = pawn.IsAiming;
            PresentationRotation = IsFinite(pawn.PresentationRotation)
                ? pawn.PresentationRotation
                : Quaternion.identity;
            AimPointOffset = SanitizePose(pawn.AimPointOffset);
            ViewAnglesDegrees = CalculateViewAnglesDegrees();
            ViewDeltaDegrees = SanitizeVector(pawn.ViewDeltaDegrees);
            LookLayerWeight = IsFinite(pawn.LookLayerWeight)
                ? Mathf.Clamp01(pawn.LookLayerWeight)
                : 0f;
            LeanAngleDegrees = IsFinite(pawn.LeanAngleDegrees)
                ? pawn.LeanAngleDegrees
                : 0f;

            Vector3 rawMoveInput = pawn.PeekingMovementInput();
            MoveInput = IsFinite(rawMoveInput)
                ? Vector2.ClampMagnitude(new Vector2(rawMoveInput.x, rawMoveInput.z), 1f)
                : Vector2.zero;
            UseFreeAim = pawn.UseFreeAim;
            RecoilOffset = SanitizePose(pawn.RecoilOffset);

            float collisionDistance = pawn.WeaponCollisionDistance;
            WeaponCollisionHasHit = pawn.WeaponCollisionHasHit
                && IsFinite(collisionDistance)
                && collisionDistance >= 0f;
            WeaponCollisionDistance = WeaponCollisionHasHit
                ? collisionDistance
                : 0f;
        }

        private static KTransform SanitizePose(Pose pose)
        {
            if (!IsFinite(pose.position) || !IsFinite(pose.rotation))
            {
                return KTransform.Identity;
            }

            float squareMagnitude = pose.rotation.x * pose.rotation.x
                + pose.rotation.y * pose.rotation.y
                + pose.rotation.z * pose.rotation.z
                + pose.rotation.w * pose.rotation.w;
            if (squareMagnitude <= Mathf.Epsilon)
            {
                return KTransform.Identity;
            }

            return new KTransform(pose.position, Quaternion.Normalize(pose.rotation));
        }

        private Vector2 CalculateViewAnglesDegrees()
        {
            if (pawn.Transform == null || !IsFinite(pawn.PresentationRotation))
            {
                return Vector2.zero;
            }

            Quaternion relativeControlRotation = Quaternion.Inverse(pawn.Transform.rotation)
                * pawn.PresentationRotation;
            Vector3 localControlForward = relativeControlRotation * Vector3.forward;
            if (!IsFinite(localControlForward))
            {
                return Vector2.zero;
            }

            return new Vector2(
                Mathf.Atan2(localControlForward.x, localControlForward.z) * Mathf.Rad2Deg,
                -Mathf.Asin(Mathf.Clamp(localControlForward.y, -1f, 1f)) * Mathf.Rad2Deg);
        }

        private static Vector2 SanitizeVector(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) ? value : Vector2.zero;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
