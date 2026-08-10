using System.Threading;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace CGame
{
    public sealed class InputWorldCoreService :
        IWorldCoreService,
        IWorldTickCoreService,
        IPlayerInputSource
    {
        private InputService inputService;

        public string Name => "Input";

        public TickGroup TickGroup => TickGroup.TG_Input;

        public int TickCount { get; private set; }

        public bool FirePressed => ReadState().FirePressed;

        public bool ReloadPressed => ReadState().ReloadPressed;

        public bool MeleePressed => Keyboard.current?.vKey.wasPressedThisFrame == true;

        public int RequestedQuickBarSlot
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard?.digit1Key.wasPressedThisFrame == true)
                {
                    return 0;
                }

                if (keyboard?.digit2Key.wasPressedThisFrame == true)
                {
                    return 1;
                }

                if (keyboard?.digit3Key.wasPressedThisFrame == true)
                {
                    return 2;
                }

                return -1;
            }
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            inputService = new InputService();
            inputService.Initialize();
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            inputService?.Shutdown();
            inputService = null;
            return Task.CompletedTask;
        }

        public void Tick(float deltaTime)
        {
            inputService?.Tick(deltaTime);
            TickCount++;
        }

        public CharacterControlIntent ReadControlIntent()
        {
            PlayerInputState state = ReadState();
            return new CharacterControlIntent(
                new UnityEngine.Vector3(state.MoveInput.x, 0f, state.MoveInput.y),
                state.JumpPressed,
                state.SprintHeld);
        }

        public UnityEngine.Vector2 ReadLookDelta(float deltaTime)
        {
            return ReadState().LookInput.ResolveFrameDelta(deltaTime);
        }

        private PlayerInputState ReadState()
        {
            return inputService == null
                ? default
                : inputService.GetHandle(InputType.Player).GetState<PlayerInputState>();
        }
    }
}
