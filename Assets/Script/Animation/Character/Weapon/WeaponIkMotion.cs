using System;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Weapon IK Motion", fileName = "WeaponIkMotion")]
    public sealed class WeaponIkMotion : ScriptableObject
    {
        [SerializeField] private AnimationClipAsset animation;
        [SerializeField, Min(0f)] private float blendDuration = 0.15f;

        public AnimationClipAsset Animation => animation;

        public float BlendDuration => blendDuration;

        public void Configure(AnimationClipAsset clip, float duration)
        {
            animation = clip ?? throw new ArgumentNullException(nameof(clip));
            blendDuration = Mathf.Max(0f, duration);
        }

        public void Validate()
        {
            if (animation == null || !animation.IsValid)
            {
                throw new InvalidOperationException("Weapon IK Motion requires a valid AnimationClipAsset.");
            }
        }
    }
}
