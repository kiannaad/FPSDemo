using System;
using CGame.Animation;

namespace CGame.Ability.Animation
{
    public interface IAbilityAnimationPlayer
    {
        event Action Updated;
        IAbilityAnimationPlayback PlayAnimation(AnimationClipAsset asset, long requestId);
        bool StopAnimation(IAbilityAnimationPlayback playback);
    }
}
