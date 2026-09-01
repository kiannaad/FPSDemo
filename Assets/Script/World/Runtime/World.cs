using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class World
    {
        private readonly Dictionary<Type, WorldSubSystem> subSystemsByType =
            new Dictionary<Type, WorldSubSystem>();
        private readonly List<WorldSubSystem> orderedSubSystems = new List<WorldSubSystem>();
        private readonly List<WorldSubSystem> initializedSubSystems = new List<WorldSubSystem>();
        private readonly List<WorldSubSystem> begunSubSystems = new List<WorldSubSystem>();
        private readonly Dictionary<WorldSubSystem, IDisposable> subSystemTickRegistrations =
            new Dictionary<WorldSubSystem, IDisposable>();
        private readonly List<ActorRegistration> actorRegistrations = new List<ActorRegistration>();
        private readonly WorldConfiguration configuration;
        private Task shutdownTask;

        private World(IEnumerable<WorldSubSystem> subSystems, WorldConfiguration configuration)
        {
            this.configuration = configuration;
            TickTaskManager = new TickTaskManager();
            TickTaskManager.OwnerFaulted += OnTickOwnerFaulted;
            AddSubSystems(subSystems ?? Array.Empty<WorldSubSystem>());
            BuildSubSystemOrder();
        }

        public static World Current { get; private set; }

        public WorldState State { get; private set; } = WorldState.Created;

        public string Failure { get; private set; } = string.Empty;

        public TickTaskManager TickTaskManager { get; }

        public Player LocalPlayer { get; private set; }

        public GameMode GameMode { get; private set; }

        public LevelRuntime LevelRuntime { get; private set; }

        public GameState GameState { get; private set; }

        public bool IsGameplayReady { get; private set; }

        public int GameplayReadyPublishCount { get; private set; }

        public WorldState GameplayReadyPublishedWorldState { get; private set; }

        public ActorState GameplayReadyPublishedPawnState { get; private set; }

        public event Action GameplayReady;

        public int RegisteredActorCount => actorRegistrations.Count;

        public static World Create() => Create(Array.Empty<WorldSubSystem>(), null);

        public static World Create(IEnumerable<WorldSubSystem> subSystems) => Create(subSystems, null);

        public static World Create(WorldConfiguration configuration)
        {
            IReadOnlyList<WorldSubSystem> subSystems = configuration == null
                ? Array.Empty<WorldSubSystem>()
                : configuration.CreateWorldSubSystems();
            return Create(subSystems, configuration);
        }

        private static World Create(IEnumerable<WorldSubSystem> subSystems, WorldConfiguration configuration)
        {
            if (Current != null)
            {
                if (Current.State == WorldState.ShuttingDown ||
                    Current.State == WorldState.Destroyed ||
                    Current.State == WorldState.Faulted)
                {
                    Current = null;
                }
                else
                {
                    throw new InvalidOperationException("Only one active World is allowed.");
                }
            }

            var world = new World(subSystems, configuration);
            Current = world;
            return world;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            EnsureState(WorldState.Created, "initialize");
            State = WorldState.Initializing;
            try
            {
                LevelRuntime = configuration?.CreateLevelRuntime();
                for (int index = 0; index < orderedSubSystems.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WorldSubSystem subSystem = orderedSubSystems[index];
                    subSystem.Attach(this);
                    await subSystem.InitializeSubSystemAsync(cancellationToken);
                    initializedSubSystems.Add(subSystem);
                    IDisposable registration = TickTaskManager.RegisterOwner(
                        subSystem,
                        subSystem.TickTasks,
                        critical: true);
                    subSystemTickRegistrations.Add(subSystem, registration);
                }

                cancellationToken.ThrowIfCancellationRequested();
                GameState = configuration?.CreateGameState(this);
                if (GameState != null)
                {
                    RegisterActor(GameState, critical: true);
                    await GameState.LoadExperienceAsync();
                }

                LocalPlayer = new Player(configuration?.CreatePlayerSubSystems());
                await LocalPlayer.InitializeAsync(TickTaskManager, cancellationToken);
                GameMode gameMode = configuration?.CreateGameMode(this, LocalPlayer);
                if (gameMode != null)
                {
                    GameMode = gameMode;
                    RegisterActor(gameMode, critical: true);
                    gameMode.CreateAndAttachPlayerController();
                    await gameMode.SpawnDefaultPawn(gameMode.PlayerController, cancellationToken);
                }

                State = WorldState.Initialized;
            }
            catch (Exception exception)
            {
                Failure = exception.Message;
                State = WorldState.Faulted;
                LevelRuntime?.Shutdown();
                LevelRuntime = null;
                if (GameState != null)
                {
                    await GameState.ShutdownExperienceAsync();
                    GameState = null;
                }
                DisposeAllActors();
                GameMode = null;
                if (LocalPlayer != null)
                {
                    await LocalPlayer.ShutdownAsync(TickTaskManager);
                    LocalPlayer = null;
                }
                await ShutdownInitializedSubSystemsAsync();
                throw;
            }
        }

        public void StartPlay()
        {
            EnsureState(WorldState.Initialized, "start play");
            State = WorldState.StartingPlay;
            try
            {
                for (int index = 0; index < orderedSubSystems.Count; index++)
                {
                    WorldSubSystem subSystem = orderedSubSystems[index];
                    subSystem.BeginPlaySubSystem();
                    begunSubSystems.Add(subSystem);
                    TickTaskManager.SetOwnerEnabled(subSystem, true);
                }

                LocalPlayer?.BeginPlay(TickTaskManager);
                for (int index = 0; index < actorRegistrations.Count; index++)
                {
                    actorRegistrations[index].Activate();
                }

                State = WorldState.Playing;
                MarkGameplayReady();
            }
            catch (Exception exception)
            {
                Failure = exception.Message;
                DisposeAllActors();
                LocalPlayer?.ShutdownAsync(TickTaskManager).GetAwaiter().GetResult();
                EndBegunSubSystems();
                State = WorldState.Faulted;
                throw;
            }
        }

        public T GetSubSystem<T>() where T : WorldSubSystem
        {
            return subSystemsByType.TryGetValue(typeof(T), out WorldSubSystem subSystem)
                ? (T)subSystem
                : null;
        }

        public ActorRegistration RegisterActor(Actor actor, bool critical = false)
        {
            if (State == WorldState.ShuttingDown || State == WorldState.Destroyed || State == WorldState.Faulted)
            {
                throw new InvalidOperationException($"World cannot register an Actor while it is {State}.");
            }

            ActorRegistration registration = ActorRegistration.Register(actor, TickTaskManager, critical);
            registration.Disposed += OnActorRegistrationDisposed;
            actorRegistrations.Add(registration);
            return registration;
        }

        public void UnregisterActor(ActorRegistration registration)
        {
            if (registration == null || !actorRegistrations.Contains(registration))
            {
                throw new InvalidOperationException("ActorRegistration does not belong to this World.");
            }

            registration.Dispose();
        }

        public void ActivateActor(ActorRegistration registration)
        {
            if (registration == null || !actorRegistrations.Contains(registration))
            {
                throw new InvalidOperationException("ActorRegistration does not belong to this World.");
            }

            registration.Activate();
        }

        public void ReportGameplayFailure(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            if (State == WorldState.ShuttingDown || State == WorldState.Destroyed) return;
            Failure = exception.Message;
            EndBegunSubSystems();
            State = WorldState.Faulted;
        }

        public void FixedTick(float deltaTime) => ExecuteTick(TickDomain.Fixed, deltaTime);

        public void UpdateTick(float deltaTime) => ExecuteTick(TickDomain.Update, deltaTime);

        public void PreAnimationTick(float deltaTime) => ExecuteTick(TickDomain.PreAnimation, deltaTime);

        public void LateTick(float deltaTime) => ExecuteTick(TickDomain.Late, deltaTime);

        public Task ShutdownAsync()
        {
            if (shutdownTask != null)
            {
                return shutdownTask;
            }

            shutdownTask = ShutdownInternalAsync();
            return shutdownTask;
        }

        private async Task ShutdownInternalAsync()
        {
            State = WorldState.ShuttingDown;
            IsGameplayReady = false;
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            if (GameState != null)
            {
                await GameState.ShutdownExperienceAsync();
                GameState = null;
            }
            DisposeAllActors();
            GameMode = null;
            LevelRuntime?.Shutdown();
            LevelRuntime = null;
            if (LocalPlayer != null)
            {
                await LocalPlayer.ShutdownAsync(TickTaskManager);
                LocalPlayer = null;
            }
            EndBegunSubSystems();
            await ShutdownInitializedSubSystemsAsync();
            TickTaskManager.OwnerFaulted -= OnTickOwnerFaulted;
            State = WorldState.Destroyed;
            GameplayReady = null;
        }

        private void AddSubSystems(IEnumerable<WorldSubSystem> subSystems)
        {
            foreach (WorldSubSystem subSystem in subSystems)
            {
                if (subSystem == null)
                {
                    throw new InvalidOperationException("World contains a null SubSystem.");
                }

                Type type = subSystem.GetType();
                if (subSystemsByType.ContainsKey(type))
                {
                    throw new InvalidOperationException($"World already contains SubSystem {type.Name}.");
                }

                subSystemsByType.Add(type, subSystem);
            }
        }

        private void BuildSubSystemOrder()
        {
            var visiting = new HashSet<Type>();
            var visited = new HashSet<Type>();
            foreach (WorldSubSystem subSystem in subSystemsByType.Values)
            {
                VisitSubSystem(subSystem, visiting, visited);
            }
        }

        private void VisitSubSystem(WorldSubSystem subSystem, HashSet<Type> visiting, HashSet<Type> visited)
        {
            Type type = subSystem.GetType();
            if (visited.Contains(type))
            {
                return;
            }

            if (!visiting.Add(type))
            {
                throw new InvalidOperationException($"SubSystem dependency cycle includes {type.Name}.");
            }

            for (int index = 0; index < subSystem.Dependencies.Count; index++)
            {
                Type dependencyType = subSystem.Dependencies[index];
                if (!subSystemsByType.TryGetValue(dependencyType, out WorldSubSystem dependency))
                {
                    throw new InvalidOperationException($"SubSystem {type.Name} requires missing {dependencyType.Name}.");
                }

                VisitSubSystem(dependency, visiting, visited);
            }

            visiting.Remove(type);
            visited.Add(type);
            orderedSubSystems.Add(subSystem);
        }

        private void ExecuteTick(TickDomain domain, float deltaTime)
        {
            if (State == WorldState.Playing)
            {
                TickTaskManager.ExecuteDomain(domain, deltaTime);
            }
        }

        private void OnTickOwnerFaulted(TickTaskFault fault)
        {
            if (!(fault.Owner is WorldSubSystem))
            {
                return;
            }

            Failure = fault.Exception.Message;
            EndBegunSubSystems();
            State = WorldState.Faulted;
        }

        private void OnActorRegistrationDisposed(ActorRegistration registration)
        {
            registration.Disposed -= OnActorRegistrationDisposed;
            actorRegistrations.Remove(registration);
        }

        private void DisposeAllActors()
        {
            while (actorRegistrations.Count > 0)
            {
                ActorRegistration registration = actorRegistrations[actorRegistrations.Count - 1];
                registration.Dispose();
                if (actorRegistrations.Contains(registration))
                {
                    OnActorRegistrationDisposed(registration);
                }
            }
        }

        private void EndBegunSubSystems()
        {
            for (int index = begunSubSystems.Count - 1; index >= 0; index--)
            {
                WorldSubSystem subSystem = begunSubSystems[index];
                try
                {
                    TickTaskManager.SetOwnerEnabled(subSystem, false);
                    subSystem.EndPlaySubSystem();
                }
                catch
                {
                    // Continue reverse cleanup so one SubSystem cannot leak later owners.
                }
            }

            begunSubSystems.Clear();
        }

        private async Task ShutdownInitializedSubSystemsAsync()
        {
            for (int index = initializedSubSystems.Count - 1; index >= 0; index--)
            {
                WorldSubSystem subSystem = initializedSubSystems[index];
                if (subSystemTickRegistrations.TryGetValue(subSystem, out IDisposable registration))
                {
                    registration.Dispose();
                    subSystemTickRegistrations.Remove(subSystem);
                }

                try
                {
                    await subSystem.ShutdownSubSystemAsync();
                }
                catch
                {
                    // Continue reverse cleanup so one SubSystem cannot leak later owners.
                }
            }

            initializedSubSystems.Clear();
        }

        private void EnsureState(WorldState expected, string operation)
        {
            if (State != expected)
            {
                throw new InvalidOperationException($"World cannot {operation} while it is {State}.");
            }
        }

        private void MarkGameplayReady()
        {
            if (IsGameplayReady) throw new InvalidOperationException("World GameplayReady can only be published once.");
            if (State != WorldState.Playing)
                throw new InvalidOperationException("World must be Playing before GameplayReady.");
            if (GameMode != null)
            {
                Controller controller = GameMode.PlayerController;
                if (controller == null || controller.State != ActorState.Playing ||
                    controller.PossessedActor == null || controller.PossessedActor.State != ActorState.Playing)
                {
                    throw new InvalidOperationException("GameplayReady requires a Playing Controller with a possessed Playing Pawn.");
                }
            }

            IsGameplayReady = true;
            GameplayReadyPublishCount++;
            GameplayReadyPublishedWorldState = State;
            GameplayReadyPublishedPawnState = GameMode?.PlayerController?.PossessedActor?.State ?? ActorState.Constructed;
            GameplayReady?.Invoke();
        }
    }
}
