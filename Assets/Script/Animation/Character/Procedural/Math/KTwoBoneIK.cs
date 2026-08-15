using UnityEngine;

namespace CGame.Animation
{
    public struct KTwoBoneIkData
    {
        public KTransform Root;
        public KTransform Mid;
        public KTransform Tip;
        public KTransform Target;
        public KTransform Hint;
        public float PositionWeight;
        public float RotationWeight;
        public float HintWeight;
        public bool HasValidHint;
    }

    public static class KTwoBoneIK
    {
        private const float SqrEpsilon = 1e-8f;

        public static void Solve(ref KTwoBoneIkData data)
        {
            Vector3 rootPosition = data.Root.Position;
            Vector3 midPosition = data.Mid.Position;
            Vector3 tipPosition = data.Tip.Position;
            Vector3 targetPosition = Vector3.Lerp(tipPosition, data.Target.Position, data.PositionWeight);
            Quaternion targetRotation = Quaternion.Slerp(data.Tip.Rotation, data.Target.Rotation, data.RotationWeight);
            bool hasHint = data.HasValidHint && data.HintWeight > 0f;

            Vector3 rootToMid = midPosition - rootPosition;
            Vector3 midToTip = tipPosition - midPosition;
            Vector3 rootToTip = tipPosition - rootPosition;
            Vector3 rootToTarget = targetPosition - rootPosition;
            float rootToMidLength = rootToMid.magnitude;
            float midToTipLength = midToTip.magnitude;
            float rootToTipLength = rootToTip.magnitude;
            float rootToTargetLength = rootToTarget.magnitude;

            float oldAngle = TriangleAngle(rootToTipLength, rootToMidLength, midToTipLength);
            float newAngle = TriangleAngle(rootToTargetLength, rootToMidLength, midToTipLength);
            Vector3 axis = Vector3.Cross(rootToMid, midToTip);
            if (axis.sqrMagnitude < SqrEpsilon)
            {
                axis = hasHint ? Vector3.Cross(data.Hint.Position - rootPosition, midToTip) : Vector3.zero;
                if (axis.sqrMagnitude < SqrEpsilon) axis = Vector3.Cross(rootToTarget, midToTip);
                if (axis.sqrMagnitude < SqrEpsilon) axis = Vector3.up;
            }

            axis.Normalize();
            float halfDelta = 0.5f * (oldAngle - newAngle);
            Quaternion midDelta = new Quaternion(
                axis.x * Mathf.Sin(halfDelta),
                axis.y * Mathf.Sin(halfDelta),
                axis.z * Mathf.Sin(halfDelta),
                Mathf.Cos(halfDelta));
            KTransform localTip = data.Mid.GetRelativeTransform(data.Tip, false);
            data.Mid.Rotation = midDelta * data.Mid.Rotation;
            data.Tip = data.Mid.GetWorldTransform(localTip, false);

            rootToTip = data.Tip.Position - rootPosition;
            KTransform localMid = data.Root.GetRelativeTransform(data.Mid, false);
            localTip = data.Mid.GetRelativeTransform(data.Tip, false);
            data.Root.Rotation = Quaternion.FromToRotation(rootToTip, rootToTarget) * data.Root.Rotation;
            data.Mid = data.Root.GetWorldTransform(localMid, false);
            data.Tip = data.Mid.GetWorldTransform(localTip, false);

            if (hasHint)
            {
                Vector3 solvedAxis = data.Tip.Position - rootPosition;
                float solvedAxisSqr = solvedAxis.sqrMagnitude;
                if (solvedAxisSqr > 0f)
                {
                    Vector3 normalizedAxis = solvedAxis / Mathf.Sqrt(solvedAxisSqr);
                    Vector3 projectedMid = data.Mid.Position - rootPosition;
                    projectedMid -= normalizedAxis * Vector3.Dot(projectedMid, normalizedAxis);
                    Vector3 projectedHint = data.Hint.Position - rootPosition;
                    projectedHint -= normalizedAxis * Vector3.Dot(projectedHint, normalizedAxis);
                    float maxReach = rootToMidLength + midToTipLength;
                    if (projectedMid.sqrMagnitude > maxReach * maxReach * 0.001f
                        && projectedHint.sqrMagnitude > 0f)
                    {
                        Quaternion hintRotation = Quaternion.FromToRotation(projectedMid, projectedHint);
                        hintRotation.x *= data.HintWeight;
                        hintRotation.y *= data.HintWeight;
                        hintRotation.z *= data.HintWeight;
                        hintRotation = NormalizeSafe(hintRotation);
                        data.Root.Rotation = hintRotation * data.Root.Rotation;
                        data.Mid = data.Root.GetWorldTransform(localMid, false);
                        data.Tip = data.Mid.GetWorldTransform(localTip, false);
                    }
                }
            }

            data.Tip.Rotation = targetRotation;
        }

        private static float TriangleAngle(float opposite, float adjacentA, float adjacentB)
        {
            float denominator = 2f * adjacentA * adjacentB;
            if (denominator <= Mathf.Epsilon) return 0f;
            float cosine = Mathf.Clamp(
                (adjacentA * adjacentA + adjacentB * adjacentB - opposite * opposite) / denominator,
                -1f,
                1f);
            return Mathf.Acos(cosine);
        }

        private static Quaternion NormalizeSafe(Quaternion value)
        {
            float dot = Quaternion.Dot(value, value);
            if (dot <= float.Epsilon) return Quaternion.identity;
            float inverseLength = 1f / Mathf.Sqrt(dot);
            return new Quaternion(
                value.x * inverseLength,
                value.y * inverseLength,
                value.z * inverseLength,
                value.w * inverseLength);
        }
    }
}
