using System;

namespace CGame.Ability
{
    public sealed class AbilityGameEventRegistration : IDisposable
    {
        private AbilitySystemComponent abilitySystem;
        private readonly long registrationId;

        internal AbilityGameEventRegistration(AbilitySystemComponent abilitySystem, long registrationId)
        {
            this.abilitySystem = abilitySystem;
            this.registrationId = registrationId;
        }

        public bool IsActive =>
            abilitySystem != null &&
            abilitySystem.IsGameEventRegistrationActive(registrationId);

        public void Dispose()
        {
            AbilitySystemComponent current = abilitySystem;
            abilitySystem = null;
            current?.UnregisterGameEvent(registrationId);
        }
    }
}
