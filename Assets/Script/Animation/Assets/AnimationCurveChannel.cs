using System;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public sealed class AnimationCurveChannel
    {
        [SerializeField] private string name;
        [SerializeField] private AnimationCurve curve = new AnimationCurve();

        public AnimationCurveChannel(string name, AnimationCurve curve)
        {
            this.name = name ?? string.Empty;
            this.curve = curve ?? new AnimationCurve();
        }

        public string Name => name;
        public AnimationCurve Curve => curve;

        public void SetCurve(AnimationCurve value)
        {
            curve = value ?? new AnimationCurve();
        }
    }
}
