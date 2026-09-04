using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace CGame.Network
{
    public sealed class DedicatedServerRuntime : MonoBehaviour
    {
        private const int TargetQueuedMoves = 2;
        private const int MaxSimulationStepsPerFixedUpdate = 4;
        // The three authored EnemyPoints begin 2.5–4m from PlayerPoint 1.
        // Holding at 6m made every newly spawned enemy appear frozen.  Keep a
        // close combat buffer, but require a visible authoritative chase first.
        private const float EnemyHoldRange = 2f;
        // Keep the first combat exchange observable: the client must have time to
        // receive the spawned roster and send its first predicted fire request.
        private const int EnemyInitialAttackDelayTicks = 300;
        private const int EnemyAttackIntervalTicks = 150;
        private const int EnemyAttackDamage = 3;
        private readonly List<Pawn> authorityPawns = new List<Pawn>();
        private readonly List<ActorRegistration> pawnRegistrations = new List<ActorRegistration>();
        private readonly Dictionary<long, AuthorityMoveProcessor> moveProcessors =
            new Dictionary<long, AuthorityMoveProcessor>();
        private readonly Dictionary<long, DedicatedMotorMoveSimulation> motorSimulations =
            new Dictionary<long, DedicatedMotorMoveSimulation>();
        private readonly Dictionary<long, DedicatedPawnCombatState> combatPawnsById =
            new Dictionary<long, DedicatedPawnCombatState>();
        private readonly Dictionary<long, AuthorityMoveInbox> pendingMovesByPawnId =
            new Dictionary<long, AuthorityMoveInbox>();
        private readonly ConcurrentQueue<FireQueryOperation> pendingFireQueries =
            new ConcurrentQueue<FireQueryOperation>();
        private readonly ConcurrentQueue<EnemyDamageOperation> pendingEnemyDamage =
            new ConcurrentQueue<EnemyDamageOperation>();
        private readonly ConcurrentQueue<PawnFireOperation> pendingPawnFire =
            new ConcurrentQueue<PawnFireOperation>();
        private readonly ConcurrentQueue<FireCommitOperation> pendingFireCommits =
            new ConcurrentQueue<FireCommitOperation>();
        private readonly Dictionary<string, DedicatedFireCommitResult> fireCommitResultsByKey =
            new Dictionary<string, DedicatedFireCommitResult>(StringComparer.Ordinal);
        private readonly DedicatedTargetIdentityRegistry targetIdentityRegistry =
            new DedicatedTargetIdentityRegistry();
        private readonly DedicatedCombatIdentityRegistry combatIdentityRegistry =
            new DedicatedCombatIdentityRegistry();
        private readonly List<GameObject> dedicatedTargetRoots = new List<GameObject>();
        private AuthoritativeEnemyRoster authoritativeEnemyRoster;
        private readonly Dictionary<long, DedicatedEnemyEntity> enemiesById = new Dictionary<long, DedicatedEnemyEntity>();
        private GameObject dedicatedEnemyRoot;
        private DedicatedServerLaunchConfiguration launch;
        private DedicatedServerHealthServer healthServer;
        private DedicatedDataServer dataServer;
        private World world;
        private CharacterPhysicsSubSystem physics;
        private long authorityServerTick;
        private readonly Dictionary<long, long> nextEnemyAttackSequenceById = new Dictionary<long, long>();
        private readonly Dictionary<long, long> nextEnemyAttackTickById = new Dictionary<long, long>();
        private readonly Dictionary<long, DedicatedEnemyActionState> latestEnemyActionById = new Dictionary<long, DedicatedEnemyActionState>();
        private float previousFixedDeltaTime;
        private bool initialized;
        private NavMeshDataInstance navigationData;

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
            healthServer.EnemyDamageAsync = QueueEnemyDamageAsync;
            healthServer.PawnFireAsync = QueuePawnFireAsync;
            healthServer.FireCommitAsync = QueueFireCommitAsync;
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
            if (dedicatedBootstrap != null) BuildNavigation(levelScene);
            if (dedicatedBootstrap?.EnemyRosterDefinition != null)
            {
                SpawnAuthoritativeEnemies(dedicatedBootstrap.EnemyRosterDefinition);
                Debug.Log($"[DedicatedServer][051] EnemyRosterReady MatchId={launch.MatchId} Count={authoritativeEnemyRoster.Entities.Count}");
            }
            else if (dedicatedBootstrap != null)
            {
                SpawnDedicatedTargets(dedicatedBootstrap.DedicatedTargetSpawnDefinition);
                Debug.Log($"[DedicatedServer][047] TargetsSpawned MatchId={launch.MatchId} Count={targetIdentityRegistry.TargetIds.Count}");
            }

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
                combatPawnsById.Add(pawnConfiguration.PawnId, new DedicatedPawnCombatState(pawnConfiguration.PawnId, 100, 12, 30, "Default"));
                if (pawn.Root != null)
                {
                    EnsureHitCollider(pawn.Root);
                    combatIdentityRegistry.RegisterPawn(pawnConfiguration.PawnId, pawn.Root);
                }
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
            Scene loadedScene = SceneManager.GetSceneByName(levelId);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                Debug.Log($"[DedicatedServer][047] LevelAlreadyLoaded Scene={levelId}");
                return Task.FromResult(loadedScene);
            }

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
            ProcessEnemyDamage();
            ProcessPawnFire();
            ProcessFireCommits();
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
            StepAuthoritativeEnemies();
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

        private void StepAuthoritativeEnemies()
        {
            if (dedicatedEnemyRoot == null || authorityPawns.Count == 0) return;
            foreach (DedicatedEnemyEntity enemy in dedicatedEnemyRoot.GetComponentsInChildren<DedicatedEnemyEntity>())
            {
                if (enemy.IsDead) continue;
                Pawn targetPawn = SelectNearestAliveAuthorityPawn(enemy.transform.position);
                Transform target = targetPawn?.Root?.transform;
                if (target == null) continue;
                if (Vector3.Distance(enemy.transform.position, target.position) > EnemyHoldRange)
                    enemy.StepChase(target, CharacterPhysicsSubSystem.FixedStepSeconds);
                if (Vector3.Distance(enemy.transform.position, target.position) > EnemyHoldRange) continue;
                if (!nextEnemyAttackTickById.TryGetValue(enemy.EnemyId, out long nextAttackTick))
                {
                    nextEnemyAttackTickById[enemy.EnemyId] = authorityServerTick + EnemyInitialAttackDelayTicks;
                    continue;
                }
                if (authorityServerTick < nextAttackTick) continue;
                Vector3 origin = enemy.transform.position + Vector3.up;
                Vector3 direction = (target.position + Vector3.up - origin).normalized;
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                DedicatedCombatIdentity identity = combatIdentityRegistry.Resolve(hit.collider);
                if (identity.Kind != DedicatedCombatIdentityKind.Pawn || identity.Id != targetPawn.GetComponent<NetworkPawnBinding>().PawnId) continue;
                if (!combatPawnsById.TryGetValue(identity.Id, out DedicatedPawnCombatState pawnState) || pawnState.IsDead) continue;
                nextEnemyAttackSequenceById.TryGetValue(enemy.EnemyId, out long sequence);
                sequence = checked(sequence + 1);
                nextEnemyAttackSequenceById[enemy.EnemyId] = sequence;
                nextEnemyAttackTickById[enemy.EnemyId] = authorityServerTick + EnemyAttackIntervalTicks;
                DedicatedPawnVitalsResult result = pawnState.ApplyEnemyDamage(enemy.EnemyId, sequence, EnemyAttackDamage);
                latestEnemyActionById[enemy.EnemyId] = new DedicatedEnemyActionState
                {
                    enemyId = enemy.EnemyId,
                    actionSequence = sequence,
                    actionKind = "Fire",
                    serverTick = authorityServerTick,
                    vitalsRevision = enemy.VitalsRevision
                };
                Debug.Log($"[DedicatedServer][052] EnemyAttack MatchId={launch.MatchId} EnemyId={enemy.EnemyId} PawnId={identity.Id} ActionSequence={sequence} Health={result.Health} Dead={result.IsDead}");
            }
        }

        private Pawn SelectNearestAliveAuthorityPawn(Vector3 origin)
        {
            Pawn selected = null;
            long selectedId = long.MaxValue;
            float selectedDistanceSquared = float.MaxValue;
            foreach (Pawn pawn in authorityPawns)
            {
                if (pawn?.Root == null || !pawn.TryGetComponent(out NetworkPawnBinding binding) ||
                    !combatPawnsById.TryGetValue(binding.PawnId, out DedicatedPawnCombatState state) || state.IsDead)
                    continue;
                float distanceSquared = (pawn.Root.transform.position - origin).sqrMagnitude;
                if (distanceSquared < selectedDistanceSquared ||
                    (Mathf.Approximately(distanceSquared, selectedDistanceSquared) && binding.PawnId < selectedId))
                {
                    selected = pawn;
                    selectedId = binding.PawnId;
                    selectedDistanceSquared = distanceSquared;
                }
            }
            return selected;
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
            authoritativeEnemyRoster?.Dispose();
            authoritativeEnemyRoster = null;
            if (dedicatedEnemyRoot != null) Destroy(dedicatedEnemyRoot);
            dedicatedEnemyRoot = null;
            if (navigationData.valid) navigationData.Remove();
        }

        private void SpawnAuthoritativeEnemies(EnemyRosterDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.Validate();
            if (authoritativeEnemyRoster != null) throw new InvalidOperationException("Dedicated enemy roster is already created.");

            dedicatedEnemyRoot = new GameObject("DedicatedEnemyRoster");
            try
            {
                var roster = new AuthoritativeEnemyRoster();
                roster.Create(definition.Entries, new DedicatedEnemyRosterFactory(world.LevelRuntime, dedicatedEnemyRoot.transform));
                authoritativeEnemyRoster = roster;
                foreach (DedicatedEnemyEntity enemy in dedicatedEnemyRoot.GetComponentsInChildren<DedicatedEnemyEntity>())
                {
                    EnsureHitCollider(enemy.gameObject);
                    combatIdentityRegistry.RegisterEnemy(enemy.EnemyId, enemy.gameObject);
                    enemiesById.Add(enemy.EnemyId, enemy);
                }
            }
            catch
            {
                if (dedicatedEnemyRoot != null) DestroyImmediate(dedicatedEnemyRoot);
                dedicatedEnemyRoot = null;
                throw;
            }
        }

        private void BuildNavigation(Scene levelScene)
        {
            var sources = new List<NavMeshBuildSource>();
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1000f));
            NavMeshBuilder.CollectSources(
                bounds,
                NavMesh.AllAreas,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                sources);
            if (sources.Count == 0) throw new InvalidOperationException("Dedicated navigation has no build sources.");
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Dedicated navigation build failed.");
            navigationData = NavMesh.AddNavMeshData(data);
            Debug.Log($"[DedicatedServer][051] NavigationReady MatchId={launch.MatchId} Sources={sources.Count}");
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
                    if (behaviour is GameInstance gameInstance)
                    {
                        gameInstance.ShutdownRuntimeWorld();
                    }
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

        private Task<DedicatedEnemyDamageResult> QueueEnemyDamageAsync(DedicatedEnemyDamageRequest request)
        {
            if (request == null) return Task.FromResult(new DedicatedEnemyDamageResult { Accepted = false, Failure = "InvalidRequest" });
            var operation = new EnemyDamageOperation(request);
            pendingEnemyDamage.Enqueue(operation);
            return operation.Completion.Task;
        }

        private void ProcessEnemyDamage()
        {
            while (pendingEnemyDamage.TryDequeue(out EnemyDamageOperation operation))
            {
                DedicatedEnemyDamageRequest request = operation.Request;
                if (!IsPhysicsReady || request.MatchId != launch.MatchId ||
                    !moveProcessors.ContainsKey(request.CausingPawnId) || !enemiesById.TryGetValue(request.EnemyId, out DedicatedEnemyEntity enemy))
                {
                    operation.Completion.TrySetResult(new DedicatedEnemyDamageResult { Accepted = false, Failure = "AuthorityUnavailable" });
                    continue;
                }
            try
            {
                DedicatedDamageResult damage = enemy.ApplyDamage(request.CausingPawnId, request.CausingShotSequence, request.Damage);
                Debug.Log($"[DedicatedServer][052] EnemyDamaged MatchId={launch.MatchId} EnemyId={damage.EnemyId} PawnId={request.CausingPawnId} ShotSequence={request.CausingShotSequence} Health={damage.Health} Revision={damage.VitalsRevision} Dead={damage.IsDead} Replay={damage.IsReplay}");
                operation.Completion.TrySetResult(new DedicatedEnemyDamageResult
                {
                    Accepted = true,
                    EnemyId = damage.EnemyId,
                    Health = damage.Health,
                    MaxHealth = damage.MaxHealth,
                    VitalsRevision = damage.VitalsRevision,
                    IsDead = damage.IsDead,
                    DiedThisHit = damage.DiedThisHit,
                    IsReplay = damage.IsReplay
                });
                if (damage.DiedThisHit)
                {
                    enemiesById.Remove(damage.EnemyId);
                    authoritativeEnemyRoster?.Remove(damage.EnemyId);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                operation.Completion.TrySetResult(new DedicatedEnemyDamageResult { Accepted = false, Failure = "InvalidDamage" });
            }
            }
        }

        private sealed class EnemyDamageOperation
        {
            public EnemyDamageOperation(DedicatedEnemyDamageRequest request)
            {
                Request = request;
                Completion = new TaskCompletionSource<DedicatedEnemyDamageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public DedicatedEnemyDamageRequest Request { get; }
            public TaskCompletionSource<DedicatedEnemyDamageResult> Completion { get; }
        }

        private Task<DedicatedPawnFireResult> QueuePawnFireAsync(DedicatedPawnFireRequest request)
        {
            if (request == null) return Task.FromResult(new DedicatedPawnFireResult { Accepted = false, Failure = "InvalidRequest" });
            var operation = new PawnFireOperation(request);
            pendingPawnFire.Enqueue(operation);
            return operation.Completion.Task;
        }

        private Task<DedicatedFireCommitResult> QueueFireCommitAsync(DedicatedFireCommitRequest request)
        {
            if (request == null) return Task.FromResult(new DedicatedFireCommitResult { Accepted = false, Failure = "InvalidRequest" });
            var operation = new FireCommitOperation(request);
            pendingFireCommits.Enqueue(operation);
            return operation.Completion.Task;
        }

        private void ProcessFireCommits()
        {
            while (pendingFireCommits.TryDequeue(out FireCommitOperation operation))
            {
                DedicatedFireCommitRequest request = operation.Request;
                string key = $"{request.PawnId}:{request.ClientShotSequence}";
                if (fireCommitResultsByKey.TryGetValue(key, out DedicatedFireCommitResult replay))
                {
                    replay.IsReplay = true;
                    operation.Completion.TrySetResult(replay);
                    continue;
                }
                DedicatedFireCommitResult result = CommitFire(request);
                fireCommitResultsByKey.Add(key, result);
                operation.Completion.TrySetResult(result);
            }
        }

        private DedicatedFireCommitResult CommitFire(DedicatedFireCommitRequest request)
        {
            if (!IsPhysicsReady || request.MatchId != launch.MatchId || !moveProcessors.ContainsKey(request.PawnId) ||
                !combatPawnsById.TryGetValue(request.PawnId, out DedicatedPawnCombatState pawn) || pawn.IsDead)
                return new DedicatedFireCommitResult { Accepted = false, Failure = "AuthorityUnavailable" };

            Vector3 origin = new Vector3(request.OriginX, request.OriginY, request.OriginZ);
            Vector3 direction = new Vector3(request.DirectionX, request.DirectionY, request.DirectionZ);
            if (direction.sqrMagnitude <= Mathf.Epsilon || request.Range <= 0f || request.Damage <= 0)
                return new DedicatedFireCommitResult { Accepted = false, Failure = "InvalidFire" };

            DedicatedPawnEquipmentResult equipment = pawn.TryConsumeFire(request.ClientShotSequence);
            if (!equipment.Accepted)
                return new DedicatedFireCommitResult
                {
                    Accepted = false, MagazineAmmo = equipment.MagazineAmmo, MagazineCapacity = equipment.MagazineCapacity,
                    EquipmentRevision = equipment.EquipmentRevision, Failure = pawn.IsDead ? "AuthorityUnavailable" : "OutOfAmmo"
                };

            bool hit = Physics.Raycast(origin, direction.normalized, out RaycastHit raycastHit, request.Range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            DedicatedCombatIdentity identity = hit ? combatIdentityRegistry.Resolve(raycastHit.collider) : default;
            var result = new DedicatedFireCommitResult
            {
                Accepted = true, MagazineAmmo = equipment.MagazineAmmo, MagazineCapacity = equipment.MagazineCapacity,
                EquipmentRevision = equipment.EquipmentRevision, Hit = hit,
                PositionX = hit ? raycastHit.point.x : 0f, PositionY = hit ? raycastHit.point.y : 0f, PositionZ = hit ? raycastHit.point.z : 0f,
                NormalX = hit ? raycastHit.normal.x : 0f, NormalY = hit ? raycastHit.normal.y : 0f, NormalZ = hit ? raycastHit.normal.z : 0f,
                SurfaceId = hit ? (raycastHit.collider.sharedMaterial?.name ?? raycastHit.collider.name) : string.Empty,
                TargetId = hit ? targetIdentityRegistry.Resolve(raycastHit.collider) : null,
                HitEnemyId = identity.Kind == DedicatedCombatIdentityKind.Enemy ? identity.Id : 0,
                HitPawnId = identity.Kind == DedicatedCombatIdentityKind.Pawn ? identity.Id : 0
            };
            if (result.HitEnemyId > 0 && enemiesById.TryGetValue(result.HitEnemyId, out DedicatedEnemyEntity enemy))
            {
                DedicatedDamageResult damage = enemy.ApplyDamage(request.PawnId, request.ClientShotSequence, request.Damage);
                result.EnemyDamage = new DedicatedEnemyDamageResult
                {
                    Accepted = true, EnemyId = damage.EnemyId, Health = damage.Health, MaxHealth = damage.MaxHealth,
                    VitalsRevision = damage.VitalsRevision, IsDead = damage.IsDead, DiedThisHit = damage.DiedThisHit, IsReplay = damage.IsReplay
                };
                if (damage.DiedThisHit)
                {
                    enemiesById.Remove(damage.EnemyId);
                    authoritativeEnemyRoster?.Remove(damage.EnemyId);
                }
            }
            return result;
        }

        private sealed class FireCommitOperation
        {
            public FireCommitOperation(DedicatedFireCommitRequest request)
            {
                Request = request;
                Completion = new TaskCompletionSource<DedicatedFireCommitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            public DedicatedFireCommitRequest Request { get; }
            public TaskCompletionSource<DedicatedFireCommitResult> Completion { get; }
        }

        private void ProcessPawnFire()
        {
            while (pendingPawnFire.TryDequeue(out PawnFireOperation operation))
            {
                DedicatedPawnFireRequest request = operation.Request;
                if (!IsPhysicsReady || request.MatchId != launch.MatchId || !combatPawnsById.TryGetValue(request.PawnId, out DedicatedPawnCombatState pawn))
                {
                    operation.Completion.TrySetResult(new DedicatedPawnFireResult { Accepted = false, Failure = "AuthorityUnavailable" });
                    continue;
                }
                DedicatedPawnEquipmentResult result = pawn.TryConsumeFire(request.ClientShotSequence);
                operation.Completion.TrySetResult(new DedicatedPawnFireResult
                {
                    Accepted = result.Accepted,
                    MagazineAmmo = result.MagazineAmmo,
                    MagazineCapacity = result.MagazineCapacity,
                    EquipmentRevision = result.EquipmentRevision,
                    Failure = result.Accepted ? null : "OutOfAmmo"
                });
            }
        }

        private sealed class PawnFireOperation
        {
            public PawnFireOperation(DedicatedPawnFireRequest request)
            {
                Request = request;
                Completion = new TaskCompletionSource<DedicatedPawnFireResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            public DedicatedPawnFireRequest Request { get; }
            public TaskCompletionSource<DedicatedPawnFireResult> Completion { get; }
        }

        private void ProcessFireQueries()
        {
            while (pendingFireQueries.TryDequeue(out FireQueryOperation operation))
            {
                DedicatedFireQueryRequest request = operation.Request;
                if (!IsPhysicsReady || request.MatchId != launch.MatchId ||
                    !moveProcessors.ContainsKey(request.PawnId) ||
                    !combatPawnsById.TryGetValue(request.PawnId, out DedicatedPawnCombatState pawnState) || pawnState.IsDead)
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
                DedicatedCombatIdentity identity = hit ? combatIdentityRegistry.Resolve(raycastHit.collider) : default;
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
                    TargetId = hit ? targetIdentityRegistry.Resolve(raycastHit.collider) : null,
                    HitEnemyId = identity.Kind == DedicatedCombatIdentityKind.Enemy ? identity.Id : 0,
                    HitPawnId = identity.Kind == DedicatedCombatIdentityKind.Pawn ? identity.Id : 0
                });
            }
        }

        private static void EnsureHitCollider(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) != null) return;
            root.AddComponent<CapsuleCollider>();
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
            if (!combatPawnsById.TryGetValue(identity.PawnId, out DedicatedPawnCombatState pawnState) || pawnState.IsDead)
            {
                Debug.Log($"[DedicatedServer][052] PawnInputIgnored MatchId={launch.MatchId} PawnId={identity.PawnId} Reason=PawnUnavailable");
                return;
            }
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
                enemySpawns = CollectEnemyHealthStates(),
                enemyActions = CollectEnemyActions(),
                pawnStates = CollectPawnHealthStates(),
                failure = failure
            };
        }

        private DedicatedEnemyHealthState[] CollectEnemyHealthStates()
        {
            if (dedicatedEnemyRoot == null) return Array.Empty<DedicatedEnemyHealthState>();
            DedicatedEnemyEntity[] entities = dedicatedEnemyRoot.GetComponentsInChildren<DedicatedEnemyEntity>();
            var values = new DedicatedEnemyHealthState[entities.Length];
            for (int index = 0; index < entities.Length; index++)
            {
                Transform transform = entities[index].transform;
                Quaternion rotation = transform.rotation;
                Vector3 position = transform.position;
                values[index] = new DedicatedEnemyHealthState
                {
                    enemyId = entities[index].EnemyId,
                    archetypeId = entities[index].ArchetypeId,
                    positionX = position.x,
                    positionY = position.y,
                    positionZ = position.z,
                    rotationX = rotation.x,
                    rotationY = rotation.y,
                    rotationZ = rotation.z,
                    rotationW = rotation.w,
                    velocityX = entities[index].PlanarVelocity.x,
                    velocityZ = entities[index].PlanarVelocity.z,
                    health = entities[index].Health
                };
            }
            return values;
        }

        private DedicatedEnemyActionState[] CollectEnemyActions()
        {
            var values = new DedicatedEnemyActionState[latestEnemyActionById.Count];
            int index = 0;
            foreach (DedicatedEnemyActionState action in latestEnemyActionById.Values)
                values[index++] = action;
            return values;
        }

        private DedicatedPawnHealthState[] CollectPawnHealthStates()
        {
            var values = new DedicatedPawnHealthState[combatPawnsById.Count];
            int index = 0;
            foreach (DedicatedPawnCombatState pawn in combatPawnsById.Values)
            {
                values[index++] = new DedicatedPawnHealthState
                {
                    pawnId = pawn.PawnId,
                    vitalsRevision = pawn.VitalsRevision,
                    health = pawn.Health,
                    maxHealth = pawn.MaxHealth,
                    isDead = pawn.IsDead,
                    equipmentRevision = pawn.EquipmentRevision,
                    weaponName = pawn.WeaponName,
                    magazineAmmo = pawn.MagazineAmmo,
                    magazineCapacity = pawn.MagazineCapacity
                };
            }
            return values;
        }
    }
}
