using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct TurnJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle ModelRoot;
        public float TurnAngleDegrees;
        public float Weight;
        public bool OffsetPosition;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;

            // The physical root follows SimulatedRotation. Counter-rotate
            // the visual ModelRoot in the AnimationStream so its Hips/feet inherit
            // the turn offset. Look consumes the same offset and owns the upper
            // body aim correction; do not restore UpperBodyRoot here.
            Quaternion yaw = Quaternion.AngleAxis(TurnAngleDegrees, Vector3.up);
            Vector3 pivot = Root.GetPosition(stream);
            Vector3 currentPosition = ModelRoot.GetPosition(stream);
            Quaternion currentRotation = ModelRoot.GetRotation(stream);
            Quaternion targetRotation = yaw * currentRotation;

            ModelRoot.SetRotation(stream, Quaternion.Slerp(currentRotation, targetRotation, Weight));
            if (!OffsetPosition) return;

            Vector3 targetPosition = pivot + yaw * (currentPosition - pivot);
            ModelRoot.SetPosition(stream, Vector3.Lerp(currentPosition, targetPosition, Weight));
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    public enum TurnRequest
    {
        None,
        Left,
        Right
    }

    public struct TurnRuntimeState
    {
        private const float MaxVisualOffsetDegrees = 90f;

        private float cachedAngle;
        private float playback;
        public float Angle { get; private set; }
        public bool IsTurning { get; private set; }

        // This is the continuous visual ModelRoot offset. The threshold controls
        // only the one-shot turn animation and when this offset is eased back to
        // zero; gating it here would make the lower body snap at the threshold.
        public float AppliedAngle => Angle;

        public void Cancel()
        {
            cachedAngle = 0f;
            playback = 0f;
            Angle = 0f;
            IsTurning = false;
        }

        public void ClampForLookYaw(float viewYawDegrees, float maximumLookYawDegrees)
        {
            float maximum = Mathf.Max(0f, maximumLookYawDegrees);
            float minimumAngle = Mathf.Max(-MaxVisualOffsetDegrees, viewYawDegrees - maximum);
            float maximumAngle = Mathf.Min(MaxVisualOffsetDegrees, viewYawDegrees + maximum);

            // TurnOffset is -Angle, so Look receives viewYawDegrees - Angle.
            // Clamp the visual counter-offset to the yaw range the spine chain can
            // actually express; otherwise a fast input frame leaves an aim gap.
            Angle = minimumAngle <= maximumAngle
                ? Mathf.Clamp(Angle, minimumAngle, maximumAngle)
                : Mathf.Clamp(Angle, -MaxVisualOffsetDegrees, MaxVisualOffsetDegrees);
        }

        public TurnRequest Advance(float viewDeltaDegrees, float deltaTime, float threshold, float speed, AnimationCurve curve)
        {
            // Look can aim the upper body through at most +/- 90 degrees.  A wider
            // visual ModelRoot offset leaves an uncompensated yaw gap between the
            // weapon and camera after a high-rate input frame.
            Angle = Mathf.Clamp(Angle - viewDeltaDegrees, -MaxVisualOffsetDegrees, MaxVisualOffsetDegrees);
            TurnRequest request = TurnRequest.None;
            if (!IsTurning && Mathf.Abs(Angle) > threshold)
            {
                cachedAngle = Angle;
                playback = 0f;
                IsTurning = true;
                request = Angle < 0f ? TurnRequest.Right : TurnRequest.Left;
            }

            if (IsTurning && deltaTime > 0f)
            {
                playback = Mathf.Clamp01(playback + deltaTime * Mathf.Max(0f, speed));
                Angle = Mathf.Lerp(cachedAngle, 0f, curve.Evaluate(playback));
                if (playback >= 1f)
                {
                    Angle = 0f;
                    IsTurning = false;
                }
            }
            return request;
        }
    }
}
