using System;
using CGame.Ability.Attributes;
using UnityEngine;

namespace CGame.Ability.Effects
{
    [Serializable]
    public sealed class GameplayEffectModifierDefinition
    {
        [SerializeField] private GameplayAttributeId targetAttributeId;
        [SerializeField] private GameplayEffectModifierOperation operation;
        [SerializeField] private GameplayEffectMagnitudeSource magnitudeSource;
        [SerializeField] private float constantMagnitude;
        [SerializeField] private GameplayAttributeId sourceAttributeId;
        [NonSerialized] private GameplayAttribute runtimeTargetAttribute;
        [NonSerialized] private GameplayAttribute runtimeSourceAttribute;

        private GameplayEffectModifierDefinition()
        {
        }

        private GameplayEffectModifierDefinition(
            GameplayAttribute targetAttribute,
            GameplayEffectModifierOperation operation,
            GameplayEffectMagnitudeSource magnitudeSource,
            float constantMagnitude,
            GameplayAttribute sourceAttribute)
        {
            if (targetAttribute == null) throw new ArgumentNullException(nameof(targetAttribute));
            if (!GameplayAttributeRegistry.TryGetId(targetAttribute, out targetAttributeId))
            {
                runtimeTargetAttribute = targetAttribute;
            }
            this.operation = operation;
            this.magnitudeSource = magnitudeSource;
            this.constantMagnitude = constantMagnitude;
            if (sourceAttribute != null && !GameplayAttributeRegistry.TryGetId(sourceAttribute, out sourceAttributeId))
            {
                runtimeSourceAttribute = sourceAttribute;
            }
        }

        public GameplayAttribute TargetAttribute => runtimeTargetAttribute ?? GameplayAttributeRegistry.Resolve(targetAttributeId);
        public GameplayEffectModifierOperation Operation => operation;
        public GameplayEffectMagnitudeSource MagnitudeSource => magnitudeSource;
        public float ConstantMagnitude => constantMagnitude;
        public GameplayAttribute SourceAttributeValue => runtimeSourceAttribute ?? GameplayAttributeRegistry.Resolve(sourceAttributeId);

        public static GameplayEffectModifierDefinition Constant(
            GameplayAttribute targetAttribute,
            GameplayEffectModifierOperation operation,
            float magnitude)
        {
            if (float.IsNaN(magnitude) || float.IsInfinity(magnitude))
            {
                throw new ArgumentOutOfRangeException(nameof(magnitude), magnitude, "Modifier magnitude must be finite.");
            }

            return new GameplayEffectModifierDefinition(
                targetAttribute,
                operation,
                GameplayEffectMagnitudeSource.Constant,
                magnitude,
                null);
        }

        public static GameplayEffectModifierDefinition SourceAttribute(
            GameplayAttribute targetAttribute,
            GameplayEffectModifierOperation operation,
            GameplayAttribute sourceAttribute)
        {
            return new GameplayEffectModifierDefinition(
                targetAttribute,
                operation,
                GameplayEffectMagnitudeSource.SourceAttribute,
                0f,
                sourceAttribute ?? throw new ArgumentNullException(nameof(sourceAttribute)));
        }
    }
}
