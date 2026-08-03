using CGame.Ability.Animation;
using CGame.Animation;

namespace CGame
{
    public interface IWeaponSwitchPresentation : IAbilityAnimationPlayer
    {
        IAbilityAnimationPlayback PlayPose(
            AnimationClipAsset asset,
            long requestId);

        IWeaponSwitchPresentationReplacement PrepareReplacement(
            WeaponAnimationDefinition definition,
            uint generation);

        void PromotePose(IAbilityAnimationPlayback playback);
    }
}
