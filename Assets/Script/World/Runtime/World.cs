using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class World
    {
        private readonly IReadOnlyList<IWorldCoreService> coreServices;
        private readonly List<IWorldCoreService> initializedServices = new List<IWorldCoreService>();
        private readonly GameLauncher gameLauncher;
        private readonly TickFunctionHandle motorStepTick;
        private readonly TickFunctionHandle physicsEventTick;
        private readonly TickFunctionHandle motorPresentationTick;
        private readonly List<IWorldController> controllers = new List<IWorldController>();
        private readonly List<TickFunctionHandle> coreServiceTickHandles = new List<TickFunctionHandle>();
        private float currentPresentationTime;
        private long nextGameSessionId;
        private GameSessionId activeSessionId;
        private IWorldSession currentGameSession;

        private World(
            IEnumerable<IWorldCoreService> coreServices,
            GameLauncher gameLauncher,
            TickScheduler tickScheduler,
            ICharacterMotorSimulation characterMotorSimulation)
        {
            this.coreServices = new List<IWorldCoreService>(coreServices ??
                throw new ArgumentNullException(nameof(coreServices)));
            this.gameLauncher = gameLauncher ?? throw new ArgumentNullException(nameof(gameLauncher));
            TickScheduler = tickScheduler ?? new TickScheduler();
            CharacterMotorSimulation = characterMotorSimulation ?? new NullCharacterMotorSimulation();
            foreach (IWorldCoreService service in this.coreServices)
            {
                if (service is IWorldTickCoreService tickService)
                {
                    coreServiceTickHandles.Add(TickScheduler.Register(
                        $"CoreService.{service.Name}",
                        tickService.TickGroup,
                        tickService.Tick,
                        critical: true));
                }
            }
            motorStepTick = TickScheduler.Register(
                "CharacterMotorSimulation.Step",
                TickGroup.TG_CharacterMotorSimulation,
                CharacterMotorSimulation.Step,
                critical: true);
            physicsEventTick = TickScheduler.Register(
                "CharacterMotorSimulation.ConsumePostPhysicsEvents",
                TickGroup.TG_PostPhysics,
                CharacterMotorSimulation.ConsumePostPhysicsEvents,
                critical: true);
            motorPresentationTick = TickScheduler.Register(
                "CharacterMotorSimulation.Present",
                TickGroup.TG_CharacterPresentation,
                ignoredDeltaTime => CharacterMotorSimulation.Present(currentPresentationTime),
                critical: true);
        }

        public static World Current { get; private set; }

        public WorldState State { get; private set; } = WorldState.Created;

        public GameStartRequest? PendingGameStartRequest { get; private set; }

        public string Failure { get; private set; } = string.Empty;

        public TickScheduler TickScheduler { get; }

        public ICharacterMotorSimulation CharacterMotorSimulation { get; }

        public IReadOnlyList<IWorldController> Controllers => controllers;

        public IWorldSession CurrentGameSession => currentGameSession;

        public T GetCoreService<T>() where T : class
        {
            foreach (IWorldCoreService service in coreServices)
            {
                if (service is T match)
                {
                    return match;
                }
            }

            return null;
        }

        public static World Create(
            IEnumerable<IWorldCoreService> coreServices,
            GameLauncher gameLauncher,
            TickScheduler tickScheduler = null,
            ICharacterMotorSimulation characterMotorSimulation = null)
        {
            if (Current != null)
            {
                throw new InvalidOperationException("Only one active World is allowed.");
            }

            World world = new World(coreServices, gameLauncher, tickScheduler, characterMotorSimulation);
            Current = world;
            return world;
        }

        public async Task<WorldStartResult> StartAsync(CancellationToken cancellationToken = default)
        {
            if (State != WorldState.Created)
            {
                throw new InvalidOperationException($"Cannot start World while it is {State}.");
            }

            State = WorldState.Initializing;
            foreach (IWorldCoreService service in coreServices)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await service.InitializeAsync(cancellationToken);
                    initializedServices.Add(service);
                }
                catch (Exception exception)
                {
                    Failure = $"{service?.Name ?? "Unknown core service"}: {exception.Message}";
                    await ShutdownInitializedServicesAsync();
                    State = WorldState.InitializationFailed;
                    return WorldStartResult.Fail(Failure);
                }
            }

            State = WorldState.Launching;
            GameLaunchResult launchResult = await gameLauncher.LaunchAsync(cancellationToken);
            if (!launchResult.Succeeded)
            {
                Failure = launchResult.Failure?.ToString() ?? "Launch failed.";
                State = WorldState.LaunchFailed;
                return WorldStartResult.Fail(Failure);
            }

            PendingGameStartRequest = launchResult.Request;
            return WorldStartResult.Success(launchResult.Request);
        }

        public async Task<WorldStartResult> ReturnToLoginAsync(CancellationToken cancellationToken = default)
        {
            bool hasPendingRequest = State == WorldState.Launching && PendingGameStartRequest.HasValue;
            bool hasActiveSession = (State == WorldState.StartingGame || State == WorldState.Running)
                                    && currentGameSession != null;
            if (!hasPendingRequest && !hasActiveSession)
            {
                throw new InvalidOperationException($"Cannot return to login while World is {State}.");
            }

            State = WorldState.ReturningToLogin;
            PendingGameStartRequest = null;
            ShutdownCurrentSession();
            GameLaunchResult launchResult = await gameLauncher.ReturnToLoginAsync(cancellationToken);
            if (!launchResult.Succeeded)
            {
                Failure = launchResult.Failure?.ToString() ?? "Launch failed.";
                State = WorldState.LaunchFailed;
                return WorldStartResult.Fail(Failure);
            }

            PendingGameStartRequest = launchResult.Request;
            State = WorldState.Launching;
            return WorldStartResult.Success(launchResult.Request);
        }

        public GameSessionStartResult StartGame(IGameSessionFactory sessionFactory)
        {
            if (sessionFactory == null)
            {
                throw new ArgumentNullException(nameof(sessionFactory));
            }

            if (State != WorldState.Launching || !PendingGameStartRequest.HasValue)
            {
                throw new InvalidOperationException($"Cannot start a gameplay session while World is {State}.");
            }

            GameStartRequest request = PendingGameStartRequest.Value;
            GameSessionId sessionId = new GameSessionId(++nextGameSessionId);
            activeSessionId = sessionId;
            State = WorldState.StartingGame;
            try
            {
                IWorldSession session = sessionFactory.Create(this, sessionId, request);
                if (session == null || session.Id != sessionId || !session.IsActive)
                {
                    session?.Shutdown();
                    throw new InvalidOperationException("Game session factory returned an invalid session.");
                }

                currentGameSession = session;
                PendingGameStartRequest = null;
                return GameSessionStartResult.Success(sessionId);
            }
            catch (Exception exception)
            {
                ShutdownControllers();
                activeSessionId = default;
                Failure = exception.Message;
                State = WorldState.GameStartFailed;
                return GameSessionStartResult.Fail(Failure);
            }
        }

        public WorldControllerRegistration RegisterController(
            GameSessionId sessionId,
            IWorldController controller)
        {
            if (!sessionId.IsValid || sessionId != activeSessionId)
            {
                throw new InvalidOperationException("Controller belongs to a stale or inactive GameSessionId.");
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (controller.SessionId != sessionId || !controller.IsActive)
            {
                throw new InvalidOperationException("Only an active Controller from the current session can register.");
            }

            if (controllers.Contains(controller))
            {
                throw new InvalidOperationException("Controller is already registered with this World.");
            }

            controllers.Add(controller);
            return new WorldControllerRegistration(this, controller);
        }

        public bool IsCurrentSession(GameSessionId sessionId)
        {
            return sessionId.IsValid
                && sessionId == activeSessionId
                && currentGameSession != null
                && currentGameSession.IsActive;
        }

        public bool TryRunForSession(GameSessionId sessionId, Action<IWorldSession> callback)
        {
            if (!IsCurrentSession(sessionId))
            {
                return false;
            }

            callback?.Invoke(currentGameSession);
            return true;
        }

        public bool TryEnterRunning(GameSessionId sessionId)
        {
            if (State != WorldState.StartingGame || !IsCurrentSession(sessionId))
            {
                return false;
            }

            State = WorldState.Running;
            return true;
        }

        public bool TryFailGameStart(GameSessionId sessionId, string failure)
        {
            if (State != WorldState.StartingGame || !IsCurrentSession(sessionId))
            {
                return false;
            }

            Failure = string.IsNullOrWhiteSpace(failure) ? "Pawn initialization failed." : failure;
            State = WorldState.GameStartFailed;
            return true;
        }

        public async Task ShutdownAsync()
        {
            if (State == WorldState.Destroyed)
            {
                return;
            }

            State = WorldState.ShuttingDown;
            PendingGameStartRequest = null;
            ShutdownCurrentSession();
            await gameLauncher.ShutdownAsync();
            for (int index = coreServiceTickHandles.Count - 1; index >= 0; index--)
            {
                coreServiceTickHandles[index].Dispose();
            }
            coreServiceTickHandles.Clear();
            motorPresentationTick.Dispose();
            physicsEventTick.Dispose();
            motorStepTick.Dispose();
            CharacterMotorSimulation.Dispose();
            await ShutdownInitializedServicesAsync();
            State = WorldState.Destroyed;
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        public void FixedTick(float deltaTime)
        {
            if (CanTick())
            {
                TickScheduler.ExecuteDomain(TickDomain.Fixed, deltaTime);
            }
        }

        public void UpdateTick(float deltaTime)
        {
            if (CanTick())
            {
                TickScheduler.ExecuteDomain(TickDomain.Update, deltaTime);
            }
        }

        public void LateTick(float deltaTime, float currentTime)
        {
            if (CanTick())
            {
                currentPresentationTime = currentTime;
                TickScheduler.ExecuteDomain(TickDomain.Late, deltaTime);
            }
        }

        private async Task ShutdownInitializedServicesAsync()
        {
            for (int index = initializedServices.Count - 1; index >= 0; index--)
            {
                try
                {
                    await initializedServices[index].ShutdownAsync();
                }
                catch
                {
                    // Continue reverse cleanup so one service cannot leak later owners.
                }
            }

            initializedServices.Clear();
        }

        internal void UnregisterController(IWorldController controller)
        {
            if (controller != null)
            {
                controllers.Remove(controller);
            }
        }

        private void ShutdownCurrentSession()
        {
            IWorldSession session = currentGameSession;
            currentGameSession = null;
            try
            {
                session?.Shutdown();
            }
            finally
            {
                ShutdownControllers();
                activeSessionId = default;
            }
        }

        private void ShutdownControllers()
        {
            for (int index = controllers.Count - 1; index >= 0; index--)
            {
                try
                {
                    controllers[index].Shutdown();
                }
                catch
                {
                    // Continue reverse cleanup so one Controller cannot leak later owners.
                }
            }

            controllers.Clear();
        }

        private bool CanTick()
        {
            return State != WorldState.Created
                && State != WorldState.InitializationFailed
                && State != WorldState.LaunchFailed
                && State != WorldState.GameStartFailed
                && State != WorldState.ShuttingDown
                && State != WorldState.Destroyed;
        }
    }
}
