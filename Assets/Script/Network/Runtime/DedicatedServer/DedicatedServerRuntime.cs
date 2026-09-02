using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Network
{
    public sealed class DedicatedServerRuntime : MonoBehaviour
    {
        private const int TargetQueuedMoves = 2;
        private const int MaxSimulationStepsPerFixedUpdate = 4;
        private readonly List<Pawn> authorityPawns = new List<Pawn>();
        private readonly List<ActorRegistration> pawnRegistrations = new List<ActorRegistration>();
        private readonly Dictionary<long, AuthorityMoveProcessor> moveProcessors =
            new Dictionary<long, AuthorityMoveProcessor>();
        private readonly Dictionary<long, DedicatedMotorMoveSimulation> motorSimulations =
            new Dictionary<long, DedicatedMotorMoveSimulation>();
        private readonly Dictionary<long, AuthorityMoveInbox> pendingMovesByPawnId =
            new Dictionary<long, AuthorityMoveInbox>();
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
                IAuthorityMoveSimulation simulation;
                if (dedicatedBootstrap != null)
                {
                    var motorSimulation = new DedicatedMotorMoveSimulation(world, pawn);
                    motorSimulations.Add(pawnConfiguration.PawnId, motorSimulation);
                    pendingMovesByPawnId.Add(pawnConfiguration.PawnId, new AuthorityMoveInbox());
                    simulation = motorSimulation;
                }
                else
                {
                    simulation = new DedicatedTransformMoveSimulation(fallbackTransform);
                }
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

            int simulationSteps = GetSimulationStepCount();
            for (int step = 0; step < simulationSteps; step++)
            {
                SimulateAuthorityStep();
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

        private int GetSimulationStepCount()
        {
            int simulationSteps = 1;
            foreach (AuthorityMoveInbox inbox in pendingMovesByPawnId.Values)
            {
                simulationSteps = Mathf.Max(
                    simulationSteps,
                    inbox.GetSimulationStepCount(TargetQueuedMoves, MaxSimulationStepsPerFixedUpdate));
            }

            return simulationSteps;
        }

        private void SimulateAuthorityStep()
        {
            authorityServerTick++;
            long serverTick = authorityServerTick;
            var movesForTick = new List<KeyValuePair<long, AcceptedAuthorityMove>>();
            foreach (KeyValuePair<long, AuthorityMoveInbox> entry in pendingMovesByPawnId)
            {
                if (!entry.Value.TryDequeue(out AcceptedAuthorityMove pending)) continue;
                movesForTick.Add(new KeyValuePair<long, AcceptedAuthorityMove>(entry.Key, pending));
                motorSimulations[entry.Key].ApplyControlIntent(pending.Move);
            }
            world.FixedTick(Time.fixedDeltaTime);
            foreach (KeyValuePair<long, AcceptedAuthorityMove> entry in movesForTick)
            {
                DedicatedMotorMoveSimulation simulation = motorSimulations[entry.Key];
                simulation.ClearControlIntent();
                AuthorityState state = simulation.Capture(serverTick);
                OwnerReconcile reconcile = moveProcessors[entry.Key].ReconcileAccepted(entry.Value.Move, state);
                SendReconcile(entry.Value.Identity, entry.Value.Move, reconcile);
            }
            foreach (KeyValuePair<long, AuthorityMoveProcessor> entry in moveProcessors)
            {
                AuthoritySnapshot snapshot = new AuthoritySnapshot(
                    launch.MatchId,
                    entry.Key,
                    entry.Value.Capture(serverTick));
                dataServer.BroadcastAuthoritySnapshot(snapshot);
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
            if (motorSimulations.ContainsKey(identity.PawnId))
            {
                AuthorityMoveValidation validation = processor.Validate(identity.ConnectionId, move, serverTick);
                if (!validation.Accepted)
                {
                    SendReconcile(identity, move, processor.Reject(validation, serverTick));
                    return;
                }

                pendingMovesByPawnId[identity.PawnId].Enqueue(identity, move);
                return;
            }

            OwnerReconcile reconcile = processor.Process(identity.ConnectionId, move, serverTick);
            SendReconcile(identity, move, reconcile);
        }

        private void SendReconcile(DedicatedDataIdentity identity, PawnMove move, OwnerReconcile reconcile)
        {
            dataServer.SendOwnerReconcile(identity.PawnId, launch.MatchId, reconcile);
            if (reconcile.Kind != OwnerReconcileKind.Correction || move.Sequence % 60 != 0) return;
            long positionError = CalculatePositionErrorMillimeters(
                move.PredictedPosition,
                reconcile.AuthorityState.Position);
            int queuedMoves = pendingMovesByPawnId.TryGetValue(identity.PawnId, out AuthorityMoveInbox inbox)
                ? inbox.Count
                : 0;
            Debug.LogWarning(
                $"[DedicatedServer][038] CorrectionSent PawnId={identity.PawnId} Sequence={move.Sequence} "
                + $"Reason={reconcile.Reason ?? "None"} PositionErrorMm={positionError} QueuedMoves={queuedMoves} "
                + $"Predicted={move.PredictedPosition.XMillimeters},{move.PredictedPosition.YMillimeters},{move.PredictedPosition.ZMillimeters} "
                + $"Authority={reconcile.AuthorityState.Position.XMillimeters},{reconcile.AuthorityState.Position.YMillimeters},{reconcile.AuthorityState.Position.ZMillimeters}");
        }

        private static long CalculatePositionErrorMillimeters(QuantizedVector3 predicted, QuantizedVector3 authority)
        {
            long x = (long)predicted.XMillimeters - authority.XMillimeters;
            long y = (long)predicted.YMillimeters - authority.YMillimeters;
            long z = (long)predicted.ZMillimeters - authority.ZMillimeters;
            return (long)Mathf.Round(Mathf.Sqrt(x * x + y * y + z * z));
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
