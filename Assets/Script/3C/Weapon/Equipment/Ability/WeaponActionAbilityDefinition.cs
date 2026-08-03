using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;

namespace CGame
{
    public sealed class WeaponActionAbilityDefinition : AbilityDefinition
    {
        public WeaponActionAbilityDefinition(
            GameplayTag abilityTag,
            GameplayTag eventTag,
            WeaponActionKind actionKind,
            AnimationClipAsset animation)
            : base(
                abilityTag,
                activationOwnedTags: new[] { WeaponActionGameplayTags.ActionState },
                blockedOwnedTags: new[] { WeaponActionGameplayTags.ActionState })
        {
            EventTag = eventTag;
            ActionKind = actionKind;
            Animation = animation;
        }

        public GameplayTag EventTag { get; }
        public WeaponActionKind ActionKind { get; }
        public AnimationClipAsset Animation { get; }

        protected override AbilityInstance CreateInstance()
        {
            return new WeaponActionAbilityInstance(this);
        }
    }
}
