using System;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public struct VectorCurve
    {
        [SerializeField] private AnimationCurve x;
        [SerializeField] private AnimationCurve y;
        [SerializeField] private AnimationCurve z;

        public VectorCurve(AnimationCurve x, AnimationCurve y, AnimationCurve z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public AnimationCurve X => x;
        public AnimationCurve Y => y;
        public AnimationCurve Z => z;
        public float Length => Mathf.Max(
            KCurves.GetCurveLength(x),
            Mathf.Max(KCurves.GetCurveLength(y), KCurves.GetCurveLength(z)));

        public bool IsValid => x != null && y != null && z != null;

        public Vector3 Evaluate(float time)
        {
            return IsValid
                ? new Vector3(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time))
                : Vector3.zero;
        }

        public static VectorCurve Linear(
            float startTime,
            float endTime,
            float startValue,
            float endValue)
        {
            return new VectorCurve(
                AnimationCurve.Linear(startTime, endTime, startValue, endValue),
                AnimationCurve.Linear(startTime, endTime, startValue, endValue),
                AnimationCurve.Linear(startTime, endTime, startValue, endValue));
        }

        public static VectorCurve Constant(float startTime, float endTime, float value)
        {
            return new VectorCurve(
                AnimationCurve.Constant(startTime, endTime, value),
                AnimationCurve.Constant(startTime, endTime, value),
                AnimationCurve.Constant(startTime, endTime, value));
        }
    }
}
