using System;
using UnityEngine;

namespace CGame.Animation
{
    public enum AnimationCurveBlendMode
    {
        Direct,
        Mask
    }

    public enum AnimationCurveBlendSource
    {
        Animator,
        Playables,
        Context
    }

    [Serializable]
    public sealed class AnimationCurveBlend
    {
        [SerializeField] private string curveName;
        [SerializeField] private AnimationCurveBlendMode mode;
        [SerializeField, Range(0f, 1f)] private float clampMinimum;
        [SerializeField] private AnimationCurveBlendSource source;

        public string CurveName => curveName;
        public AnimationCurveBlendMode Mode => mode;
        public float ClampMinimum => clampMinimum;
        public AnimationCurveBlendSource Source => source;

        public float Evaluate(CharacterAnimInstance owner)
        {
            if (string.IsNullOrEmpty(curveName) || curveName == "None")
            {
                return 1f;
            }

            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            return Evaluate(owner.GetCurveValue(curveName, source));
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
