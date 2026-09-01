using System;
using CGame.Ability.Effects;

namespace CGame.Ability.Attributes
{
    public sealed class HealthSet : AttributeSet
    {
        public static GameplayAttribute HealthAttribute { get; } =
            GameplayAttribute.Create<HealthSet>(nameof(Health), set => set.Health);
        public static GameplayAttribute MaxHealthAttribute { get; } =
            GameplayAttribute.Create<HealthSet>(nameof(MaxHealth), set => set.MaxHealth);
        public static GameplayAttribute DamageAttribute { get; } =
            GameplayAttribute.Create<HealthSet>(nameof(Damage), set => set.Damage);

        public HealthSet(float health = 0f, float maxHealth = 0f, float damage = 0f)
        {
            Health = new GameplayAttributeData(health);
            MaxHealth = new GameplayAttributeData(maxHealth);
            Damage = new GameplayAttributeData(damage);
        }

        public GameplayAttributeData Health { get; }
        public GameplayAttributeData MaxHealth { get; }
        public GameplayAttributeData Damage { get; }

        public event Action<float, float> HealthChanged;
        public event Action OutOfHealth;

        protected internal override void PostGameplayEffectExecute(
            GameplayAttribute attribute,
            GameplayEffectContext context,
            IGameplayEffectExecution execution)
        {
            if (!ReferenceEquals(attribute, DamageAttribute))
            {
                return;
            }

            float oldHealth = Health.CurrentValue;
            float damage = Math.Max(0f, Damage.CurrentValue);
            float maxHealth = Math.Max(0f, MaxHealth.CurrentValue);
            float newHealth = Math.Max(0f, Math.Min(maxHealth, oldHealth - damage));
            execution.SetCurrentValue(Health, newHealth);
            execution.SetCurrentValue(Damage, 0f);

            if (newHealth != oldHealth)
            {
                execution.QueueEvent(() => HealthChanged?.Invoke(oldHealth, newHealth));
            }

            if (oldHealth > 0f && newHealth <= 0f)
            {
                execution.QueueEvent(() => OutOfHealth?.Invoke());
            }
        }
    }
}
