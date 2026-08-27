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
            if (!KCurves.IsWeightRelevant(Weight))
            {
                return;
            }

            Quaternion yaw = Quaternion.Euler(0f, TurnAngleDegrees, 0f);
            AnimationLayerJobUtility.ModifyTransform(
                stream,
                Root,
                ModelRoot,
                new KPose
                {
                    Pose = new KTransform(Vector3.zero, yaw),
                    Space = TransformSpace.ComponentSpace,
                    ModifyMode = TransformModifyMode.Add
                },
                Weight);

            if (!OffsetPosition)
            {
                return;
            }

            Vector3 localPosition = ModelRoot.GetLocalPosition(stream);
            localPosition = yaw * localPosition - localPosition;
            AnimationLayerJobUtility.ModifyTransform(
                stream,
                Root,
                ModelRoot,
                new KPose
                {
                    Pose = new KTransform(localPosition, Quaternion.identity),
                    Space = TransformSpace.ComponentSpace,
                    ModifyMode = TransformModifyMode.Add
                },
                Weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        public static float CalculateYawCorrection(
            Quaternion currentRotation,
            float targetVisualYawDegrees,
            float fallbackCorrectionDegrees)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(currentRotation * Vector3.forward, Vector3.up);
            if (planarForward.sqrMagnitude <= 0.000001f)
            {
                return fallbackCorrectionDegrees;
            }

            float currentYaw = Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(currentYaw, targetVisualYawDegrees);
        }
    }

    public struct TurnRuntimeState
    {
        private float playback;
        private float turnAngle;
        private float cachedTurnAngle;

        public float Angle
        {
            get => turnAngle;
            set => turnAngle = value;
        }
        public bool IsTurning { get; private set; }

        public void Cancel()
        {
            playback = 0f;
            turnAngle = 0f;
            cachedTurnAngle = 0f;
            IsTurning = false;
        }

        public TurnRequest Advance(
            float viewDeltaDegrees,
            float deltaTime,
            float threshold,
            float speed,
            AnimationCurve curve,
            float weight)
        {
            Debug.Log($"viewDeltaDegrees : {viewDeltaDegrees} degrees");
            turnAngle -= viewDeltaDegrees;
            turnAngle *= Mathf.Clamp01(weight);
            Debug.Log($"turnAngle : {turnAngle} degrees");
            TurnRequest request = TurnRequest.None;
            if (!IsTurning && Mathf.Abs(turnAngle) > threshold)
            {
                cachedTurnAngle = turnAngle;
                IsTurning = true;
                playback = 0f;
                request = turnAngle < 0f ? TurnRequest.Right : TurnRequest.Left;
            }

            if (!IsTurning)
            {
                return request;
            }

            playback = Mathf.Clamp01(playback + Mathf.Max(0f, deltaTime) * speed);
            float alpha = curve.Evaluate(playback);
            turnAngle = Mathf.Lerp(cachedTurnAngle, 0f, alpha);
            if (Mathf.Approximately(playback, 1f))
            {
                IsTurning = false;
            }

            return request;
        }
    }

    public enum TurnRequest
    {
        None,
        Left,
        Right
    }
}
