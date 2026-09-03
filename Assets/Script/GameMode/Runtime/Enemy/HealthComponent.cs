using System;
using CGame.Ability;
using CGame.Ability.Attributes;

namespace CGame
{
    public sealed class HealthComponent : ActorComponent
    {
        private AbilitySystemComponent abilitySystem;
        private HealthSet healthSet;

        public float Health => healthSet?.Health.CurrentValue ?? 0f;
        public float MaxHealth => healthSet?.MaxHealth.CurrentValue ?? 0f;
        public bool IsBound => healthSet != null;
        public bool IsDead { get; private set; }
        public long AppliedAuthoritativeRevision { get; private set; } = -1;

        public event Action<float, float> HealthChanged;
        public event Action DeathStarted;
        public event Action DeathFinished;

        public void Bind(AbilitySystemComponent targetAbilitySystem)
        {
            if (targetAbilitySystem == null)
            {
                throw new ArgumentNullException(nameof(targetAbilitySystem));
            }

            HealthSet targetHealthSet = targetAbilitySystem.GetSet<HealthSet>()
                ?? throw new InvalidOperationException("AbilitySystemComponent must contain HealthSet before HealthComponent binding.");
            if (ReferenceEquals(abilitySystem, targetAbilitySystem) && ReferenceEquals(healthSet, targetHealthSet))
            {
                return;
            }

            Unbind();
            abilitySystem = targetAbilitySystem;
            healthSet = targetHealthSet;
            IsDead = healthSet.Health.CurrentValue <= 0f;
            AppliedAuthoritativeRevision = -1;
            healthSet.HealthChanged += OnHealthChanged;
            healthSet.OutOfHealth += OnOutOfHealth;
        }

        public void Unbind()
        {
            if (healthSet != null)
            {
                healthSet.HealthChanged -= OnHealthChanged;
                healthSet.OutOfHealth -= OnOutOfHealth;
            }

            abilitySystem = null;
            healthSet = null;
            IsDead = false;
            AppliedAuthoritativeRevision = -1;
        }

        public bool ApplyAuthoritativeState(float health, float maxHealth, long revision, bool isDead)
        {
            if (healthSet == null) throw new InvalidOperationException("HealthComponent must be bound before applying authority state.");
            if (revision <= AppliedAuthoritativeRevision) return false;
            if (IsDead) return false;
            if (float.IsNaN(health) || float.IsInfinity(health) ||
                float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) ||
                maxHealth < 0f || health < 0f || health > maxHealth || (isDead && health != 0f))
                throw new ArgumentOutOfRangeException(nameof(health));
            AppliedAuthoritativeRevision = revision;
            healthSet.ApplyAuthoritativeState(health, maxHealth, isDead);
            return true;
        }

        protected override void OnShutdown()
        {
            Unbind();
            HealthChanged = null;
            DeathStarted = null;
            DeathFinished = null;
        }

        private void OnHealthChanged(float oldHealth, float newHealth)
        {
            HealthChanged?.Invoke(oldHealth, newHealth);
        }

        private void OnOutOfHealth()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            if (Owner is Pawn pawn && pawn.Root != null)
            {
                pawn.Root.SetActive(false);
            }

            DeathStarted?.Invoke();
            DeathFinished?.Invoke();
        }
    }
}
