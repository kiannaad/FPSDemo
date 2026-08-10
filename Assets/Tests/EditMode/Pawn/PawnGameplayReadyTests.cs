using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using CGame.InventoryEquipment;
using NUnit.Framework;
using UnityEngine;

namespace CGame.PawnRuntime.Tests
{
    public sealed class PawnGameplayReadyTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private World world;

        [TearDown]
        public void TearDown()
        {
            world?.ShutdownAsync().GetAwaiter().GetResult();
            world = null;
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[index]);
                }
            }

            objects.Clear();
        }

        [Test]
        public void InventoryAndQuickBar_KeepUniqueOwnershipLeaseAndThreeDistinctStates()
        {
            WeaponEquipmentDefinition equipment = Track(
                WeaponEquipmentDefinition.CreateRuntime(30, 0));
            WeaponItemDefinition firstDefinition = Track(
                WeaponItemDefinition.CreateRuntime(equipment, 10, 20));
            WeaponItemDefinition secondDefinition = Track(
                WeaponItemDefinition.CreateRuntime(equipment, 5, 10));
            InitialInventorySet initialSet = Track(
                InitialInventorySet.CreateRuntime(0, firstDefinition, secondDefinition));
            var inventory = new InventoryComponent();
            IReadOnlyList<ItemInstanceHandle> handles = inventory.Initialize(initialSet);
            var quickBar = new QuickBarComponent(inventory);
            quickBar.Initialize(handles, initialSet.SelectedSlot);

            Assert.That(handles[0], Is.Not.EqualTo(handles[1]));
            Assert.That(quickBar.SelectedSlot, Is.Zero);
            Assert.That(quickBar.RequestedHandle, Is.EqualTo(handles[0]));
            Assert.That(quickBar.EquippedHandle.IsValid, Is.False);
            using (InventoryLease lease = inventory.AcquireLease(handles[0]))
            {
                Assert.That(lease.Item.Handle, Is.EqualTo(handles[0]));
                Assert.That(inventory.Remove(handles[0]), Is.False);
            }

            Assert.That(inventory.Remove(handles[0]), Is.True);
            Assert.Throws<InvalidOperationException>(() => inventory.Initialize(initialSet));
        }

        [Test]
        public void EquipmentTransaction_GatesReadySupportsActionsRapidSwitchFailureAndRespawnRebuild()
        {
            var equipmentAbilitySet = new AbilitySet();
            WeaponEquipmentDefinition firstEquipment = Track(
                WeaponEquipmentDefinition.CreateRuntime(10, 1, false, equipmentAbilitySet));
            WeaponEquipmentDefinition secondEquipment = Track(
                WeaponEquipmentDefinition.CreateRuntime(8, 1));
            WeaponEquipmentDefinition failedEquipment = Track(
                WeaponEquipmentDefinition.CreateRuntime(6, 1, true));
            WeaponItemDefinition firstItemDefinition = Track(
                WeaponItemDefinition.CreateRuntime(firstEquipment, 2, 8));
            WeaponItemDefinition secondItemDefinition = Track(
                WeaponItemDefinition.CreateRuntime(secondEquipment, 4, 4));
            WeaponItemDefinition failedItemDefinition = Track(
                WeaponItemDefinition.CreateRuntime(failedEquipment, 3, 3));
            InitialInventorySet initialSet = Track(
                InitialInventorySet.CreateRuntime(
                    0,
                    firstItemDefinition,
                    secondItemDefinition,
                    failedItemDefinition));
            SessionFixture fixture = StartSession(initialSet);

            Assert.That(fixture.GameMode.PrepareLocalPawn(new PawnFactory(), fixture.Starts), Is.True);
            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.True);
            Assert.That(world.State, Is.EqualTo(WorldState.StartingGame));
            PawnAssembly candidate = fixture.GameMode.CandidatePawnAssembly;
            Assert.That(candidate.Extension.State, Is.EqualTo(PawnInitState.DataInitialized));

            world.UpdateTick(0.016f);
            world.UpdateTick(0.016f);
            PawnAssembly firstPawn = fixture.GameMode.CurrentPawnAssembly;
            WeaponInstance firstWeapon = firstPawn.Equipment.CurrentWeapon;
            ItemInstance persistentItem = firstWeapon.Item;
            Assert.That(world.State, Is.EqualTo(WorldState.Running));
            Assert.That(fixture.Controller.QuickBar.EquippedHandle, Is.EqualTo(persistentItem.Handle));
            Assert.That(firstWeapon.AbilityReceipts.Count, Is.EqualTo(1));
            AbilityGrantReceipt equipmentReceipt = firstWeapon.AbilityReceipts[0];
            Assert.That(equipmentReceipt.IsActive, Is.True);

            Assert.That(firstWeapon.Fire(), Is.True);
            Assert.That(persistentItem.MagazineAmmo, Is.EqualTo(1));
            Assert.That(firstWeapon.Reload(), Is.EqualTo(8));
            Assert.That(firstWeapon.Melee(), Is.True);
            Assert.That(persistentItem.Durability, Is.LessThan(1f));

            Assert.That(fixture.Controller.QuickBar.SelectSlot(1), Is.True);
            Assert.That(fixture.Controller.QuickBar.SelectSlot(0), Is.True);
            long latestGeneration = fixture.Controller.QuickBar.RequestGeneration;
            world.UpdateTick(0.016f);
            world.UpdateTick(0.016f);
            Assert.That(firstPawn.Equipment.CurrentWeapon.Item, Is.SameAs(persistentItem));
            Assert.That(fixture.Controller.QuickBar.EquippedHandle, Is.EqualTo(persistentItem.Handle));
            Assert.That(fixture.Controller.QuickBar.RequestGeneration, Is.EqualTo(latestGeneration));

            Assert.That(fixture.Controller.QuickBar.SelectSlot(1), Is.True);
            world.UpdateTick(0.016f);
            world.UpdateTick(0.016f);
            WeaponInstance secondWeapon = firstPawn.Equipment.CurrentWeapon;
            Assert.That(secondWeapon, Is.Not.SameAs(firstWeapon));
            Assert.That(firstWeapon.IsDisposed, Is.True);
            Assert.That(equipmentReceipt.IsActive, Is.False);
            Assert.That(fixture.Controller.PlayerState.BaseGrantReceipt.IsActive, Is.True);
            Assert.That(secondWeapon.Item.Definition, Is.SameAs(secondItemDefinition));

            Assert.That(fixture.Controller.QuickBar.SelectSlot(2), Is.True);
            world.UpdateTick(0.016f);
            world.UpdateTick(0.016f);
            Assert.That(firstPawn.Equipment.CurrentWeapon, Is.SameAs(secondWeapon));
            Assert.That(firstPawn.Equipment.LastFailure, Is.Not.Empty);

            Assert.That(fixture.Controller.QuickBar.SelectSlot(0), Is.True);
            world.UpdateTick(0.016f);
            world.UpdateTick(0.016f);
            WeaponInstance preRespawnWeapon = firstPawn.Equipment.CurrentWeapon;
            Assert.That(preRespawnWeapon.Item, Is.SameAs(persistentItem));
            Assert.That(fixture.GameMode.PrepareLocalPawn(new PawnFactory(), fixture.Starts), Is.True);
            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.True);
            world.UpdateTick(0.016f);
            world.UpdateTick(0.016f);
            WeaponInstance respawnWeapon = fixture.GameMode.CurrentPawnAssembly.Equipment.CurrentWeapon;
            Assert.That(respawnWeapon, Is.Not.SameAs(preRespawnWeapon));
            Assert.That(preRespawnWeapon.IsDisposed, Is.True);
            Assert.That(respawnWeapon.Item, Is.SameAs(persistentItem));
            Assert.That(respawnWeapon.Item.MagazineAmmo, Is.EqualTo(9));
        }

        [Test]
        public void PawnExtension_AdvancesMonotonicallyFromRequestsAndShutsParticipantsInReverse()
        {
            PawnData pawnData = Track(PawnData.CreateRuntime(new AbilitySet()));
            var extension = new PawnExtensionComponent(new Pawn(), pawnData);
            var trace = new List<string>();
            var first = new TrackingParticipant("first", trace);
            var second = new TrackingParticipant("second", trace) { AllowDataInitialized = false };
            extension.RegisterParticipant(first);
            extension.RegisterParticipant(second);

            extension.RequestInitializationCheck();
            Assert.That(extension.AdvanceInitialization(), Is.True);
            Assert.That(extension.State, Is.EqualTo(PawnInitState.DataAvailable));
            Assert.That(second.EnterCount, Is.EqualTo(1));
            Assert.That(second.CanEnterCount, Is.GreaterThan(second.EnterCount));

            second.AllowDataInitialized = true;
            extension.RequestInitializationCheck();
            Assert.That(extension.AdvanceInitialization(), Is.True);
            Assert.That(extension.State, Is.EqualTo(PawnInitState.GameplayReady));
            Assert.That(first.EnteredStates, Is.EqualTo(new[]
            {
                PawnInitState.DataAvailable,
                PawnInitState.DataInitialized,
                PawnInitState.GameplayReady
            }));

            extension.Shutdown();
            Assert.That(extension.State, Is.EqualTo(PawnInitState.Destroyed));
            Assert.That(trace.GetRange(trace.Count - 2, 2), Is.EqualTo(new[]
            {
                "shutdown:second",
                "shutdown:first"
            }));
        }

        [Test]
        public void PawnFactory_IsStatelessAndPawnDataUniquelySelectsPrefab()
        {
            GameObject prefab = Track(new GameObject("PawnPrefab"));
            prefab.SetActive(false);
            PawnData pawnData = Track(PawnData.CreateRuntime(prefab, new AbilitySet()));
            var factory = new PawnFactory();

            PawnAssembly first = factory.CreateCandidate(pawnData, new Vector3(1f, 2f, 3f), Quaternion.identity);
            PawnAssembly second = factory.CreateCandidate(pawnData, new Vector3(4f, 5f, 6f), Quaternion.identity);

            Assert.That(first.Root, Is.Not.SameAs(second.Root));
            Assert.That(first.Host.Pawn, Is.SameAs(first.Pawn));
            Assert.That(second.Host.Pawn, Is.SameAs(second.Pawn));
            Assert.That(first.Root.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(second.Root.transform.position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(first.Extension.State, Is.EqualTo(PawnInitState.Spawned));
            first.Dispose();
            second.Dispose();
        }

        [Test]
        public void PrepareDoesNotPublishPawn_CommitPublishesAvatarAndGameplayReadyEntersRunning()
        {
            SessionFixture fixture = StartSession();
            PlayerController controller = fixture.Controller;
            int baseAbilityCount = controller.PlayerState.AbilitySystem.AbilityCount;

            Assert.That(fixture.GameMode.PrepareLocalPawn(new PawnFactory(), fixture.Starts), Is.True);
            PawnAssembly candidate = fixture.GameMode.CandidatePawnAssembly;
            Assert.That(fixture.GameMode.SpawnState, Is.EqualTo(PawnSpawnState.DataAvailable));
            Assert.That(candidate.Extension.State, Is.EqualTo(PawnInitState.DataAvailable));
            Assert.That(candidate.Root.activeSelf, Is.False);
            Assert.That(controller.ControlledPawn, Is.Null);
            Assert.That(controller.PlayerState.CurrentPawn, Is.Null);
            Assert.That(controller.PlayerState.AbilitySystem.Avatar, Is.Null);

            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.True);

            Assert.That(world.State, Is.EqualTo(WorldState.Running));
            Assert.That(fixture.GameMode.SpawnState, Is.EqualTo(PawnSpawnState.GameplayReady));
            Assert.That(fixture.GameMode.CurrentPawnAssembly, Is.SameAs(candidate));
            Assert.That(controller.ControlledPawn, Is.SameAs(candidate.Pawn));
            Assert.That(controller.PlayerState.CurrentPawn, Is.SameAs(candidate.Pawn));
            Assert.That(controller.PlayerState.AbilitySystem.Avatar, Is.SameAs(candidate.Pawn));
            Assert.That(controller.PlayerState.AbilitySystem.AbilityCount, Is.EqualTo(baseAbilityCount));
            Assert.That(controller.PlayerState.BaseGrantReceipt.IsActive, Is.True);
            Assert.That(candidate.Hero.IsBound, Is.True);
            Assert.That(controller.QuickBar.BoundPawn, Is.SameAs(candidate.Pawn));
            Assert.That(controller.PlayerCamera.BoundPawn, Is.SameAs(candidate.Pawn));
            Assert.That(candidate.Root.activeSelf, Is.True);
        }

        [Test]
        public void GameplayReadyGate_KeepsWorldStartingUntilAsyncParticipantRequestsRecheck()
        {
            SessionFixture fixture = StartSession();
            Assert.That(fixture.GameMode.PrepareLocalPawn(new PawnFactory(), fixture.Starts), Is.True);
            PawnAssembly candidate = fixture.GameMode.CandidatePawnAssembly;
            candidate.Equipment.SetReady(false);

            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.True);
            Assert.That(candidate.Extension.State, Is.EqualTo(PawnInitState.DataInitialized));
            Assert.That(fixture.GameMode.SpawnState, Is.EqualTo(PawnSpawnState.WaitingForGameplayReady));
            Assert.That(world.State, Is.EqualTo(WorldState.StartingGame));

            candidate.Equipment.SetReady(true);
            candidate.Extension.RequestInitializationCheck();
            world.UpdateTick(0.016f);

            Assert.That(candidate.Extension.State, Is.EqualTo(PawnInitState.GameplayReady));
            Assert.That(fixture.GameMode.SpawnState, Is.EqualTo(PawnSpawnState.GameplayReady));
            Assert.That(world.State, Is.EqualTo(WorldState.Running));
        }

        [Test]
        public void InitialSpawnFault_DestroysCandidateAndMarksGameStartFailed()
        {
            SessionFixture fixture = StartSession();
            var factory = new FailingPawnFactory(PawnInitState.DataInitialized);
            Assert.That(fixture.GameMode.PrepareLocalPawn(factory, fixture.Starts), Is.True);
            PawnAssembly candidate = fixture.GameMode.CandidatePawnAssembly;

            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.False);

            Assert.That(candidate.IsDisposed, Is.True);
            Assert.That(fixture.GameMode.CandidatePawnAssembly, Is.Null);
            Assert.That(fixture.GameMode.CurrentPawnAssembly, Is.Null);
            Assert.That(fixture.Controller.ControlledPawn, Is.Null);
            Assert.That(fixture.Controller.PlayerState.CurrentPawn, Is.Null);
            Assert.That(world.State, Is.EqualTo(WorldState.GameStartFailed));
        }

        [Test]
        public void RespawnFault_KeepsControllerPlayerStateInventoryAndQuickBarAlive()
        {
            SessionFixture fixture = StartSession();
            Assert.That(fixture.GameMode.PrepareLocalPawn(new PawnFactory(), fixture.Starts), Is.True);
            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.True);
            PlayerController controller = fixture.Controller;
            PlayerState playerState = controller.PlayerState;
            IInventoryComponent inventory = controller.Inventory;
            IQuickBarComponent quickBar = controller.QuickBar;

            Assert.That(fixture.GameMode.PrepareLocalPawn(
                new FailingPawnFactory(PawnInitState.DataInitialized),
                fixture.Starts), Is.True);
            Assert.That(fixture.GameMode.CommitLocalPawn(), Is.False);

            Assert.That(world.State, Is.EqualTo(WorldState.Running));
            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.PlayerState, Is.SameAs(playerState));
            Assert.That(controller.Inventory, Is.SameAs(inventory));
            Assert.That(controller.QuickBar, Is.SameAs(quickBar));
            Assert.That(playerState.IsDisposed, Is.False);
            Assert.That(inventory.IsDisposed, Is.False);
            Assert.That(quickBar.IsDisposed, Is.False);
            Assert.That(controller.ControlledPawn, Is.Null);
            Assert.That(fixture.GameMode.CurrentPawnAssembly, Is.Null);
        }

        [Test]
        public void PawnShutdown_ReleasesHeroBindingsSymmetrically()
        {
            SessionFixture fixture = StartSession();
            fixture.GameMode.PrepareLocalPawn(new PawnFactory(), fixture.Starts);
            fixture.GameMode.CommitLocalPawn();
            PawnAssembly pawnAssembly = fixture.GameMode.CurrentPawnAssembly;
            Assert.That(pawnAssembly.Hero.IsBound, Is.True);

            world.ShutdownAsync().GetAwaiter().GetResult();
            world = null;

            Assert.That(pawnAssembly.IsDisposed, Is.True);
            Assert.That(pawnAssembly.Extension.State, Is.EqualTo(PawnInitState.Destroyed));
            Assert.That(pawnAssembly.Movement.IsShutdown, Is.True);
            Assert.That(pawnAssembly.Animation.IsShutdown, Is.True);
            Assert.That(pawnAssembly.Equipment.IsShutdown, Is.True);
            Assert.That(pawnAssembly.Hero.IsBound, Is.False);
        }

        private SessionFixture StartSession(InitialInventorySet initialInventorySet = null)
        {
            GameObject prefab = Track(new GameObject("PawnPrefab"));
            prefab.SetActive(false);
            PawnData pawnData = Track(PawnData.CreateRuntime(prefab, new AbilitySet()));
            PawnControllerDefinition controllerDefinition = Track(
                ScriptableObject.CreateInstance<PawnControllerDefinition>());
            PawnGameModeDefinition gameModeDefinition = Track(
                ScriptableObject.CreateInstance<PawnGameModeDefinition>());
            gameModeDefinition.Configure(pawnData, controllerDefinition, initialInventorySet);
            world = World.Create(
                Array.Empty<IWorldCoreService>(),
                new GameLauncher(new Func<ILaunchStep>[] { () => new ReadyStep() }));
            Assert.That(world.StartAsync().GetAwaiter().GetResult().Succeeded, Is.True);
            Assert.That(world.StartGame(new GameplaySessionFactory(gameModeDefinition)).Succeeded, Is.True);
            GameMode gameMode = ((GameManager)world.CurrentGameSession).CurrentGameMode;
            var starts = new PlayerStartRegistry();
            starts.Register(new PlayerStartInfo("local", Vector3.zero, Quaternion.identity));
            starts.Register(new PlayerStartInfo("respawn", Vector3.right, Quaternion.identity));
            return new SessionFixture(gameMode, gameMode.LocalPlayerController, starts);
        }

        private T Track<T>(T target) where T : UnityEngine.Object
        {
            objects.Add(target);
            return target;
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

        private sealed class SessionFixture
        {
            public SessionFixture(
                GameMode gameMode,
                PlayerController controller,
                PlayerStartRegistry starts)
            {
                GameMode = gameMode;
                Controller = controller;
                Starts = starts;
            }

            public GameMode GameMode { get; }
            public PlayerController Controller { get; }
            public PlayerStartRegistry Starts { get; }
        }

        private sealed class TrackingParticipant : IPawnInitStateParticipant
        {
            private readonly List<string> trace;

            public TrackingParticipant(string name, List<string> trace)
            {
                Name = name;
                this.trace = trace;
            }

            public string Name { get; }
            public bool AllowDataInitialized { get; set; } = true;
            public int CanEnterCount { get; private set; }
            public int EnterCount { get; private set; }
            public List<PawnInitState> EnteredStates { get; } = new List<PawnInitState>();

            public bool CanEnterState(PawnInitState nextState, PawnInitContext context)
            {
                CanEnterCount++;
                return nextState != PawnInitState.DataInitialized || AllowDataInitialized;
            }

            public void EnterState(PawnInitState nextState, PawnInitContext context)
            {
                EnterCount++;
                EnteredStates.Add(nextState);
                trace.Add($"enter:{Name}:{nextState}");
            }

            public void Shutdown()
            {
                trace.Add($"shutdown:{Name}");
            }
        }

        private sealed class FailingPawnFactory : PawnFactory
        {
            private readonly PawnInitState failState;

            public FailingPawnFactory(PawnInitState failState)
            {
                this.failState = failState;
            }

            public override PawnAssembly CreateCandidate(
                PawnData pawnData,
                Vector3 position,
                Quaternion rotation)
            {
                PawnAssembly assembly = base.CreateCandidate(pawnData, position, rotation);
                assembly.Extension.RegisterParticipant(new FailingParticipant(failState));
                return assembly;
            }
        }

        private sealed class FailingParticipant : IPawnInitStateParticipant
        {
            private readonly PawnInitState failState;

            public FailingParticipant(PawnInitState failState)
            {
                this.failState = failState;
            }

            public string Name => "Failing";

            public bool CanEnterState(PawnInitState nextState, PawnInitContext context) => true;

            public void EnterState(PawnInitState nextState, PawnInitContext context)
            {
                if (nextState == failState)
                {
                    throw new InvalidOperationException($"failed:{failState}");
                }
            }

            public void Shutdown()
            {
            }
        }
    }

    public sealed class PawnControllerDefinition : ControllerDefinition
    {
        public override PlayerController CreateController(PlayerControllerCreationContext context)
        {
            return PlayerController.Create(context, new DefaultPlayerControllerComponentFactory());
        }
    }

    public sealed class PawnGameModeDefinition : GameModeDefinition
    {
        private PawnData pawnData;
        private ControllerDefinition controllerDefinition;
        private InitialInventorySet initialInventorySet;

        public void Configure(
            PawnData pawnData,
            ControllerDefinition controllerDefinition,
            InitialInventorySet initialInventorySet = null)
        {
            this.pawnData = pawnData;
            this.controllerDefinition = controllerDefinition;
            this.initialInventorySet = initialInventorySet;
        }

        public override PawnData ResolvePawnData(GameStartRequest request) => pawnData;

        public override InitialInventorySet ResolveInitialInventorySet(GameStartRequest request)
        {
            return initialInventorySet;
        }

        public override GameMode CreateRuntime(GameModeCreationContext context, PawnData resolvedPawnData)
        {
            return new GameMode(context, controllerDefinition, resolvedPawnData);
        }
    }
}
