using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Network
{
    public sealed class DedicatedServerRuntime : MonoBehaviour
    {
        private readonly List<Pawn> authorityPawns = new List<Pawn>();
        private readonly List<ActorRegistration> pawnRegistrations = new List<ActorRegistration>();
        private readonly Dictionary<long, AuthorityMoveProcessor> moveProcessors =
            new Dictionary<long, AuthorityMoveProcessor>();
        private DedicatedServerLaunchConfiguration launch;
        private DedicatedServerHealthServer healthServer;
        private DedicatedDataServer dataServer;
        private World world;
        private CharacterPhysicsSubSystem physics;
        private long authorityServerTick;
        private float previousFixedDeltaTime;
        private bool initialized;

        public IReadOnlyList<Pawn> AuthorityPawns => authorityPawns;
        public long FixedStepCount => physics?.FixedStepCount ?? 0;
        public bool IsPhysicsReady { get; private set; }

        public async Task StartAsync(
            DedicatedServerLaunchConfiguration configuration,
            bool loadLevel = true)
        {
            await StartInternalAsync(configuration, null, loadLevel);
        }

        public async Task StartAsync(
            DedicatedServerLaunchConfiguration configuration,
            WorldConfiguration bootstrap,
            bool loadLevel = true)
        {
            await StartInternalAsync(configuration, bootstrap, loadLevel);
        }

        private async Task StartInternalAsync(
            DedicatedServerLaunchConfiguration configuration,
            WorldConfiguration bootstrap,
            bool loadLevel)
        {
            if (initialized) throw new InvalidOperationException("Dedicated Server runtime is already initialized.");
            launch = configuration ?? throw new ArgumentNullException(nameof(configuration));
            initialized = true;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = CharacterPhysicsSubSystem.FixedStepSeconds;
            healthServer = new DedicatedServerHealthServer();
            healthServer.Start(launch.HealthPort, CreateHealth("Starting", null));
            dataServer = new DedicatedDataServer(launch);
            dataServer.MoveReceived += OnMoveReceived;
            Scene levelScene = default;
            if (loadLevel)
            {
                levelScene = await LoadLevelAsync(launch.LevelId);
            }
            else
            {
                levelScene = SceneManager.GetSceneByName(launch.LevelId);
            }
            IDedicatedServerBootstrapConfiguration dedicatedBootstrap = bootstrap as IDedicatedServerBootstrapConfiguration;
            if (bootstrap != null && dedicatedBootstrap == null)
                throw new InvalidOperationException("Dedicated bootstrap configuration is incompatible.");
            if (dedicatedBootstrap != null)
            {
                var dedicatedConfiguration = ScriptableObject.CreateInstance<DedicatedServerWorldConfiguration>();
                dedicatedConfiguration.Initialize(dedicatedBootstrap, levelScene);
                world = World.Create(dedicatedConfiguration);
                physics = world.GetSubSystem<CharacterPhysicsSubSystem>();
            }
            else
            {
                physics = new CharacterPhysicsSubSystem();
                world = World.Create(new WorldSubSystem[] { physics });
            }
            await world.InitializeAsync();

            foreach (DedicatedAuthorityPawnConfiguration pawnConfiguration in launch.AuthorityPawns)
            {
                NetworkPawnBinding binding = new NetworkPawnBinding(
                        pawnConfiguration.PawnId,
                        pawnConfiguration.OwnerPlayerId,
                        pawnConfiguration.PossessionRevision,
                        NetworkPawnRole.Authority);
                Pawn pawn;
                Transform fallbackTransform = null;
                if (dedicatedBootstrap != null)
                {
                    SpawnPointStatus spawn = world.LevelRuntime.GetStatus(pawnConfiguration.SpawnPointId);
                    pawn = await new PawnFactory().CreateRemoteAsync(
                        dedicatedBootstrap.PlayerPawnDefinition,
                        spawn.Transform.position,
                        spawn.Transform.rotation,
                        binding);
                }
                else
                {
                    var root = new GameObject($"AuthorityPawn-{pawnConfiguration.PawnId}");
                    pawn = new Pawn(root, new ActorComponent[] { binding });
                    fallbackTransform = root.transform;
                }
                authorityPawns.Add(pawn);
                pawnRegistrations.Add(world.RegisterActor(pawn, critical: true));
                IAuthorityMoveSimulation simulation = dedicatedBootstrap != null
                    ? new DedicatedMotorMoveSimulation(world, pawn)
                    : new DedicatedTransformMoveSimulation(fallbackTransform);
                moveProcessors.Add(pawnConfiguration.PawnId, new AuthorityMoveProcessor(
                    new AuthorityMoveValidator(
                        pawnConfiguration.ConnectionId,
                        launch.MatchId,
                        pawnConfiguration.PawnId,
                        pawnConfiguration.PossessionRevision),
                    simulation,
                    positionErrorThresholdMillimeters: 50));
            }

            world.StartPlay();
        }

        private static Task<Scene> LoadLevelAsync(string levelId)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(levelId, LoadSceneMode.Additive);
            if (operation == null)
            {
                throw new InvalidOperationException($"Unable to load Dedicated Server level {levelId}.");
            }

            var completion = new TaskCompletionSource<Scene>();
            operation.completed += _ => completion.TrySetResult(SceneManager.GetSceneByName(levelId));
            return completion.Task;
        }

        private void FixedUpdate()
        {
            dataServer?.PollEvents();
            if (world?.State != WorldState.Playing) return;
            authorityServerTick++;
            world.FixedTick(Time.fixedDeltaTime);
            long serverTick = authorityServerTick;
            foreach (KeyValuePair<long, AuthorityMoveProcessor> entry in moveProcessors)
            {
                AuthoritySnapshot snapshot = new AuthoritySnapshot(
                    launch.MatchId,
                    entry.Key,
                    entry.Value.Capture(serverTick));
                dataServer.BroadcastAuthoritySnapshot(snapshot);
            }
            if (!IsPhysicsReady && physics.FixedStepCount > 0)
            {
                IsPhysicsReady = true;
                healthServer.Publish(CreateHealth("PhysicsReady", null));
                Debug.Log($"[DedicatedServer][037] PhysicsReady MatchId={launch.MatchId} AuthorityPawns={authorityPawns.Count} FixedStepCount={physics.FixedStepCount}");
            }
            else if (IsPhysicsReady)
            {
                healthServer.Publish(CreateHealth("PhysicsReady", null));
            }
        }

        private void OnDestroy()
        {
            if (previousFixedDeltaTime > 0f) Time.fixedDeltaTime = previousFixedDeltaTime;
            healthServer?.Dispose();
            dataServer?.Dispose();
            if (world != null)
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
            }
        }

        private void OnMoveReceived(DedicatedDataIdentity identity, PawnMove move)
        {
            if (!moveProcessors.TryGetValue(identity.PawnId, out AuthorityMoveProcessor processor)) return;
            long serverTick = System.Math.Max(1, authorityServerTick);
            OwnerReconcile reconcile = processor.Process(identity.ConnectionId, move, serverTick);
            dataServer.SendOwnerReconcile(identity.PawnId, launch.MatchId, reconcile);
            Debug.Log($"[DedicatedServer][038] ReconcileSent PawnId={identity.PawnId} Sequence={move.Sequence} Kind={reconcile.Kind} Reason={reconcile.Reason ?? "None"}");
        }

        private DedicatedServerHealthSnapshot CreateHealth(string status, string failure)
        {
            return new DedicatedServerHealthSnapshot
            {
                status = status,
                matchId = launch.MatchId,
                dataPort = launch.DataPort,
                healthPort = launch.HealthPort,
                levelId = launch.LevelId,
                contentVersion = launch.ContentVersion,
                authorityPawnCount = authorityPawns.Count,
                fixedStepCount = FixedStepCount,
                failure = failure
            };
        }
    }
}
