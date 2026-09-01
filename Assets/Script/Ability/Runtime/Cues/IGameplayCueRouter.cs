using CGame.GameplayTags;

namespace CGame.Ability.Cues
{
    public interface IGameplayCueRouter
    {
        void Execute(GameplayTag cueTag, GameplayCueParameters parameters);
        GameplayCueHandle Add(GameplayTag cueTag, GameplayCueParameters parameters);
        bool Remove(GameplayCueHandle handle);
        void RemoveForTarget(object target);
    }
}
