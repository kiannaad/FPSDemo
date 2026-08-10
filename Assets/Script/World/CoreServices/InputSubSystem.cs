using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame
{
    public sealed class InputSubSystem : PlayerSubSystem, IPlayerInputSource
    {
        private InputService inputService;

        public int TickCount { get; private set; }
        public InputHandle InputHandle => inputService?.GetHandle(InputType.Player);

        public bool FirePressed => ReadState().FirePressed;
        public bool ReloadPressed => ReadState().ReloadPressed;
        public bool MeleePressed => Keyboard.current?.vKey.wasPressedThisFrame == true;

        public int RequestedQuickBarSlot
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard?.digit1Key.wasPressedThisFrame == true) return 0;
                if (keyboard?.digit2Key.wasPressedThisFrame == true) return 1;
                if (keyboard?.digit3Key.wasPressedThisFrame == true) return 2;
                return -1;
            }
        }

        public CharacterControlIntent ReadControlIntent()
        {
            PlayerInputState state = ReadState();
            return new CharacterControlIntent(
                new Vector3(state.MoveInput.x, 0f, state.MoveInput.y),
                state.JumpPressed,
                state.SprintHeld);
        }

        public Vector2 ReadLookDelta(float deltaTime) => ReadState().LookInput.ResolveFrameDelta(deltaTime);

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            inputService = new InputService();
            inputService.Initialize();
            AddTickTask("Player.Input", TickGroup.TG_Input, Tick);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync()
        {
            inputService?.Shutdown();
            inputService = null;
            return Task.CompletedTask;
        }

        private void Tick(float deltaTime)
        {
            inputService?.Tick(deltaTime);
            TickCount++;
        }

        private PlayerInputState ReadState()
        {
            return inputService == null
                ? default
                : inputService.GetHandle(InputType.Player).GetState<PlayerInputState>();
        }
    }
}
