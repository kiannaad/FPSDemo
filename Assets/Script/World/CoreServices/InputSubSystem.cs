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
        public bool GameplayInputEnabled { get; private set; } = true;
        public void SetGameplayInputEnabled(bool enabled) => GameplayInputEnabled = enabled;
        public InputHandle InputHandle => inputService?.GetHandle(InputType.Player);

        public bool FirePressed => HasFocusedInput && ReadState().FirePressed;
        public bool ReloadPressed => HasFocusedInput && ReadState().ReloadPressed;
        public bool MeleePressed => HasFocusedInput && Keyboard.current?.vKey.wasPressedThisFrame == true;

        public int RequestedQuickBarSlot
        {
            get
            {
                if (!HasFocusedInput) return -1;
                Keyboard keyboard = Keyboard.current;
                if (keyboard?.digit1Key.wasPressedThisFrame == true
                    || keyboard?.numpad1Key.wasPressedThisFrame == true) return 0;
                if (keyboard?.digit2Key.wasPressedThisFrame == true
                    || keyboard?.numpad2Key.wasPressedThisFrame == true) return 1;
                if (keyboard?.digit3Key.wasPressedThisFrame == true
                    || keyboard?.numpad3Key.wasPressedThisFrame == true) return 2;
                return -1;
            }
        }

        public CharacterControlIntent ReadControlIntent()
        {
            if (!HasFocusedInput) return default;
            PlayerInputState state = ReadState();
            return new CharacterControlIntent(
                new Vector3(state.MoveInput.x, 0f, state.MoveInput.y),
                state.JumpPressed,
                state.SprintHeld);
        }

        public Vector2 ReadLookDelta(float deltaTime) => HasFocusedInput
            ? ReadState().LookInput.ResolveFrameDelta(deltaTime)
            : Vector2.zero;

        private bool HasFocusedInput => GameplayInputEnabled && Application.isFocused;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameplayInputEnabled = true;
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
