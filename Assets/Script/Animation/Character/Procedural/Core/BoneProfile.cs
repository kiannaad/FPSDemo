using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Bone Profile", fileName = "BoneProfile")]
    public sealed class BoneProfile : ScriptableObject
    {
        [SerializeField] private KRig rig;
        [SerializeField] private List<AnimationLayerSettings> layers = new List<AnimationLayerSettings>();
        [SerializeField, Min(0f)] private float blendIn = 0.15f;
        [SerializeField, Min(0f)] private float blendOut = 0.15f;
        [SerializeField] private EaseMode easeMode = EaseMode.EaseInOut;
        [SerializeField] private float evaluationTimeout = 5f;

        public KRig Rig => rig;
        public IReadOnlyList<AnimationLayerSettings> Layers => layers;
        public float BlendIn => blendIn;
        public float BlendOut => blendOut;
        public EaseMode EaseMode => easeMode;
        public float EvaluationTimeout => evaluationTimeout;

        public void Configure(KRig profileRig, IEnumerable<AnimationLayerSettings> profileLayers)
        {
            rig = profileRig ?? throw new ArgumentNullException(nameof(profileRig));
            layers = profileLayers == null
                ? throw new ArgumentNullException(nameof(profileLayers))
                : new List<AnimationLayerSettings>(profileLayers);
        }

        public void Validate(KRig expectedRig)
        {
            if (expectedRig == null)
            {
                throw new ArgumentNullException(nameof(expectedRig));
            }

            if (rig != expectedRig)
            {
                throw new InvalidOperationException("Bone Profile must reference the Pawn KRig.");
            }

            if (layers == null)
            {
                throw new InvalidOperationException("Bone Profile layer collection is missing.");
            }

            if (blendIn < 0f || blendOut < 0f)
            {
                throw new InvalidOperationException("Bone Profile blend durations cannot be negative.");
            }

            if (evaluationTimeout <= 0f)
            {
                throw new InvalidOperationException("Bone Profile evaluation timeout must be positive.");
            }

            HashSet<AnimationLayerSettings> uniqueSettings = new HashSet<AnimationLayerSettings>();
            bool hasTurnLayer = false;
            foreach (AnimationLayerSettings settings in layers)
            {
                if (settings == null)
                {
                    throw new InvalidOperationException("Bone Profile contains a missing layer setting.");
                }

                if (!uniqueSettings.Add(settings))
                {
                    throw new InvalidOperationException("Bone Profile contains a duplicate layer setting reference.");
                }

                if (settings is TurnLayerSettings)
                {
                    if (hasTurnLayer)
                    {
                        throw new InvalidOperationException("Bone Profile cannot contain more than one Turn Layer.");
                    }

                    hasTurnLayer = true;
                }

                settings.Validate(expectedRig);
            }
        }
    }
}
