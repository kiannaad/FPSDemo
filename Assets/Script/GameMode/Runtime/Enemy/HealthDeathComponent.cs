using System;

namespace CGame
{
    public sealed class HealthDeathComponent : ActorComponent
    {
        private readonly float maximumHealth;

        public HealthDeathComponent(float health)
        {
            maximumHealth = health > 0f ? health : throw new ArgumentOutOfRangeException(nameof(health));
            CurrentHealth = maximumHealth;
        }

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        public event Action DeathFinished;

        public void ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = System.Math.Max(0f, CurrentHealth - amount);
            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                DeathFinished?.Invoke();
            }
        }

        protected override void OnShutdown() => DeathFinished = null;
    }
}
