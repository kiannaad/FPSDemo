using System;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public abstract class AnimationNotify
    {
        [SerializeField] private string displayName = "Notify";
        [SerializeField] private AnimationNotifyFadeOutPolicy fadeOutPolicy = AnimationNotifyFadeOutPolicy.StopImmediately;

        public string DisplayName
        {
            get => displayName;
            set => displayName = string.IsNullOrWhiteSpace(value) ? "Notify" : value;
        }

        public AnimationNotifyFadeOutPolicy FadeOutPolicy
        {
            get => fadeOutPolicy;
            set => fadeOutPolicy = value;
        }
    }
}
