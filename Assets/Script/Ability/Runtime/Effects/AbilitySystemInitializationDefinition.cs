using System;
using CGame.Ability.Attributes;
using UnityEngine;

namespace CGame.Ability.Effects
{
    [CreateAssetMenu(menuName = "CGame/Ability/Ability System Initialization Definition")]
    public sealed class AbilitySystemInitializationDefinition : ScriptableObject
    {
        [SerializeField] private AttributeSetDefinition[] attributeSets = Array.Empty<AttributeSetDefinition>();
        [SerializeField] private GameplayEffectDefinition[] initializationEffects = Array.Empty<GameplayEffectDefinition>();

        public void Initialize(AbilitySystemComponent abilitySystem)
        {
            if (abilitySystem == null)
            {
                throw new ArgumentNullException(nameof(abilitySystem));
            }

            if (abilitySystem.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(abilitySystem));
            }

            try
            {
                foreach (AttributeSetDefinition setDefinition in attributeSets)
                {
                    if (setDefinition == null)
                    {
                        throw new InvalidOperationException($"Initialization definition {name} contains a missing AttributeSetDefinition.");
                    }

                    abilitySystem.AddAttributeSet(setDefinition.CreateSet());
                }

                foreach (GameplayEffectDefinition effectDefinition in initializationEffects)
                {
                    if (effectDefinition == null)
                    {
                        throw new InvalidOperationException($"Initialization definition {name} contains a missing GameplayEffectDefinition.");
                    }

                    var context = new GameplayEffectContext(
                        abilitySystem.Owner,
                        abilitySystem.Avatar,
                        this);
                    GameplayEffectSpec spec = abilitySystem.MakeOutgoingSpec(effectDefinition, 1f, context);
                    GameplayEffectApplyResult result = abilitySystem.ApplyGameplayEffectSpecToSelf(spec);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Initialization effect {effectDefinition.name} failed with {result.FailureReason} in {name}.");
                    }
                }
            }
            catch
            {
                abilitySystem.Dispose();
                throw;
            }
        }

        public void ConfigureForTests(
            AttributeSetDefinition[] setDefinitions,
            GameplayEffectDefinition[] effectDefinitions)
        {
            Configure(setDefinitions, effectDefinitions);
        }

        public void Configure(
            AttributeSetDefinition[] setDefinitions,
            GameplayEffectDefinition[] effectDefinitions)
        {
            attributeSets = setDefinitions == null
                ? Array.Empty<AttributeSetDefinition>()
                : (AttributeSetDefinition[])setDefinitions.Clone();
            initializationEffects = effectDefinitions == null
                ? Array.Empty<GameplayEffectDefinition>()
                : (GameplayEffectDefinition[])effectDefinitions.Clone();
        }
    }
}
