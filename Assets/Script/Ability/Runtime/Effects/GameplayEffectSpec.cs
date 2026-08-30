using System;
using System.Collections.Generic;
using System.Linq;
using CGame.Ability.Attributes;

namespace CGame.Ability.Effects
{
    public sealed class GameplayEffectSpec
    {
        internal GameplayEffectSpec(
            GameplayEffectDefinition definition,
            float level,
            GameplayEffectContext context,
            GameplayEffectFailureReason failureReason,
            IReadOnlyList<EvaluatedGameplayEffectModifier> modifiers)
        {
            Definition = definition;
            Level = level;
            Context = context;
            FailureReason = failureReason;
            Modifiers = modifiers ?? Array.Empty<EvaluatedGameplayEffectModifier>();
        }

        public GameplayEffectDefinition Definition { get; }
        public float Level { get; }
        public GameplayEffectContext Context { get; }
        public GameplayEffectFailureReason FailureReason { get; }
        public bool IsValid => FailureReason == GameplayEffectFailureReason.None;
        internal IReadOnlyList<EvaluatedGameplayEffectModifier> Modifiers { get; }

        public GameplayEffectSpec CloneWithContext(GameplayEffectContext context)
        {
            return new GameplayEffectSpec(
                Definition,
                Level,
                context,
                FailureReason,
                Modifiers.ToArray());
        }
    }

    internal readonly struct EvaluatedGameplayEffectModifier
    {
        public EvaluatedGameplayEffectModifier(
            GameplayAttribute targetAttribute,
            GameplayEffectModifierOperation operation,
            float magnitude)
        {
            TargetAttribute = targetAttribute;
            Operation = operation;
            Magnitude = magnitude;
        }

        public GameplayAttribute TargetAttribute { get; }
        public GameplayEffectModifierOperation Operation { get; }
        public float Magnitude { get; }
    }
}
