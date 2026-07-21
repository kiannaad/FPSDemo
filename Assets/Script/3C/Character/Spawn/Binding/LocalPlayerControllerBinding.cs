using System;

namespace CGame
{
    public sealed class LocalPlayerControllerBinding : ICharacterControllerBinding
    {
        private PlayerController controller;
        private IControllerRegistration registration;

        internal LocalPlayerControllerBinding(PlayerController controller, IControllerRegistration registration)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.registration = registration ?? throw new ArgumentNullException(nameof(registration));
        }

        internal PlayerController Controller => controller;
        public bool IsActive => controller != null && registration != null && registration.IsActive;

        public void Dispose()
        {
            PlayerController currentController = controller;
            IControllerRegistration currentRegistration = registration;
            controller = null;
            registration = null;
            currentController?.SettingInputHandle(null);
            currentController?.UnpossessingPawn();
            currentRegistration?.Dispose();
        }
    }
}
