namespace CGame.Ability.Animation
{
    public interface IAbilityAnimationPlayback
    {
        AbilityAnimationPlaybackState State { get; }
        bool IsTerminal { get; }
    }
}
