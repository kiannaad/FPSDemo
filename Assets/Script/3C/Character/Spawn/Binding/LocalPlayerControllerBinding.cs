using System;

namespace CGame
{
    public sealed class LocalPlayerControllerBinding : ICharacterControllerBinding
    {
        private PlayerController controller;
        private IControllerRegistration registration;
        private PlayerStateAvatarBinding avatarBinding;

        internal LocalPlayerControllerBinding(
            PlayerController controller,
            IControllerRegistration registration,
            PlayerStateAvatarBinding avatarBinding)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.registration = registration ?? throw new ArgumentNullException(nameof(registration));
            this.avatarBinding = avatarBinding ?? throw new ArgumentNullException(nameof(avatarBinding));
        }

        internal PlayerController Controller => controller;
        public bool IsActive =>
            controller != null &&
            registration != null &&
            registration.IsActive &&
            avatarBinding != null &&
            avatarBinding.IsActive;
        public WeaponRuntime WeaponRuntime => controller?.WeaponRuntime;

        public void Dispose()
        {
            PlayerController currentController = controller;
            IControllerRegistration currentRegistration = registration;
            PlayerStateAvatarBinding currentAvatarBinding = avatarBinding;
            controller = null;
            registration = null;
            avatarBinding = null;
            currentAvatarBinding?.Dispose();
            currentController?.SettingInputHandle(null);
            currentController?.UnpossessingPawn();
            currentRegistration?.Dispose();
        }
    }
}
