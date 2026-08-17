using UnityEngine;

namespace CGame.Animation
{
    public interface IAnimationAsset
    {
        AnimationClip MainClip { get; }
        bool IsValid { get; }

    }
}
