using UnityEngine;

namespace CGame.Animation
{
    // Compatibility state retained for existing diagnostics and historical tests.
    // TurnLayerJob no longer reads or writes this state.
    public sealed class TurnPresentationState
    {
        private const float MaxVisualOffsetDegrees = 90f;

        private float visualFacingYaw;
        private float turnStartYaw;
        private float turnTargetYaw;
        private float playback;
        private float capturedSpeed;
        private AnimationCurve capturedCurve;
        private float lastPhysicalYaw;
        private bool hasPhysicalYaw;

        public float Angle { get; private set; }
        public float VisualFacingYaw => visualFacingYaw;
        public float TurnTargetYaw => turnTargetYaw;
        public float Playback => playback;
        public bool IsInitialized { get; private set; }
        public bool IsTurning { get; private set; }
        public bool IsControlHandoff { get; private set; }

        public float ResolvePhysicalYaw(Quaternion rootRotation)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(rootRotation * Vector3.forward, Vector3.up);
            if (planarForward.sqrMagnitude > 0.000001f)
            {
                lastPhysicalYaw = Mathf.DeltaAngle(0f, Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg);
                hasPhysicalYaw = true;
            }

            return hasPhysicalYaw ? lastPhysicalYaw : 0f;
        }

        public void Reset(float physicalRootYaw)
        {
            visualFacingYaw = Mathf.DeltaAngle(0f, physicalRootYaw);
            turnStartYaw = visualFacingYaw;
            turnTargetYaw = visualFacingYaw;
            playback = 0f;
            capturedSpeed = 0f;
            capturedCurve = null;
            Angle = 0f;
            IsInitialized = true;
            IsTurning = false;
            IsControlHandoff = false;
        }

        public TurnRequest AdvanceTurn(float physicalRootYaw, float deltaTime, float threshold, float speed, AnimationCurve curve)
        {
            if (!IsInitialized)
            {
                Reset(physicalRootYaw);
            }

            IsControlHandoff = false;
            if (IsTurning)
            {
                playback = Mathf.Clamp01(playback + Mathf.Max(0f, deltaTime) * capturedSpeed);
                visualFacingYaw = Mathf.LerpAngle(turnStartYaw, turnTargetYaw, Mathf.Clamp01(capturedCurve.Evaluate(playback)));
                if (playback >= 1f)
                {
                    visualFacingYaw = turnTargetYaw;
                    IsTurning = false;
                }
            }

            Angle = Mathf.Clamp(Mathf.DeltaAngle(physicalRootYaw, visualFacingYaw), -MaxVisualOffsetDegrees, MaxVisualOffsetDegrees);
            if (IsTurning || Mathf.Abs(Angle) <= Mathf.Max(0f, threshold))
            {
                return TurnRequest.None;
            }

            turnStartYaw = visualFacingYaw;
            turnTargetYaw = Mathf.DeltaAngle(0f, physicalRootYaw);
            playback = 0f;
            capturedSpeed = Mathf.Max(0.0001f, speed);
            capturedCurve = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            IsTurning = true;
            return Angle < 0f ? TurnRequest.Right : TurnRequest.Left;
        }

        public void AdvanceControlHandoff(float physicalRootYaw, float deltaTime, float duration)
        {
            if (!IsInitialized)
            {
                Reset(physicalRootYaw);
            }

            IsTurning = false;
            float step = MaxVisualOffsetDegrees * Mathf.Max(0f, deltaTime) / Mathf.Max(0.001f, duration);
            visualFacingYaw = Mathf.MoveTowardsAngle(visualFacingYaw, physicalRootYaw, step);
            Angle = Mathf.DeltaAngle(physicalRootYaw, visualFacingYaw);
            IsControlHandoff = Mathf.Abs(Angle) > 0.01f;
        }
    }
}
