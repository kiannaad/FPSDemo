using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Gameplay.Tests
{
    public sealed class PlayerGameModeControllerTests
    {
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            if (World.Current != null)
            {
                World.Current.ShutdownAsync().GetAwaiter().GetResult();
            }

            for (int index = runtimeAssets.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(runtimeAssets[index]);
            }

            runtimeAssets.Clear();
        }

        [Test]
        public void World_CreatesOnePlayerGameModeAndConcretePlayerController()
        {
            GameObject pawnPrefab = Track(new GameObject("PlayerOwnershipPawnPrefab"));
            pawnPrefab.SetActive(false);
            PawnData pawnData = Track(PawnData.CreateRuntime(pawnPrefab, new AbilitySet()));
            TestConfiguration configuration = Track(CreateConfiguration(
                Array.Empty<PlayerSubSystem>(),
                (world, player) => new DefaultGameMode(world, player, pawnData, null)));
            World world = World.Create(configuration);

            world.InitializeAsync().GetAwaiter().GetResult();

            Assert.That(world.LocalPlayer, Is.Not.Null);
            Assert.That(world.GameMode, Is.TypeOf<DefaultGameMode>());
            Assert.That(world.LocalPlayer.Controller, Is.TypeOf<PlayerController>());
            Assert.That(world.GameMode.Player, Is.SameAs(world.LocalPlayer));
            Assert.That(world.GameMode.PlayerController, Is.SameAs(world.LocalPlayer.Controller));

            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            Assert.That(controller.Player, Is.SameAs(world.LocalPlayer));
            Assert.That(controller.PlayerState, Is.Not.Null);
            Assert.That(controller.PossessedPawn, Is.Not.Null);
            Assert.That(controller.ControlYaw, Is.Zero);
            Assert.That(controller.ControlPitch, Is.Zero);
            Assert.That(controller.IsActive, Is.False);

            world.UpdateTick(0.25f);
            Assert.That(controller.TickCount, Is.Zero, "Controller must not tick before BeginPlay.");

            world.StartPlay();
            world.UpdateTick(0.25f);

            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.TickCount, Is.EqualTo(1));
            Assert.That(controller.LastTickDeltaTime, Is.EqualTo(0.25f));
        }

        [Test]
        public void BeginPlayAndShutdown_RespectPlayerSubsystemThenActorOrder()
        {
            var trace = new List<string>();
            var playerSubSystem = new TracePlayerSubSystem(trace);
            TraceGameMode gameMode = null;
            TestConfiguration configuration = Track(CreateConfiguration(
                new PlayerSubSystem[] { playerSubSystem },
                (world, player) => gameMode = new TraceGameMode(world, player, trace)));
            World world = World.Create(configuration);

            world.InitializeAsync().GetAwaiter().GetResult();
            world.StartPlay();
            world.ShutdownAsync().GetAwaiter().GetResult();

            Assert.That(trace, Is.EqualTo(new[]
            {
                "subsystem.begin",
                "gamemode.begin",
                "controller.begin",
                "controller.end",
                "controller.shutdown",
                "gamemode.end",
                "gamemode.shutdown",
                "subsystem.end",
                "subsystem.shutdown"
            }));
            Assert.That(gameMode.PlayerController.State, Is.EqualTo(ActorState.Unregistered));
        }

        [Test]
        public void ControllerInitializationFailure_RollsBackPlayerStateAndWorldOwners()
        {
            PawnData pawnData = Track(PawnData.CreateRuntime(new AbilitySet()));
            FailingGameMode gameMode = null;
            TestConfiguration configuration = Track(CreateConfiguration(
                Array.Empty<PlayerSubSystem>(),
                (world, player) => gameMode = new FailingGameMode(world, player, pawnData)));
            World world = World.Create(configuration);

            InvalidOperationException failure = null;
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Assert.That(failure, Is.Not.Null);

            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(world.LocalPlayer, Is.Null);
            Assert.That(world.GameMode, Is.Null);
            Assert.That(gameMode.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(gameMode.CreatedController.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(gameMode.CreatedController.PlayerState, Is.Null);
        }

        [Test]
        public void ProductionAssets_LinkBootstrapToGameModeAndPawnData()
        {
            const string root = "Assets/Prefab/Gameplay";
            PawnData pawnData = AssetDatabase.LoadAssetAtPath<PawnData>($"{root}/DefaultPawnData.asset");
            DefaultGameModeDefinition gameModeDefinition =
                AssetDatabase.LoadAssetAtPath<DefaultGameModeDefinition>(
                    $"{root}/DefaultGameModeDefinition.asset");
            GameBootstrap gameBootstrap =
                AssetDatabase.LoadAssetAtPath<GameBootstrap>($"{root}/GameBootstrap.asset");

            Assert.That(pawnData, Is.Not.Null);
            Assert.That(gameModeDefinition, Is.Not.Null);
            Assert.That(gameBootstrap, Is.Not.Null);

            var gameModeObject = new SerializedObject(gameModeDefinition);
            Assert.That(
                gameModeObject.FindProperty("pawnDefinition").objectReferenceValue,
                Is.SameAs(pawnData));
            Assert.That(
                new SerializedObject(pawnData).FindProperty("pawnPrefab").objectReferenceValue,
                Is.Not.Null);

            var bootstrapObject = new SerializedObject(gameBootstrap);
            Assert.That(bootstrapObject.FindProperty("initializeResources").boolValue, Is.True);
            Assert.That(bootstrapObject.FindProperty("initializeInput").boolValue, Is.True);
            Assert.That(
                bootstrapObject.FindProperty("gameModeDefinition").objectReferenceValue,
                Is.SameAs(gameModeDefinition));
            Assert.That(
                bootstrapObject.FindProperty("characterPhysicsSettings").objectReferenceValue,
                Is.Not.Null);

            var pawnObject = new SerializedObject(pawnData);
            Assert.That(pawnObject.FindProperty("animationConfig").objectReferenceValue, Is.Not.Null);
            GameObject pawnPrefab = (GameObject)pawnObject.FindProperty("pawnPrefab").objectReferenceValue;
            Assert.That(pawnPrefab.GetComponent<CharacterPhysicsMotor>(), Is.Not.Null);
            Assert.That(
                pawnPrefab.GetComponent<Animator>(),
                Is.Null,
                "The character Animator must live on the generated visual, not on the Pawn root.");
            Camera pawnCamera = pawnPrefab.GetComponentInChildren<Camera>(true);
            Assert.That(pawnCamera, Is.Not.Null);
            Assert.That(pawnCamera.nearClipPlane, Is.LessThanOrEqualTo(0.05f));
            Transform visual = pawnPrefab.transform.Find("KinemationVisualCharacter");
            Assert.That(visual, Is.Not.Null);
            Animator visualAnimator = visual.GetComponentInChildren<Animator>(true);
            Assert.That(visualAnimator, Is.Not.Null);
            Assert.That(visualAnimator.avatar, Is.Not.Null);
            Assert.That(visualAnimator.runtimeAnimatorController, Is.Not.Null);
            Transform head = visual.GetComponentsInChildren<Transform>(true)
                .First(transform => transform.name == "Head");
            Assert.That(pawnCamera.transform.parent, Is.SameAs(head));
            SkinnedMeshRenderer[] visualRenderers =
                visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(visualRenderers, Has.Length.GreaterThanOrEqualTo(1));
            foreach (SkinnedMeshRenderer renderer in visualRenderers)
            {
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.rootBone, Is.Not.Null);
                Assert.That(renderer.bones, Is.Not.Empty);
            }

            string[] sceneDependencies = AssetDatabase.GetDependencies(
                "Assets/Scenes/SampleScene.unity",
                true);
            Assert.That(sceneDependencies, Does.Contain("Assets/Prefab/Gameplay/GameBootstrap.asset"));
            Assert.That(sceneDependencies, Does.Contain("Assets/Prefab/Gameplay/DefaultPawn.prefab"));
            Assert.That(sceneDependencies, Does.Contain("Assets/Script/World/Runtime/GameInstance.cs"));
            Assert.That(sceneDependencies, Does.Not.Contain("Assets/Script/World/Runtime/WorldBehaviour.cs"));
        }

        private T Track<T>(T asset) where T : UnityEngine.Object
        {
            runtimeAssets.Add(asset);
            return asset;
        }

        private static TestConfiguration CreateConfiguration(
            IReadOnlyList<PlayerSubSystem> playerSubSystems,
            Func<World, Player, GameMode> createGameMode)
        {
            TestConfiguration configuration = ScriptableObject.CreateInstance<TestConfiguration>();
            configuration.PlayerSubSystems = playerSubSystems;
            configuration.GameModeFactory = createGameMode;
            return configuration;
        }

        private sealed class TestConfiguration : WorldConfiguration
        {
            public IReadOnlyList<PlayerSubSystem> PlayerSubSystems { get; set; }

            public Func<World, Player, GameMode> GameModeFactory { get; set; }

            public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems() =>
                Array.Empty<WorldSubSystem>();

            public override IReadOnlyList<PlayerSubSystem> CreatePlayerSubSystems() =>
                PlayerSubSystems ?? Array.Empty<PlayerSubSystem>();

            public override GameMode CreateGameMode(World world, Player player) =>
                GameModeFactory?.Invoke(world, player);
        }

        private sealed class TracePlayerSubSystem : PlayerSubSystem
        {
            private readonly List<string> trace;

            public TracePlayerSubSystem(List<string> trace)
            {
                this.trace = trace;
            }

            protected override void OnBeginPlay() => trace.Add("subsystem.begin");

            protected override void OnEndPlay() => trace.Add("subsystem.end");

            protected override Task OnShutdownAsync()
            {
                trace.Add("subsystem.shutdown");
                return Task.CompletedTask;
            }
        }

        private sealed class TraceGameMode : GameMode
        {
            private readonly List<string> trace;

            public TraceGameMode(World world, Player player, List<string> trace)
                : base(world, player)
            {
                this.trace = trace;
            }

            protected override Controller CreatePlayerController(Player player) =>
                new TraceController(player, trace);

            protected override void OnBeginPlay() => trace.Add("gamemode.begin");

            protected override void OnEndPlay() => trace.Add("gamemode.end");

            protected override void OnShutdown() => trace.Add("gamemode.shutdown");
        }

        private sealed class TraceController : Controller
        {
            private readonly List<string> trace;

            public TraceController(Player player, List<string> trace)
                : base(player)
            {
                this.trace = trace;
            }

            protected override void OnBeginPlay() => trace.Add("controller.begin");

            protected override void OnEndPlay() => trace.Add("controller.end");

            protected override void OnShutdown() => trace.Add("controller.shutdown");
        }

        private sealed class FailingGameMode : GameMode
        {
            private readonly PawnData pawnData;

            public FailingGameMode(World world, Player player, PawnData pawnData)
                : base(world, player)
            {
                this.pawnData = pawnData;
            }

            public PlayerController CreatedController { get; private set; }

            protected override Controller CreatePlayerController(Player player)
            {
                CreatedController = CGame.PlayerController.Create(
                    player,
                    pawnData,
                    null,
                    null,
                    new NullInventoryFactory());
                return CreatedController;
            }
        }

        private sealed class NullInventoryFactory : IPlayerControllerComponentFactory
        {
            public IInventoryComponent CreateInventory() => null;

            public IQuickBarComponent CreateQuickBar(IInventoryComponent inventory) => null;

        }
    }
}
