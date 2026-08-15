using System;
using UnityEngine;

namespace CGame.Animation
{
    public enum AnimationCurveBlendMode
    {
        Direct,
        Mask
    }

    [Serializable]
    public sealed class AnimationCurveBlend
    {
        [SerializeField] private string curveName;
        [SerializeField] private AnimationCurveBlendMode mode;
        [SerializeField, Range(0f, 1f)] private float clampMinimum;

        public string CurveName => curveName;
        public AnimationCurveBlendMode Mode => mode;
        public float ClampMinimum => clampMinimum;

        public float Evaluate(Animator animator)
        {
            if (string.IsNullOrEmpty(curveName) || curveName == "None")
            {
                return 1f;
            }

            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            return Evaluate(animator.GetFloat(curveName));
        }

        public float Evaluate(float curveValue)
        {
            float value = Mathf.Clamp01(curveValue);
            if (mode == AnimationCurveBlendMode.Mask)
            {
                value = 1f - value;
            }

            return Mathf.Lerp(clampMinimum, 1f, value);
        }
    }
}
