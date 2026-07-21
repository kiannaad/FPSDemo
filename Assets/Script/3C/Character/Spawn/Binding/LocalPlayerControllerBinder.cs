using System;

namespace CGame
{
    public sealed class LocalPlayerControllerBinder
    {
        private readonly InputManager inputManager;
        private readonly ControllerManager controllerManager;

        public LocalPlayerControllerBinder(InputManager inputManager, ControllerManager controllerManager)
        {
            this.inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
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
            try
            {
                controller.SettingInputHandle(inputManager.GetHandle(inputType));
                controller.PossessingPawn(pawn);
                return new LocalPlayerControllerBinding(controller, registration);
            }
            catch
            {
                controller.SettingInputHandle(null);
                registration.Dispose();
                throw;
            }
        }
    }
}
