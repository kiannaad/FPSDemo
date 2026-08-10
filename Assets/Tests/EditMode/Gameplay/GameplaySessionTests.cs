using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Gameplay.Tests
{
    public sealed class GameplaySessionTests
    {
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private World world;

        [TearDown]
        public void TearDown()
        {
            world?.ShutdownAsync().GetAwaiter().GetResult();
            world = null;
            for (int index = assets.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(assets[index]);
            }

            assets.Clear();
        }

        [Test]
        public void StartGame_ResolvesPawnDataBeforeCreatingOneLocalController()
        {
            StartWorld();
            var trace = new List<string>();
            PawnData pawnData = Track(PawnData.CreateRuntime(new AbilitySet()));
            TestControllerDefinition controllerDefinition = Track(
                ScriptableObject.CreateInstance<TestControllerDefinition>());
            controllerDefinition.Configure(
                context => PlayerController.Create(context, new DefaultPlayerControllerComponentFactory()),
                trace);
            TestGameModeDefinition gameModeDefinition = Track(
                ScriptableObject.CreateInstance<TestGameModeDefinition>());
            gameModeDefinition.Configure(pawnData, controllerDefinition, trace);

            GameSessionStartResult result = world.StartGame(new GameplaySessionFactory(gameModeDefinition));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(world.State, Is.EqualTo(WorldState.StartingGame));
            Assert.That(trace, Is.EqualTo(new[] { "resolve-pawn-data", "create-game-mode", "create-controller" }));
            Assert.That(world.Controllers, Has.Count.EqualTo(1));
            GameManager gameManager = world.CurrentGameSession as GameManager;
            Assert.That(gameManager, Is.Not.Null);
            Assert.That(gameManager.Id, Is.EqualTo(result.SessionId));
            Assert.That(gameManager.Request.LaunchAttemptId.Value, Is.EqualTo(1));
            Assert.That(gameManager.CurrentGameMode.ResolvedPawnData, Is.SameAs(pawnData));
        }

        [Test]
        public void Controller_WithoutPawnOwnsPersistentStateAndTicksInControllerGroup()
        {
            StartWorld();
            AbilitySet baseSet = new AbilitySet();
            PawnData pawnData = Track(PawnData.CreateRuntime(baseSet));
            TestGameModeDefinition definition = CreateDefinition(pawnData);
            GameSessionStartResult result = world.StartGame(new GameplaySessionFactory(definition));
            PlayerController controller = ((GameManager)world.CurrentGameSession)
                .CurrentGameMode.LocalPlayerController;
            GameMode gameMode = ((GameManager)world.CurrentGameSession).CurrentGameMode;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.PlayerState.Avatar, Is.Null);
            Assert.That(controller.PlayerState.AbilitySystem.Avatar, Is.Null);
            Assert.That(controller.PlayerState.BaseGrantReceipts, Has.Count.EqualTo(1));
            Assert.That(controller.PlayerState.BaseGrantReceipt.AbilitySet, Is.SameAs(baseSet));
            Assert.That(controller.PlayerState.BaseGrantReceipt.SourceObject, Is.SameAs(pawnData));
            Assert.That(controller.Inventory, Is.Not.Null);
            Assert.That(controller.QuickBar.Inventory, Is.SameAs(controller.Inventory));
            Assert.That(controller.PlayerCamera, Is.Not.Null);

            world.UpdateTick(0.25f);

            Assert.That(gameMode.TickCount, Is.EqualTo(1));
            Assert.That(gameMode.LastTickDeltaTime, Is.EqualTo(0.25f));
            Assert.That(controller.TickCount, Is.EqualTo(1));
            Assert.That(controller.LastTickDeltaTime, Is.EqualTo(0.25f));
        }

        [Test]
        public void ReturnToLogin_CleansSessionAndRejectsOldSessionCallbacks()
        {
            StartWorld();
            PawnData pawnData = Track(PawnData.CreateRuntime(new AbilitySet()));
            TestGameModeDefinition definition = CreateDefinition(pawnData);
            GameSessionStartResult first = world.StartGame(new GameplaySessionFactory(definition));
            PlayerController oldController = ((GameManager)world.CurrentGameSession)
                .CurrentGameMode.LocalPlayerController;
            PlayerState oldPlayerState = oldController.PlayerState;
            AbilityGrantReceipt oldReceipt = oldPlayerState.BaseGrantReceipt;

            WorldStartResult returnResult = world.ReturnToLoginAsync().GetAwaiter().GetResult();

            Assert.That(returnResult.Succeeded, Is.True);
            Assert.That(world.State, Is.EqualTo(WorldState.Launching));
            Assert.That(world.CurrentGameSession, Is.Null);
            Assert.That(world.Controllers, Is.Empty);
            Assert.That(oldController.IsActive, Is.False);
            Assert.That(oldPlayerState.IsDisposed, Is.True);
            Assert.That(oldReceipt.IsActive, Is.False);
            Assert.That(world.TryRunForSession(first.SessionId, null), Is.False);

            GameSessionStartResult second = world.StartGame(new GameplaySessionFactory(definition));
            int accepted = 0;
            Assert.That(second.SessionId, Is.Not.EqualTo(first.SessionId));
            Assert.That(world.TryRunForSession(first.SessionId, ignored => accepted++), Is.False);
            Assert.That(world.TryRunForSession(second.SessionId, ignored => accepted++), Is.True);
            Assert.That(accepted, Is.EqualTo(1));
        }

        [Test]
        public void ControllerInitializationFailure_CleansCreatedComponentsInReverseOrder()
        {
            StartWorld();
            PawnData pawnData = Track(PawnData.CreateRuntime(new AbilitySet()));
            var trace = new List<string>();
            TestControllerDefinition controllerDefinition = Track(
                ScriptableObject.CreateInstance<TestControllerDefinition>());
            controllerDefinition.Configure(
                context => PlayerController.Create(context, new FailingComponentFactory(trace)),
                trace);
            TestGameModeDefinition definition = Track(
                ScriptableObject.CreateInstance<TestGameModeDefinition>());
            definition.Configure(pawnData, controllerDefinition, trace);

            GameSessionStartResult result = world.StartGame(new GameplaySessionFactory(definition));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(world.State, Is.EqualTo(WorldState.GameStartFailed));
            Assert.That(world.CurrentGameSession, Is.Null);
            Assert.That(world.Controllers, Is.Empty);
            Assert.That(trace.Take(5), Is.EqualTo(new[]
            {
                "resolve-pawn-data",
                "create-game-mode",
                "create-controller",
                "create-inventory",
                "create-quickbar"
            }));
            Assert.That(trace.Skip(5), Is.EqualTo(new[]
            {
                "create-camera-failed",
                "dispose-quickbar",
                "dispose-inventory"
            }));
        }

        [Test]
        public void WorldOwnsOnlyControllerCollection_NotPlayerStatePawnOrInventoryCollections()
        {
            FieldInfo[] fields = typeof(World).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            string[] collectionElementTypes = fields
                .Select(field => field.FieldType)
                .Where(type => type.IsGenericType)
                .SelectMany(type => type.GetGenericArguments())
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(collectionElementTypes, Does.Contain(typeof(IWorldController).FullName));
            Assert.That(collectionElementTypes, Has.None.EqualTo(typeof(PlayerState).FullName));
            Assert.That(collectionElementTypes, Has.None.EqualTo(typeof(IInventoryComponent).FullName));
            Assert.That(collectionElementTypes, Has.None.Contains("Pawn"));
        }

        [Test]
        public void DefinitionsUsePolymorphicFactoriesWithoutTypeOrReflectionFields()
        {
            Assert.That(typeof(GameModeDefinition).IsAbstract, Is.True);
            Assert.That(typeof(ControllerDefinition).IsAbstract, Is.True);
            Assert.That(
                typeof(GameModeDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => field.FieldType == typeof(Type)),
                Is.False);
            Assert.That(
                typeof(ControllerDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => field.FieldType == typeof(Type)),
                Is.False);
        }

        [Test]
        public void GameManagerContainsSessionStateWithoutGenericManagerRegistry()
        {
            string[] fieldTypes = typeof(GameManager)
                .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
                .Select(field => field.FieldType.FullName)
                .ToArray();

            Assert.That(fieldTypes.Any(type => type.Contains("Dictionary") || type.Contains("LinkedList")), Is.False);
            Assert.That(typeof(GameManager).GetMethod("GetManager", BindingFlags.Public | BindingFlags.Static), Is.Null);
            Assert.That(typeof(GameManager).GetMethod("CreateManager", BindingFlags.Public | BindingFlags.Static), Is.Null);
        }

        private void StartWorld()
        {
            world = World.Create(
                Array.Empty<IWorldCoreService>(),
                new GameLauncher(new Func<ILaunchStep>[] { () => new ReadyStep() }));
            WorldStartResult result = world.StartAsync().GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True);
        }

        private TestGameModeDefinition CreateDefinition(PawnData pawnData)
        {
            TestControllerDefinition controllerDefinition = Track(
                ScriptableObject.CreateInstance<TestControllerDefinition>());
            controllerDefinition.Configure(
                context => PlayerController.Create(context, new DefaultPlayerControllerComponentFactory()),
                null);
            TestGameModeDefinition definition = Track(
                ScriptableObject.CreateInstance<TestGameModeDefinition>());
            definition.Configure(pawnData, controllerDefinition, null);
            return definition;
        }

        private T Track<T>(T asset) where T : UnityEngine.Object
        {
            assets.Add(asset);
            return asset;
        }

        private sealed class ReadyStep : ILaunchStep
        {
            public string Name => "Ready";

            public Task<LaunchStepResult> ExecuteAsync(
                LaunchContext context,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(LaunchStepResult.Success());
            }

            public Task ExitAsync(LaunchContext context) => Task.CompletedTask;
        }

        private sealed class FailingComponentFactory : IPlayerControllerComponentFactory
        {
            private readonly List<string> trace;

            public FailingComponentFactory(List<string> trace)
            {
                this.trace = trace;
            }

            public IInventoryComponent CreateInventory()
            {
                trace.Add("create-inventory");
                return new TrackingInventory(trace);
            }

            public IQuickBarComponent CreateQuickBar(IInventoryComponent inventory)
            {
                trace.Add("create-quickbar");
                return new TrackingQuickBar(inventory, trace);
            }

            public IPlayerCameraComponent CreatePlayerCamera()
            {
                trace.Add("create-camera-failed");
                throw new InvalidOperationException("camera creation failed");
            }
        }

        private sealed class TrackingInventory : IInventoryComponent
        {
            private readonly List<string> trace;

            public TrackingInventory(List<string> trace)
            {
                this.trace = trace;
            }

            public bool IsDisposed { get; private set; }

            public bool IsInitialized { get; private set; }

            public IReadOnlyCollection<ItemInstance> Items => Array.Empty<ItemInstance>();

            public IReadOnlyList<ItemInstanceHandle> Initialize(InitialInventorySet initialSet)
            {
                IsInitialized = true;
                return Array.Empty<ItemInstanceHandle>();
            }

            public ItemInstanceHandle Add(ItemDefinition definition) => throw new NotSupportedException();

            public bool TryGet(ItemInstanceHandle handle, out ItemInstance item)
            {
                item = null;
                return false;
            }

            public InventoryLease AcquireLease(ItemInstanceHandle handle) => null;

            public bool Remove(ItemInstanceHandle handle) => false;

            public void Dispose()
            {
                IsDisposed = true;
                trace.Add("dispose-inventory");
            }
        }

        private sealed class TrackingQuickBar : IQuickBarComponent
        {
            private readonly List<string> trace;

            public TrackingQuickBar(IInventoryComponent inventory, List<string> trace)
            {
                Inventory = inventory;
                this.trace = trace;
            }

            public IInventoryComponent Inventory { get; }

            public bool IsDisposed { get; private set; }

            public Pawn BoundPawn { get; private set; }

            public IReadOnlyList<ItemInstanceHandle> Slots => Array.Empty<ItemInstanceHandle>();

            public int SelectedSlot => -1;

            public ItemInstanceHandle RequestedHandle => default;

            public ItemInstanceHandle EquippedHandle => default;

            public long RequestGeneration => 0;

            public event Action<ItemInstanceHandle, long> EquipmentRequested;

            public void Initialize(IReadOnlyList<ItemInstanceHandle> handles, int selectedSlot)
            {
            }

            public bool SelectSlot(int slotIndex) => false;

            public bool ConfirmEquipped(ItemInstanceHandle handle, long generation) => false;

            public bool RejectRequest(ItemInstanceHandle handle, long generation) => false;

            public PawnBindingReceipt BindPawn(Pawn pawn)
            {
                BoundPawn = pawn;
                return new PawnBindingReceipt(() => BoundPawn = null);
            }

            public void Dispose()
            {
                EquipmentRequested = null;
                IsDisposed = true;
                trace.Add("dispose-quickbar");
            }
        }
    }

    public sealed class TestControllerDefinition : ControllerDefinition
    {
        private Func<PlayerControllerCreationContext, PlayerController> factory;
        private List<string> trace;

        public void Configure(
            Func<PlayerControllerCreationContext, PlayerController> factory,
            List<string> trace)
        {
            this.factory = factory;
            this.trace = trace;
        }

        public override PlayerController CreateController(PlayerControllerCreationContext context)
        {
            trace?.Add("create-controller");
            return factory(context);
        }
    }

    public sealed class TestGameModeDefinition : GameModeDefinition
    {
        private PawnData pawnData;
        private ControllerDefinition controllerDefinition;
        private List<string> trace;

        public void Configure(
            PawnData pawnData,
            ControllerDefinition controllerDefinition,
            List<string> trace)
        {
            this.pawnData = pawnData;
            this.controllerDefinition = controllerDefinition;
            this.trace = trace;
        }

        public override PawnData ResolvePawnData(GameStartRequest request)
        {
            trace?.Add("resolve-pawn-data");
            return pawnData;
        }

        public override GameMode CreateRuntime(GameModeCreationContext context, PawnData resolvedPawnData)
        {
            trace?.Add("create-game-mode");
            Assert.That(resolvedPawnData, Is.SameAs(pawnData));
            return new GameMode(context, controllerDefinition, resolvedPawnData);
        }
    }
}
