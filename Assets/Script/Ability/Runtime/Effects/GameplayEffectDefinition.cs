using System;
using System.Collections.Generic;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.Ability.Effects
{
    [CreateAssetMenu(menuName = "CGame/Ability/Gameplay Effect Definition")]
    public sealed class GameplayEffectDefinition : ScriptableObject
    {
        [SerializeField] private GameplayEffectDurationPolicy durationPolicy = GameplayEffectDurationPolicy.Instant;
        [SerializeField] private GameplayTag executedCueTag;
        [SerializeField] private List<GameplayEffectModifierDefinition> modifiers = new List<GameplayEffectModifierDefinition>();

        public GameplayEffectDurationPolicy DurationPolicy => durationPolicy;
        public GameplayTag ExecutedCueTag => executedCueTag;
        public IReadOnlyList<GameplayEffectModifierDefinition> Modifiers => modifiers;

        public void ConfigureForTests(
            GameplayEffectDurationPolicy policy,
            params GameplayEffectModifierDefinition[] modifiers)
        {
            Configure(policy, GameplayTag.Empty, modifiers);
        }

        public void ConfigureForTests(
            GameplayEffectDurationPolicy policy,
            GameplayTag cueTag,
            params GameplayEffectModifierDefinition[] modifiers)
        {
            Configure(policy, cueTag, modifiers);
        }

        public void Configure(
            GameplayEffectDurationPolicy policy,
            GameplayTag cueTag,
            params GameplayEffectModifierDefinition[] modifiers)
        {
            durationPolicy = policy;
            executedCueTag = cueTag;
            this.modifiers = modifiers == null
                ? new List<GameplayEffectModifierDefinition>()
                : new List<GameplayEffectModifierDefinition>(modifiers);
        }
    }
}
