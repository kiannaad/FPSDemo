using System;
using CGame.GameplayTags;

namespace CGame.Ability
{
    public sealed class AbilityGrantDefinition
    {
        public AbilityGrantDefinition(AbilityDefinition definition, GameplayTag inputTag = default)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            InputTag = inputTag;
        }

        public AbilityDefinition Definition { get; }
        public GameplayTag InputTag { get; }
    }
}
