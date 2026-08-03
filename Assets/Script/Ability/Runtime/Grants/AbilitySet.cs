using System;
using System.Collections.Generic;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public sealed class AbilitySet
    {
        private readonly AbilityDefinition[] abilities;
        private readonly GameplayTag[] ownedTags;

        public AbilitySet(
            IEnumerable<AbilityDefinition> abilities = null,
            IEnumerable<GameplayTag> ownedTags = null)
        {
            this.abilities = abilities == null
                ? Array.Empty<AbilityDefinition>()
                : new List<AbilityDefinition>(abilities).ToArray();
            this.ownedTags = ownedTags == null
                ? Array.Empty<GameplayTag>()
                : new List<GameplayTag>(ownedTags).ToArray();

            if (Array.Exists(this.abilities, ability => ability == null))
            {
                throw new ArgumentException("AbilitySet cannot contain a null AbilityDefinition.", nameof(abilities));
            }
        }

        public IReadOnlyList<AbilityDefinition> Abilities => abilities;
        public IReadOnlyList<GameplayTag> OwnedTags => ownedTags;
    }
}
