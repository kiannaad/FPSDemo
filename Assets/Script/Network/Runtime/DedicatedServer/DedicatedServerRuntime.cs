using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        private readonly ConcurrentQueue<FireQueryOperation> pendingFireQueries =
            new ConcurrentQueue<FireQueryOperation>();
        private readonly DedicatedTargetIdentityRegistry targetIdentityRegistry =
            new DedicatedTargetIdentityRegistry();
        private readonly List<GameObject> dedicatedTargetRoots = new List<GameObject>();
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
            healthServer.FireQueryAsync = QueueFireQueryAsync;
            healthServer.Start(launch.HealthPort, CreateHealth("Starting", null));
            dataServer = new DedicatedDataServer(launch);
            dataServer.MoveReceived += OnMoveReceived;
            Scene levelScene = default;
            if (loadLevel)
            {
                levelScene = await LoadLevelAsync(launch.LevelId);
                Debug.Log($"[DedicatedServer][047] LevelLoaded MatchId={launch.MatchId} Scene={levelScene.name}");
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
                DisableLevelBehaviours(levelScene);
                var dedicatedConfiguration = ScriptableObject.CreateInstance<DedicatedServerWorldConfiguration>();
                dedicatedConfiguration.Initialize(dedicatedBootstrap, levelScene);
                world = World.Create(dedicatedConfiguration);
                physics = world.GetSubSystem<CharacterPhysicsSubSystem>();
                Debug.Log($"[DedicatedServer][047] WorldCreated MatchId={launch.MatchId}");
            }
            else
            {
                physics = new CharacterPhysicsSubSystem();
                world = World.Create(new WorldSubSystem[] { physics });
            }
            await world.InitializeAsync();
            Debug.Log($"[DedicatedServer][047] WorldInitialized MatchId={launch.MatchId}");
            if (dedicatedBootstrap != null) SpawnDedicatedTargets(dedicatedBootstrap.DedicatedTargetSpawnDefinition);
            Debug.Log($"[DedicatedServer][047] TargetsSpawned MatchId={launch.MatchId} Count={targetIdentityRegistry.TargetIds.Count}");

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
                    pawn = CreateDedicatedAuthorityPawn(
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
                Debug.Log($"[DedicatedServer][047] AuthorityPawnReady MatchId={launch.MatchId} PawnId={pawnConfiguration.PawnId}");
                pawnRegistrations.Add(world.RegisterActor(pawn, critical: true));
                Debug.Log($"[DedicatedServer][047] AuthorityPawnRegistered MatchId={launch.MatchId} PawnId={pawnConfiguration.PawnId}");
                IAuthorityMoveSimulation simulation;
                if (dedicatedBootstrap != null)
                {
                    var motorSimulation = new DedicatedMotorMoveSimulation(world, pawn);
                    motorSimulations.Add(pawnConfiguration.PawnId, motorSimulation);
                    pendingMovesByPawnId.Add(pawnConfiguration.PawnId, new AuthorityMoveInbox());
                    simulation = motorSimulation;
                    Debug.Log($"[DedicatedServer][047] AuthorityPawnSimulationReady MatchId={launch.MatchId} PawnId={pawnConfiguration.PawnId}");
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

            Debug.Log($"[DedicatedServer][047] WorldStartPlayRequested MatchId={launch.MatchId}");
            world.StartPlay();
            Debug.Log($"[DedicatedServer][047] WorldPlaying MatchId={launch.MatchId}");
        }

        private static Pawn CreateDedicatedAuthorityPawn(
            PawnDefinition definition,
            Vector3 position,
            Quaternion rotation,
            NetworkPawnBinding binding)
        {
            if (definition == null || definition.PawnPrefab == null)
                throw new InvalidOperationException("Dedicated authority PawnDefinition must specify a PawnPrefab.");

            GameObject root = null;
            try
            {
                root = Instantiate(definition.PawnPrefab, position, rotation);
                root.name = $"DedicatedAuthorityPawn:{definition.name}";
                root.SetActive(false);
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;

                CharacterPhysicsMotor motor = root.GetComponent<CharacterPhysicsMotor>();
                if (motor == null)
                    throw new InvalidOperationException("Dedicated authority PawnPrefab requires a CharacterPhysicsMotor.");
                foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    behaviour.enabled = behaviour is CharacterPhysicsMotor;
                }
                return new Pawn(root, new ActorComponent[]
                {
                    new PawnMovementComponent(motor),
                    binding
                });
            }
            catch
            {
                if (root != null) Destroy(root);
                throw;
            }
        }

        private static Task<Scene> LoadLevelAsync(string levelId)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(levelId, LoadSceneMode.Additive);
            if (operation == null)
            {
                throw new InvalidOperationException($"Unable to load Dedicated Server level {levelId}.");
            }

            var completion = new TaskCompletionSource<Scene>();
            Debug.Log($"[DedicatedServer][047] LevelLoadRequested Scene={levelId} IsDone={operation.isDone}");
            if (operation.isDone)
            {
                completion.TrySetResult(SceneManager.GetSceneByName(levelId));
            }
            else
            {
                operation.completed += _ =>
                {
                    Debug.Log($"[DedicatedServer][047] LevelLoadCompleted Scene={levelId}");
                    completion.TrySetResult(SceneManager.GetSceneByName(levelId));
                };
            }
            return completion.Task;
        }

        private void Update()
        {
            dataServer?.PollEvents();
            ProcessFireQueries();
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
            world.FixedTick(CharacterPhysicsSubSystem.FixedStepSeconds);
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
            while (pendingFireQueries.TryDequeue(out FireQueryOperation operation))
            {
                operation.Completion.TrySetResult(new DedicatedFireQueryResult
                {
                    Accepted = false,
                    Failure = "AuthorityShutdown"
                });
            }
            if (previousFixedDeltaTime > 0f) Time.fixedDeltaTime = previousFixedDeltaTime;
            healthServer?.Dispose();
            dataServer?.Dispose();
            if (world != null)
            {
                world.ShutdownAsync().GetAwaiter().GetResult();
            }
            for (int index = dedicatedTargetRoots.Count - 1; index >= 0; index--)
            {
                if (dedicatedTargetRoots[index] != null) Destroy(dedicatedTargetRoots[index]);
            }
            dedicatedTargetRoots.Clear();
        }

        private void SpawnDedicatedTargets(DedicatedTargetSpawnDefinition definition)
        {
            if (definition == null) throw new InvalidOperationException("Dedicated target spawn definition is missing.");
            IReadOnlyList<SpawnPointReservation> reservations = world.LevelRuntime.ReserveRandomEnemyPoints(
                definition.Count,
                new System.Random(20260830));
            try
            {
                for (int index = 0; index < reservations.Count; index++)
                {
                    SpawnPointReservation reservation = reservations[index];
                    GameObject target = Instantiate(
                        definition.Prefab,
                        reservation.Transform.position,
                        reservation.Transform.rotation);
                    target.name = $"DedicatedTarget-{reservation.PointId}";
                    foreach (Behaviour behaviour in target.GetComponentsInChildren<Behaviour>(true))
                    {
                        behaviour.enabled = false;
                    }
                    targetIdentityRegistry.Register(reservation.PointId, target);
                    dedicatedTargetRoots.Add(target);
                }
            }
            catch
            {
                for (int index = dedicatedTargetRoots.Count - 1; index >= 0; index--)
                    if (dedicatedTargetRoots[index] != null) DestroyImmediate(dedicatedTargetRoots[index]);
                dedicatedTargetRoots.Clear();
                throw;
            }
            finally
            {
                for (int index = reservations.Count - 1; index >= 0; index--) reservations[index].Dispose();
            }
        }

        private static void DisableLevelBehaviours(Scene levelScene)
        {
            if (!levelScene.IsValid() || !levelScene.isLoaded)
            {
                throw new InvalidOperationException("Dedicated Server level is not loaded.");
            }

            int disabledCount = 0;
            foreach (GameObject root in levelScene.GetRootGameObjects())
            {
                foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (!behaviour.enabled) continue;
                    behaviour.enabled = false;
                    disabledCount++;
                }
            }

            Debug.Log($"[DedicatedServer][047] LevelBehavioursDisabled Scene={levelScene.name} Count={disabledCount}");
        }

        private Task<DedicatedFireQueryResult> QueueFireQueryAsync(DedicatedFireQueryRequest request)
        {
            var operation = new FireQueryOperation(request);
            pendingFireQueries.Enqueue(operation);
            return operation.Completion.Task;
        }

        private void ProcessFireQueries()
        {
            while (pendingFireQueries.TryDequeue(out FireQueryOperation operation))
            {
                DedicatedFireQueryRequest request = operation.Request;
                if (!IsPhysicsReady || request.MatchId != launch.MatchId ||
                    !moveProcessors.ContainsKey(request.PawnId))
                {
                    operation.Completion.TrySetResult(new DedicatedFireQueryResult { Accepted = false, Failure = "AuthorityUnavailable" });
                    continue;
                }

                Vector3 origin = new Vector3(request.OriginX, request.OriginY, request.OriginZ);
                Vector3 direction = new Vector3(request.DirectionX, request.DirectionY, request.DirectionZ);
                if (direction.sqrMagnitude <= Mathf.Epsilon || request.Range <= 0f)
                {
                    operation.Completion.TrySetResult(new DedicatedFireQueryResult { Accepted = false, Failure = "InvalidRay" });
                    continue;
                }

                bool hit = Physics.Raycast(
                    origin,
                    direction.normalized,
                    out RaycastHit raycastHit,
                    request.Range,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                operation.Completion.TrySetResult(new DedicatedFireQueryResult
                {
                    Accepted = true,
                    Hit = hit,
                    PositionX = hit ? raycastHit.point.x : 0f,
                    PositionY = hit ? raycastHit.point.y : 0f,
                    PositionZ = hit ? raycastHit.point.z : 0f,
                    NormalX = hit ? raycastHit.normal.x : 0f,
                    NormalY = hit ? raycastHit.normal.y : 0f,
                    NormalZ = hit ? raycastHit.normal.z : 0f,
                    SurfaceId = hit ? (raycastHit.collider.sharedMaterial?.name ?? raycastHit.collider.name) : string.Empty,
                    TargetId = hit ? targetIdentityRegistry.Resolve(raycastHit.collider) : null
                });
            }
        }

        private sealed class FireQueryOperation
        {
            public FireQueryOperation(DedicatedFireQueryRequest request)
            {
                Request = request;
                Completion = new TaskCompletionSource<DedicatedFireQueryResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public DedicatedFireQueryRequest Request { get; }
            public TaskCompletionSource<DedicatedFireQueryResult> Completion { get; }
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
                targetIds = new List<string>(targetIdentityRegistry.TargetIds).ToArray(),
                failure = failure
            };
        }
    }
}
