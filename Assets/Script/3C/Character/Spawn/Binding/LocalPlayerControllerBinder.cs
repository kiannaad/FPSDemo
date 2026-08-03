using System;

namespace CGame
{
    public sealed class LocalPlayerControllerBinder
    {
        private readonly InputManager inputManager;
        private readonly ControllerManager controllerManager;
        private readonly PlayerStateManager playerStateManager;

        public LocalPlayerControllerBinder(
            InputManager inputManager,
            ControllerManager controllerManager,
            PlayerStateManager playerStateManager)
        {
            this.inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
            this.playerStateManager = playerStateManager ?? throw new ArgumentNullException(nameof(playerStateManager));
        }

        public LocalPlayerControllerBinding Bind(Pawn pawn, InputType inputType)
        {
            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            if (inputType != InputType.Player)
            {
                throw new ArgumentException("Local player characters require the Player input type.", nameof(inputType));
            }

            PlayerController controller = controllerManager.CreateController<PlayerController>(out IControllerRegistration registration);
            PlayerStateAvatarBinding avatarBinding = null;
            try
            {
                controller.SettingInputHandle(inputManager.GetHandle(inputType));
                controller.PossessingPawn(pawn);
                avatarBinding = playerStateManager.BindAvatar(pawn);
                return new LocalPlayerControllerBinding(controller, registration, avatarBinding);
            }
            catch
            {
                avatarBinding?.Dispose();
                controller.SettingInputHandle(null);
                controller.UnpossessingPawn();
                registration.Dispose();
                throw;
            }
        }
    }
}
