using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CGame.Animation
{
    public class AnimationClipAsset : AnimationAssetBase
    {
        [SerializeField] private AnimationClip animationClip;
        [FormerlySerializedAs("fadeDuration")]
        [SerializeField, Min(0f)] private float blendInTime = 0.25f;
        [SerializeField, Min(0f)] private float blendOutTime = 0.25f;
        [SerializeField] private float speed = 1f;
        [SerializeField] private bool overrideNormalizedStartTime;
        [SerializeField, Range(0f, 1f)] private float normalizedStartTime;
        [SerializeField] private AvatarMask mask;
        [SerializeField] private AvatarMask overrideMask;
        [SerializeField] private bool additive;
        [SerializeField] private List<AnimationCurveChannel> namedCurves = new List<AnimationCurveChannel>();

        public AnimationClip AnimationClip => animationClip;

        [Obsolete("Use BlendInTime and BlendOutTime.")]
        public float FadeDuration
        {
            get => blendInTime;
            set
            {
                blendInTime = Mathf.Max(0f, value);
                blendOutTime = blendInTime;
            }
        }

        public float BlendInTime
        {
            get => blendInTime;
            set => blendInTime = Mathf.Max(0f, value);
        }

        public float BlendOutTime
        {
            get => blendOutTime;
            set => blendOutTime = Mathf.Max(0f, value);
        }

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public bool OverrideNormalizedStartTime
        {
            get => overrideNormalizedStartTime;
            set => overrideNormalizedStartTime = value;
        }

        public float NormalizedStartTime
        {
            get => normalizedStartTime;
            set => normalizedStartTime = Mathf.Clamp01(value);
        }

        public AvatarMask Mask
        {
            get => mask;
            set => mask = value;
        }

        public AvatarMask OverrideMask
        {
            get => overrideMask;
            set => overrideMask = value;
        }

        public bool Additive
        {
            get => additive;
            set => additive = value;
        }

        public IReadOnlyList<AnimationCurveChannel> NamedCurves => namedCurves;
        public override AnimationClip MainClip => animationClip;
        public override bool IsValid => TryValidateForPlayback(out _);

        public bool TryInitialize(AnimationClip clip)
        {
            if (animationClip != null || clip == null)
            {
                return false;
            }

            animationClip = clip;
            return true;
        }

        public bool TryValidateForPlayback(out string error)
        {
            if (animationClip == null)
            {
                error = "AnimationClipAsset requires an AnimationClip.";
                return false;
            }

            if (animationClip.legacy)
            {
                error = $"Animation clip '{animationClip.name}' must not use Legacy animation.";
                return false;
            }

            if (Mathf.Approximately(speed, 0f))
            {
                error = $"Animation clip '{animationClip.name}' requires a non-zero Speed.";
                return false;
            }

            var curveNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < namedCurves.Count; i++)
            {
                AnimationCurveChannel channel = namedCurves[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.Name))
                {
                    error = $"Animation clip '{animationClip.name}' contains an unnamed curve.";
                    return false;
                }

                if (!curveNames.Add(channel.Name))
                {
                    error = $"Animation clip '{animationClip.name}' contains duplicate curve '{channel.Name}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public AnimationCurveChannel SetNamedCurve(string curveName, AnimationCurve curve)
        {
            if (string.IsNullOrWhiteSpace(curveName))
            {
                throw new ArgumentException("A curve name is required.", nameof(curveName));
            }

            for (int i = 0; i < namedCurves.Count; i++)
            {
                AnimationCurveChannel channel = namedCurves[i];
                if (string.Equals(channel.Name, curveName, StringComparison.Ordinal))
                {
                    channel.SetCurve(curve);
                    return channel;
                }
            }

            var created = new AnimationCurveChannel(curveName, curve);
            namedCurves.Add(created);
            return created;
        }

        public bool TryGetNamedCurve(string curveName, out AnimationCurve curve)
        {
            for (int i = 0; i < namedCurves.Count; i++)
            {
                AnimationCurveChannel channel = namedCurves[i];
                if (string.Equals(channel.Name, curveName, StringComparison.Ordinal))
                {
                    curve = channel.Curve;
                    return true;
                }
            }

            curve = null;
            return false;
        }
    }
}
