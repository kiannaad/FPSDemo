using System;
using System.Collections.Generic;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public abstract class AbilityDefinition
    {
        private readonly GameplayTag[] activationOwnedTags;
        private readonly GameplayTag[] requiredOwnedTags;
        private readonly GameplayTag[] blockedOwnedTags;

        protected AbilityDefinition(
            GameplayTag abilityTag,
            IEnumerable<GameplayTag> activationOwnedTags = null,
            IEnumerable<GameplayTag> requiredOwnedTags = null,
            IEnumerable<GameplayTag> blockedOwnedTags = null)
        {
            AbilityTag = abilityTag;
            this.activationOwnedTags = Copy(activationOwnedTags);
            this.requiredOwnedTags = Copy(requiredOwnedTags);
            this.blockedOwnedTags = Copy(blockedOwnedTags);
        }

        public GameplayTag AbilityTag { get; }
        public IReadOnlyList<GameplayTag> ActivationOwnedTags => activationOwnedTags;
        public IReadOnlyList<GameplayTag> RequiredOwnedTags => requiredOwnedTags;
        public IReadOnlyList<GameplayTag> BlockedOwnedTags => blockedOwnedTags;

        protected abstract AbilityInstance CreateInstance();

        internal AbilityInstance CreateInstanceForSpec()
        {
            return CreateInstance();
        }

        private static GameplayTag[] Copy(IEnumerable<GameplayTag> tags)
        {
            return tags == null ? Array.Empty<GameplayTag>() : new List<GameplayTag>(tags).ToArray();
        }
    }
}
