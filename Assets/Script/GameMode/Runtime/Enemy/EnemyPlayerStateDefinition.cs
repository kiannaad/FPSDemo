using System;
using CGame.Ability.Attributes;
using CGame.Ability.Effects;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "EnemyPlayerStateDefinition", menuName = "CGame/Gameplay/Enemy Player State Definition")]
    public sealed class EnemyPlayerStateDefinition : ScriptableObject
    {
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private AbilitySystemInitializationDefinition abilitySystemInitialization;
        [NonSerialized] private AbilitySystemInitializationDefinition runtimeAbilitySystemInitialization;

        public GameObject PawnPrefab => pawnPrefab;
        public AbilitySystemInitializationDefinition AbilitySystemInitialization =>
            runtimeAbilitySystemInitialization != null ? runtimeAbilitySystemInitialization : abilitySystemInitialization;

        public void Configure(GameObject prefab, float health = 100f)
        {
            pawnPrefab = prefab ?? throw new System.ArgumentNullException(nameof(prefab));
            if (health <= 0f) throw new System.ArgumentOutOfRangeException(nameof(health));
            runtimeAbilitySystemInitialization = CreateRuntimeInitialization(health);
        }

        public void Configure(GameObject prefab, AbilitySystemInitializationDefinition initialization)
        {
            pawnPrefab = prefab ?? throw new System.ArgumentNullException(nameof(prefab));
            abilitySystemInitialization = initialization ?? throw new System.ArgumentNullException(nameof(initialization));
            runtimeAbilitySystemInitialization = null;
        }

        private static AbilitySystemInitializationDefinition CreateRuntimeInitialization(float health)
        {
            var healthSetDefinition = CreateInstance<AttributeSetDefinition>();
            healthSetDefinition.ConfigureForTests(AttributeSetKind.Health);
            var combatSetDefinition = CreateInstance<AttributeSetDefinition>();
            combatSetDefinition.ConfigureForTests(AttributeSetKind.Combat);
            var initializationEffect = CreateInstance<GameplayEffectDefinition>();
            initializationEffect.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(HealthSet.MaxHealthAttribute, GameplayEffectModifierOperation.Override, health),
                GameplayEffectModifierDefinition.Constant(HealthSet.HealthAttribute, GameplayEffectModifierOperation.Override, health),
                GameplayEffectModifierDefinition.Constant(CombatSet.BaseDamageAttribute, GameplayEffectModifierOperation.Override, 10f));
            var initialization = CreateInstance<AbilitySystemInitializationDefinition>();
            initialization.ConfigureForTests(
                new[] { healthSetDefinition, combatSetDefinition },
                new[] { initializationEffect });
            return initialization;
        }
    }
}
