using System;

namespace CGame
{
    public sealed class AIControllerBinding : ICharacterControllerBinding
    {
        private AIRuntimeRegistry registry;
        private AIRuntimeRegistration runtimeRegistration;
        private AIController controller;
        private IControllerRegistration controllerRegistration;
        private HealthComponent health;
        private WeaponRuntimeBehaviour weaponRuntime;
        private bool isStopped;

        internal AIControllerBinding(
            AIRuntimeRegistry registry,
            AIRuntimeRegistration runtimeRegistration,
            AIController controller,
            IControllerRegistration controllerRegistration,
            HealthComponent health,
            WeaponRuntimeBehaviour weaponRuntime)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.runtimeRegistration = runtimeRegistration ?? throw new ArgumentNullException(nameof(runtimeRegistration));
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.controllerRegistration = controllerRegistration ?? throw new ArgumentNullException(nameof(controllerRegistration));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.weaponRuntime = weaponRuntime ?? throw new ArgumentNullException(nameof(weaponRuntime));
            health.Died += OnDied;
        }

        public bool IsActive => runtimeRegistration != null && runtimeRegistration.IsActive;

        public void Dispose()
        {
            AIRuntimeRegistry currentRegistry = registry;
            AIRuntimeRegistration currentRuntime = runtimeRegistration;
            HealthComponent currentHealth = health;

            registry = null;
            runtimeRegistration = null;
            health = null;
            if (currentHealth != null)
            {
                currentHealth.Died -= OnDied;
            }

            Stop();
            currentRegistry?.Remove(currentRuntime.RuntimeId, currentRuntime);
        }

        private void OnDied(DamageEvent damageEvent)
        {
            Stop();
        }

        private void Stop()
        {
            if (isStopped)
            {
                return;
            }

            isStopped = true;
            controller?.SubmitControlFrame(default);
            controller?.SettingCombatIntentSink(null);
            controller?.UnpossessingPawn();
            controller = null;
            controllerRegistration?.Dispose();
            controllerRegistration = null;
            weaponRuntime?.SubmitCombatIntent(default);
            weaponRuntime?.Shutdown();
            weaponRuntime = null;
            runtimeRegistration?.Deactivate();
        }
    }
}
