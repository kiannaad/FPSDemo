using System;
using System.Collections.Generic;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public sealed class AbilitySet
    {
        private readonly AbilityGrantDefinition[] grants;
        private readonly GameplayTag[] ownedTags;

        public AbilitySet(
            IEnumerable<AbilityDefinition> abilities = null,
            IEnumerable<GameplayTag> ownedTags = null)
        {
            grants = abilities == null
                ? Array.Empty<AbilityGrantDefinition>()
                : new List<AbilityDefinition>(abilities).ConvertAll(definition => new AbilityGrantDefinition(definition)).ToArray();
            this.ownedTags = ownedTags == null
                ? Array.Empty<GameplayTag>()
                : new List<GameplayTag>(ownedTags).ToArray();

            if (Array.Exists(grants, grant => grant == null))
            {
                throw new ArgumentException("AbilitySet cannot contain a null AbilityDefinition.", nameof(abilities));
            }
        }

        public AbilitySet(IEnumerable<AbilityGrantDefinition> grants, IEnumerable<GameplayTag> ownedTags = null)
        {
            this.grants = grants == null ? Array.Empty<AbilityGrantDefinition>() : new List<AbilityGrantDefinition>(grants).ToArray();
            this.ownedTags = ownedTags == null ? Array.Empty<GameplayTag>() : new List<GameplayTag>(ownedTags).ToArray();

            if (Array.Exists(this.grants, grant => grant == null))
            {
                throw new ArgumentException("AbilitySet cannot contain a null AbilityGrantDefinition.", nameof(grants));
            }
        }

        public IReadOnlyList<AbilityGrantDefinition> Grants => grants;
        public IReadOnlyList<AbilityDefinition> Abilities => Array.ConvertAll(grants, grant => grant.Definition);
        public IReadOnlyList<GameplayTag> OwnedTags => ownedTags;
    }
}
