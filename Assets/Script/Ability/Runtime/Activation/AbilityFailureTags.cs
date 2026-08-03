using System;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public static class AbilityFailureTags
    {
        public static GameplayTag NotFound { get; } = Create("Ability.ActivateFail.NotFound");
        public static GameplayTag Ambiguous { get; } = Create("Ability.ActivateFail.Ambiguous");
        public static GameplayTag RequiredTagMissing { get; } = Create("Ability.ActivateFail.RequiredTagMissing");
        public static GameplayTag BlockedTagPresent { get; } = Create("Ability.ActivateFail.BlockedTagPresent");
        public static GameplayTag InvalidAvatar { get; } = Create("Ability.ActivateFail.InvalidAvatar");
        public static GameplayTag InvalidSource { get; } = Create("Ability.ActivateFail.InvalidSource");
        public static GameplayTag NotLocal { get; } = Create("Ability.ActivateFail.NotLocal");
        public static GameplayTag AlreadyActive { get; } = Create("Ability.ActivateFail.AlreadyActive");
        public static GameplayTag InvalidAbilityTag { get; } = Create("Ability.ActivateFail.InvalidAbilityTag");

        private static GameplayTag Create(string name)
        {
            if (!GameplayTag.TryCreateSerialized(name, out GameplayTag tag))
            {
                throw new InvalidOperationException($"Invalid built-in Ability failure tag: {name}");
            }

            return tag;
        }
    }
}
