using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CGame.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class NetworkPawnFactoryPlayModeTests
    {
        private const string BootstrapPath = "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/Bootstrap/GameBootstrap.asset";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator FormalPawnFactory_SeparatesLocalInputAndCameraFromRemotePawn()
        {
#if UNITY_EDITOR
            if (World.Current != null)
            {
                Task shutdownTask = World.Current.ShutdownAsync();
                while (!shutdownTask.IsCompleted) yield return null;
            }

            GameBootstrap bootstrap = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBootstrap>(BootstrapPath);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.GameModeDefinition, Is.Not.Null);
            PlayerStateDefinition playerState = bootstrap.GameModeDefinition.PlayerStateDefinition;
            Assert.That(playerState, Is.Not.Null);

            World world = World.Create(new WorldSubSystem[] { new CharacterPhysicsSubSystem() });
            Task initializeTask = world.InitializeAsync();
            while (!initializeTask.IsCompleted) yield return null;
            Assert.That(initializeTask.IsFaulted, Is.False, initializeTask.Exception?.GetBaseException().Message);

            var factory = new PawnFactory();
            Task<Pawn> localTask = factory.CreateAsync(
                playerState.PawnData,
                playerState.InputProfile,
                Vector3.zero,
                Quaternion.identity);
            Task<Pawn> remoteTask = factory.CreateRemoteAsync(
                playerState.PawnData,
                Vector3.right * 3f,
                Quaternion.identity);

            while (!localTask.IsCompleted || !remoteTask.IsCompleted) yield return null;
            Assert.That(localTask.IsFaulted, Is.False, localTask.Exception?.GetBaseException().Message);
            Assert.That(remoteTask.IsFaulted, Is.False, remoteTask.Exception?.GetBaseException().Message);

            Pawn localPawn = localTask.Result;
            Pawn remotePawn = remoteTask.Result;
            try
            {
                world.RegisterActor(localPawn, critical: true);
                world.RegisterActor(remotePawn, critical: false);
                Assert.That(localPawn.TryGetComponent(out PawnHeroComponent _), Is.True);
                Assert.That(localPawn.TryGetComponent(out PawnCameraComponent _), Is.True);
                Assert.That(remotePawn.TryGetComponent(out PawnHeroComponent _), Is.False);
                Assert.That(remotePawn.TryGetComponent(out PawnCameraComponent _), Is.False);
                Assert.That(remotePawn.TryGetComponent(out PawnMovementComponent _), Is.True);
                Assert.That(remotePawn.TryGetComponent(out PawnAnimationComponent _), Is.True);
                foreach (Camera remoteCamera in remotePawn.Transform.GetComponentsInChildren<Camera>(true))
                    Assert.That(remoteCamera.enabled, Is.False, "Remote pawn camera can take over the local Game View.");
                foreach (AudioListener remoteListener in remotePawn.Transform.GetComponentsInChildren<AudioListener>(true))
                    Assert.That(remoteListener.enabled, Is.False, "Remote pawn must not own the client audio listener.");
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
            }
#else
            Assert.Ignore("This fixture resolves the formal PawnDefinition through the Editor AssetDatabase.");
            yield return null;
#endif
        }

        [UnityTest]
        [Category("Network040")]
        [Category("Network041")]
        public IEnumerator NetworkGameMode_PossessionCreatesOwnerAndRemoteThroughDistinctFactoryPaths()
        {
#if UNITY_EDITOR
            AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                SampleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            GameInstance defaultGameInstance = Object.FindObjectOfType<GameInstance>();
            Assert.That(defaultGameInstance, Is.Not.Null);
            World defaultWorld = defaultGameInstance.RuntimeWorld;
            Object.Destroy(defaultGameInstance.gameObject);
            yield return null;
            if (defaultWorld != null)
            {
                Task priorShutdown = defaultWorld.ShutdownAsync();
                while (!priorShutdown.IsCompleted) yield return null;
                Assert.That(priorShutdown.IsFaulted, Is.False, priorShutdown.Exception?.GetBaseException().Message);
            }

            GameBootstrap bootstrap = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBootstrap>(BootstrapPath);
            Assert.That(bootstrap, Is.Not.Null);
            var networkDefinition = ScriptableObject.CreateInstance<ClientNetworkDefinition>();
            var factory = new RecordingPawnFactory();
            var configuration = ScriptableObject.CreateInstance<NetworkGameModeFixtureConfiguration>();
            configuration.Initialize(bootstrap, networkDefinition, factory);
            World world = World.Create(configuration);
            Task initializeTask = world.InitializeAsync();
            while (!initializeTask.IsCompleted) yield return null;
            Assert.That(initializeTask.IsFaulted, Is.False, initializeTask.Exception?.GetBaseException().Message);

            ClientWorld clientWorld = configuration.Network.ClientWorld;
            clientWorld.SetLocalPlayer(1);
            clientWorld.OnPawnSpawned(new PawnSpawnedEvent { PawnId = 100, OwnerPlayerId = 1, SpawnPointId = "PlayerPoint 1" });
            clientWorld.OnPossessionChanged(new PossessionChangedEvent { PlayerId = 1, PawnId = 100, PossessionRevision = 1 });
            clientWorld.OnPawnSpawned(new PawnSpawnedEvent { PawnId = 200, OwnerPlayerId = 2, SpawnPointId = "PlayerPoint 2" });
            world.StartPlay();
            PlayerController controller = (PlayerController)world.GameMode.PlayerController;
            for (int frame = 0; frame < 120 &&
                (controller.ControlledPawn == null ||
                 factory.RemotePawn == null ||
                 !factory.RemotePawn.TryGetComponent(out NetworkPawnBinding _)); frame++) yield return null;
            try
            {
                Pawn ownerPawn = controller.ControlledPawn;
                Assert.That(ownerPawn, Is.Not.Null, ((NetworkGameMode)world.GameMode).NetworkStatus);
                Assert.That(factory.RemotePawn, Is.Not.Null, ((NetworkGameMode)world.GameMode).NetworkStatus);
                Assert.That(ownerPawn.TryGetComponent(out NetworkPawnBinding ownerBinding), Is.True, "Owner binding missing.");
                Assert.That(ownerBinding.Role, Is.EqualTo(NetworkPawnRole.LocalAutonomous));
                Assert.That(ownerPawn.TryGetComponent(out PawnHeroComponent _), Is.True, "Owner hero component missing.");
                Assert.That(ownerPawn.TryGetComponent(out PawnCameraComponent _), Is.True, "Owner camera component missing.");
                Assert.That(factory.RemotePawn.TryGetComponent(out NetworkPawnBinding remoteBinding), Is.True, "Remote binding missing.");
                Assert.That(remoteBinding.Role, Is.EqualTo(NetworkPawnRole.RemoteSimulated));
                Assert.That(factory.RemotePawn.TryGetComponent(out PawnHeroComponent _), Is.False, "Remote pawn unexpectedly owns input.");
                Assert.That(factory.RemotePawn.TryGetComponent(out PawnCameraComponent _), Is.False, "Remote pawn unexpectedly owns a camera.");
                Assert.That(factory.RemotePawn.TryGetComponent(out PawnAnimationComponent remoteAnimation), Is.True, "Remote animation component missing.");
                Assert.That(remoteAnimation.CharacterSource, Is.TypeOf<RemoteAnimationSnapshotSource>());
                var started = new NetworkAnimationActionStarted
                {
                    PawnId = 200,
                    PossessionRevision = 0,
                    ActionSequence = 41,
                    ServerStartTick = 0,
                    DurationTicks = 120,
                    ActionKind = NetworkAnimationActionKind.Reload,
                    VariantId = "missing-by-design",
                    EquipmentInstanceId = 9
                };
                var response = new NetworkRpcResponse(
                    new NetworkPacketHeader(
                        NetworkPacketCodec.ProtocolVersion,
                        NetworkMessageId.AnimationActionStarted,
                        NetworkPacketFlags.None,
                        0,
                        1),
                    NetworkMessageSerializer.Serialize(started));
                System.Reflection.MethodInfo receive = typeof(ClientNetworkSubSystem).GetMethod(
                    "OnNetworkEvent",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(receive, Is.Not.Null);
                receive.Invoke(configuration.Network, new object[] { response });
                receive.Invoke(configuration.Network, new object[] { response });
                Assert.That(((NetworkGameMode)world.GameMode).RemoteAnimationActionPlayedCount, Is.Zero);
                Assert.That(((NetworkGameMode)world.GameMode).RemoteAnimationActionPresentationUnavailableCount, Is.EqualTo(1));
                Assert.That(((RemoteAnimationSnapshotSource)remoteAnimation.CharacterSource).DiscontinuityCount, Is.Zero);
            }
            finally
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(configuration);
                Object.DestroyImmediate(networkDefinition);
            }
#else
            Assert.Ignore("This fixture resolves the formal scene configuration through the Editor AssetDatabase.");
            yield return null;
#endif
        }

        [UnityTest]
        [Category("Network038")]
        public IEnumerator NetworkGameMode_FormalInputCreatesMovesAndReplaysDedicatedCorrection()
        {
#if UNITY_EDITOR
            AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                SampleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            GameInstance defaultGameInstance = Object.FindObjectOfType<GameInstance>();
            World defaultWorld = defaultGameInstance?.RuntimeWorld;
            if (defaultGameInstance != null) Object.Destroy(defaultGameInstance.gameObject);
            yield return null;
            if (defaultWorld != null)
            {
                Task shutdown = defaultWorld.ShutdownAsync();
                while (!shutdown.IsCompleted) yield return null;
            }

            GameBootstrap bootstrap = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBootstrap>(BootstrapPath);
            var definition = ScriptableObject.CreateInstance<ClientNetworkDefinition>();
            var configuration = ScriptableObject.CreateInstance<NetworkGameModeFixtureConfiguration>();
            configuration.Initialize(bootstrap, definition, new RecordingPawnFactory(), includeNetwork: true);
            World world = World.Create(configuration);
            var secondClient = new NetworkRpcClient(new LiteNetClientNetworkTransport());
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject mismatchWall = null;
            try
            {
                Task initialize = world.InitializeAsync();
                while (!initialize.IsCompleted) yield return null;
                Assert.That(initialize.IsFaulted, Is.False, initialize.Exception?.GetBaseException().ToString());
                ClientNetworkSubSystem first = configuration.Network;
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!first.IsHelloComplete && Time.realtimeSinceStartup < deadline)
                {
                    TickNetworkWorld(world, secondClient);
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(first.IsHelloComplete, Is.True, first.Failure);

                Task<CreateRoomResponse> create = first.CreateRoomAsync("network-038");
                while (!create.IsCompleted) { TickNetworkWorld(world, secondClient); yield return null; }
                Assert.That(create.IsFaulted, Is.False, create.Exception?.GetBaseException().ToString());
                string roomId = create.Result.RoomId;
                secondClient.Connect("127.0.0.1", 29000, "fps-v1");
                while (!secondClient.IsConnected) { TickNetworkWorld(world, secondClient); yield return null; }
                Task<NetworkRpcResponse> join = secondClient.RequestAsync(
                    NetworkMessageId.JoinRoomRequest,
                    NetworkMessageSerializer.Serialize(new JoinRoomRequest { RoomId = roomId }),
                    System.TimeSpan.FromSeconds(5));
                while (!join.IsCompleted) { TickNetworkWorld(world, secondClient); yield return null; }
                Task<SetReadyResponse> firstReady = first.SetReadyAsync(roomId, true);
                while (!firstReady.IsCompleted) { TickNetworkWorld(world, secondClient); yield return null; }
                Task<NetworkRpcResponse> secondReady = secondClient.RequestAsync(
                    NetworkMessageId.SetReadyRequest,
                    NetworkMessageSerializer.Serialize(new SetReadyRequest { RoomId = roomId, IsReady = true }),
                    System.TimeSpan.FromSeconds(20));
                while (!secondReady.IsCompleted) { TickNetworkWorld(world, secondClient); yield return null; }
                Assert.That(secondReady.IsFaulted, Is.False, secondReady.Exception?.GetBaseException().ToString());

                NetworkGameMode gameMode = (NetworkGameMode)world.GameMode;
                PlayerController controller = (PlayerController)gameMode.PlayerController;
                deadline = Time.realtimeSinceStartup + 12f;
                while ((controller.ControlledPawn == null || !first.IsMovementConnected) && Time.realtimeSinceStartup < deadline)
                {
                    TickNetworkWorld(world, secondClient);
                    yield return null;
                }
                Assert.That(controller.ControlledPawn, Is.Not.Null, gameMode.NetworkStatus);
                Assert.That(first.IsMovementConnected, Is.True);
                world.StartPlay();

                Pawn pawn = controller.ControlledPawn;
                mismatchWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mismatchWall.name = "Network038ClientOnlyWall";
                mismatchWall.transform.position = pawn.Transform.position + pawn.Transform.forward * 0.8f;
                mismatchWall.transform.localScale = new Vector3(3f, 3f, 0.2f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int frame = 0; frame < 90 && gameMode.ReplayCompletedCount == 0; frame++)
                {
                    InputSystem.Update();
                    TickNetworkWorld(world, secondClient);
                    yield return new WaitForFixedUpdate();
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.That(world.State, Is.EqualTo(WorldState.Playing), world.Failure);
                Assert.That(gameMode.CharacterPhysicsFixedStepCount, Is.GreaterThan(0), "Character physics subsystem did not advance.");
                Assert.That(gameMode.FixedStepObservedCount, Is.GreaterThan(0), "Character physics did not publish fixed-step completion.");
                Assert.That(gameMode.MoveCreatedCount, Is.GreaterThan(0), $"NetworkGameMode did not create PawnMove after data connection. {gameMode.MovementPredictionDiagnostic}");
                Assert.That(gameMode.LastReconcileKind, Is.EqualTo(OwnerReconcileKind.Correction), gameMode.MovementPredictionDiagnostic);
                Assert.That(gameMode.ReplayCompletedCount, Is.GreaterThan(0), gameMode.MovementPredictionDiagnostic);
            }
            finally
            {
                if (mismatchWall != null) Object.Destroy(mismatchWall);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                secondClient.Dispose();
                world.ShutdownAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(configuration);
                Object.DestroyImmediate(definition);
            }
#else
            Assert.Ignore("This mainline fixture requires the Editor formal assets.");
            yield return null;
#endif
        }

        private static void TickNetworkWorld(World world, NetworkRpcClient secondClient)
        {
            if (world.State == WorldState.Initialized && world.GameMode is INetworkPrePlayExecution prePlay)
            {
                prePlay.PumpPrePlay();
            }
            else if (world.State == WorldState.Playing)
            {
                world.UpdateTick(CharacterPhysicsSubSystem.FixedStepSeconds);
                world.FixedTick(CharacterPhysicsSubSystem.FixedStepSeconds);
            }
            secondClient.Tick();
        }

        private sealed class RecordingPawnFactory : PawnFactory
        {
            public Pawn RemotePawn { get; private set; }

            public override async Task<Pawn> CreateRemoteAsync(PawnDefinition definition, Vector3 position, Quaternion rotation, ActorComponent supplementalComponent, System.Threading.CancellationToken cancellationToken = default)
            {
                RemotePawn = await base.CreateRemoteAsync(definition, position, rotation, supplementalComponent, cancellationToken);
                return RemotePawn;
            }

            public override async Task<Pawn> CreateRemoteAsync(
                PawnDefinition definition,
                Vector3 position,
                Quaternion rotation,
                ActorComponent supplementalComponent,
                System.Func<Transform, CGame.Animation.IAnimationCharacterSource> characterSourceFactory,
                System.Threading.CancellationToken cancellationToken = default)
            {
                RemotePawn = await base.CreateRemoteAsync(
                    definition,
                    position,
                    rotation,
                    supplementalComponent,
                    characterSourceFactory,
                    cancellationToken);
                return RemotePawn;
            }
        }

        private sealed class NetworkGameModeFixtureConfiguration : WorldConfiguration
        {
            private GameBootstrap bootstrap;
            private ClientNetworkDefinition networkDefinition;
            private RecordingPawnFactory pawnFactory;
            private bool includeNetwork;
            private GameBootstrap runtimeBootstrap;

            public ClientNetworkSubSystem Network { get; private set; }

            public void Initialize(GameBootstrap bootstrap, ClientNetworkDefinition networkDefinition, RecordingPawnFactory pawnFactory, bool includeNetwork = false)
            {
                this.bootstrap = bootstrap;
                this.networkDefinition = networkDefinition;
                this.pawnFactory = pawnFactory;
                this.includeNetwork = includeNetwork;
                if (includeNetwork)
                {
                    runtimeBootstrap = Object.Instantiate(bootstrap);
                    runtimeBootstrap.ConfigureClientNetwork(networkDefinition);
                }
                else
                {
                    Network = new ClientNetworkSubSystem(networkDefinition);
                }
            }

            public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems()
            {
                if (!includeNetwork) return new WorldSubSystem[] { new CharacterPhysicsSubSystem() };
                IReadOnlyList<WorldSubSystem> subSystems = runtimeBootstrap.CreateWorldSubSystems();
                for (int index = 0; index < subSystems.Count; index++)
                    if (subSystems[index] is ClientNetworkSubSystem network) Network = network;
                return subSystems;
            }

            public override IReadOnlyList<PlayerSubSystem> CreatePlayerSubSystems() => includeNetwork
                ? runtimeBootstrap.CreatePlayerSubSystems()
                : new PlayerSubSystem[] { new InputSubSystem() };
            public override LevelRuntime CreateLevelRuntime() => new LevelRuntime(bootstrap.LevelDefinition, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            public override GameMode CreateGameMode(World world, Player player) => new NetworkGameMode(world, player, bootstrap.GameModeDefinition.PlayerStateDefinition, Network, pawnFactory);
        }
    }
}
