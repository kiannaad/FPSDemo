using UnityEngine;

namespace CGame.Animation
{
    public static class KCurves
    {
        public static float GetCurveLength(AnimationCurve curve)
        {
            return curve == null || curve.length == 0
                ? 0f
                : curve[curve.length - 1].time;
        }

        public static float EvaluateEase(float value, EaseMode mode)
        {
            float alpha = Mathf.Clamp01(value);
            switch (mode)
            {
                case EaseMode.EaseIn:
                    return alpha * alpha;
                case EaseMode.EaseOut:
                    return 1f - (1f - alpha) * (1f - alpha);
                case EaseMode.EaseInOut:
                    return alpha < 0.5f
                        ? 2f * alpha * alpha
                        : 1f - Mathf.Pow(-2f * alpha + 2f, 2f) * 0.5f;
                default:
                    return alpha;
            }
        }

        public static bool IsWeightRelevant(float weight)
        {
            return !Mathf.Approximately(weight, 0f);
        }
    }
}
