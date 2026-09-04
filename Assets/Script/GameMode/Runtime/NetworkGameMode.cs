using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using CGame.Network;
using UnityEngine;

namespace CGame
{
    public sealed class NetworkGameMode : GameMode, INetworkLobby, INetworkPrePlayExecution
    {
        private readonly PlayerStateDefinition playerStateDefinition;
        private readonly ClientNetworkSubSystem network;
        private readonly PawnFactory pawnFactory;
        private readonly Dictionary<long, ActorRegistration> remotePawnRegistrationsById = new Dictionary<long, ActorRegistration>();
        private readonly Dictionary<long, RemoteSnapshotBuffer> remoteSnapshotBuffersById = new Dictionary<long, RemoteSnapshotBuffer>();
        private readonly Dictionary<long, RemoteSnapshotPresentation> remoteSnapshotPresentationsById = new Dictionary<long, RemoteSnapshotPresentation>();
        private readonly Dictionary<long, NetworkAnimationActionBridge> remoteAnimationActionBridgesById = new Dictionary<long, NetworkAnimationActionBridge>();
        private readonly Dictionary<long, RemotePawnNetworkAnimationActionPresenter> remoteFirePresentersById = new Dictionary<long, RemotePawnNetworkAnimationActionPresenter>();
        private readonly NetworkPawnAnimationActionRouter animationActionRouter = new NetworkPawnAnimationActionRouter();
        private readonly NetworkTickClock networkTickClock = new NetworkTickClock();
        private readonly NetworkTargetRegistry targetRegistry = new NetworkTargetRegistry();
        private readonly EnemyReplicationTracker enemyReplicationTracker = new EnemyReplicationTracker();
        private readonly EnemyPresentationRegistry enemyPresentationRegistry;
        private EnemySpawnGameComponent enemySpawnComponent;
        private readonly List<ClientPawnState> pendingRemotePawns = new List<ClientPawnState>();
        private readonly TaskCompletionSource<bool> initialOwnerPawnReady = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> networkStartReady = new TaskCompletionSource<bool>();
        private ActorRegistration ownerPawnRegistration;
        private readonly LocalMovementPredictionCoordinator movementPrediction;
        private CharacterPhysicsSubSystem characterPhysics;
        private Pawn ownerPawn;
        private NetworkPawnBinding ownerNetworkBinding;
        private NetworkAnimationActionBridge ownerAnimationActionBridge;
        private NetworkFireBridge ownerFireBridge;
        private OwnerGameplayHud ownerGameplayHud;
        private OwnerGameplayStateEvent latestOwnerGameplayState;
        private readonly NetworkLoopAnimationPhaseSynchronizer ownerLoopPhaseSynchronizer =
            new NetworkLoopAnimationPhaseSynchronizer();
        private bool ownerPawnSpawnRequested;
        private long startedMatchId;

        public NetworkGameMode(
            World world,
            Player player,
            PlayerStateDefinition playerStateDefinition,
            ClientNetworkSubSystem network,
            PawnFactory pawnFactory = null,
            EnemyPresentationCatalog enemyPresentationCatalog = null)
            : base(world, player)
        {
            this.playerStateDefinition = playerStateDefinition ?? throw new ArgumentNullException(nameof(playerStateDefinition));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.pawnFactory = pawnFactory ?? new PawnFactory();
            enemyPresentationRegistry = enemyPresentationCatalog == null ? null : new EnemyPresentationRegistry(enemyPresentationCatalog);
            movementPrediction = new LocalMovementPredictionCoordinator(network);
            network.ClientWorld.PawnSpawned += OnPawnSpawned;
            network.ClientWorld.OwnerPossessionApplied += OnOwnerPossessionApplied;
            network.ClientWorld.MatchStarting += OnMatchStarting;
            network.OwnerReconcileReceived += OnOwnerReconcileReceived;
            network.AuthoritySnapshotReceived += OnAuthoritySnapshotReceived;
            network.AnimationActionStartedReceived += OnAnimationActionStartedReceived;
            network.AnimationActionTerminalReceived += OnAnimationActionTerminalReceived;
            network.FireCommittedReceived += OnFireCommittedReceived;
            network.FireRejectedReceived += OnFireRejectedReceived;
            network.TargetStateChangedReceived += OnTargetStateChangedReceived;
            network.TargetStateSnapshotReceived += OnTargetStateSnapshotReceived;
            network.EnemySpawnedReceived += OnEnemySpawnedReceived;
            network.EnemySnapshotReceived += OnEnemySnapshotReceived;
            network.EnemyActionReceived += OnEnemyActionReceived;
            network.OwnerGameplayStateReceived += OnOwnerGameplayStateReceived;
            if (world.GameState is DefaultGameState gameState &&
                gameState.ExperienceManager.Components.TryGet(out enemySpawnComponent))
            {
                // Network matches render only Dedicated EnemyId entities.  The older
                // Experience target feature has no network identity and must not
                // produce a parallel local enemy roster on a client.
                enemySpawnComponent.Dispose();
                enemySpawnComponent = null;
                Debug.Log("[Network][052] LegacyLocalTargetsSuppressed");
            }
            world.GameplayReady += OnGameplayReady;
            characterPhysics = world.GetSubSystem<CharacterPhysicsSubSystem>();
            characterPhysics.FixedStepCompleted += OnFixedStepCompleted;
        }

        public string RoomId { get; private set; } = string.Empty;
        public string NetworkStatus { get; private set; } = "Connecting";
        public int SavedMoveCount => movementPrediction.SavedMoveCount;
        public OwnerReconcileKind? LastReconcileKind => movementPrediction.LastReconcileKind;
        public int ReplayCompletedCount => movementPrediction.ReplayCompletedCount;
        public int FixedStepObservedCount { get; private set; }
        public int MoveCreatedCount => movementPrediction.MoveCreatedCount;
        public string MovementPredictionDiagnostic => movementPrediction.Diagnostic;
        public long CharacterPhysicsFixedStepCount => characterPhysics?.FixedStepCount ?? 0;
        public int AuthoritySnapshotReceivedCount { get; private set; }
        public int SnapshotAppliedCount { get; private set; }
        public AuthoritySnapshot? LastOwnerAuditSnapshot { get; private set; }
        public int RemoteAnimationActionPlayedCount
        {
            get
            {
                int total = 0;
                foreach (NetworkAnimationActionBridge bridge in remoteAnimationActionBridgesById.Values)
                    total += bridge.PlayedActionCount;
                return total;
            }
        }
        public int RemoteAnimationActionPresentationUnavailableCount
        {
            get
            {
                int total = 0;
                foreach (NetworkAnimationActionBridge bridge in remoteAnimationActionBridgesById.Values)
                    total += bridge.PresentationUnavailableCount;
                return total;
            }
        }

        protected override Controller CreatePlayerController(Player player)
        {
            return CGame.PlayerController.Create(
                player,
                playerStateDefinition,
                player.GetSubSystem<InputSubSystem>(),
                new DefaultPlayerControllerComponentFactory());
        }

        public override Task SpawnDefaultPawn(Controller playerController, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void PumpPrePlay()
        {
            network.PumpPrePlay();
            if (network.IsHelloComplete && NetworkStatus == "Connecting") NetworkStatus = "Connected";
            if (!string.IsNullOrEmpty(network.Failure))
            {
                NetworkStatus = network.Failure;
                initialOwnerPawnReady.TrySetException(new InvalidOperationException(network.Failure));
            }
        }

        public async Task WaitForInitialOwnerPawnAsync(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => initialOwnerPawnReady.TrySetCanceled(cancellationToken)))
            {
                await initialOwnerPawnReady.Task;
            }
        }

        public async Task WaitForNetworkStartAsync(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => networkStartReady.TrySetCanceled(cancellationToken)))
            {
                await networkStartReady.Task;
            }
        }

        public async Task CreateRoomAsync()
        {
            CreateRoomResponse room = await network.CreateRoomAsync("sample-scene");
            RoomId = room.RoomId;
            NetworkStatus = $"Room {RoomId} created";
        }

        public async Task JoinRoomAsync(string roomId)
        {
            JoinRoomResponse room = await network.JoinRoomAsync(roomId);
            RoomId = room.RoomId;
            NetworkStatus = $"Joined {RoomId}";
        }

        public async Task SetReadyAsync(bool isReady)
        {
            if (string.IsNullOrWhiteSpace(RoomId)) throw new InvalidOperationException("Create or join a room before Ready.");
            SetReadyResponse response = await network.SetReadyAsync(RoomId, isReady);
            NetworkStatus = response.MatchStarted ? "Match starting" : $"Ready={isReady}";
        }

        protected override void OnShutdown()
        {
            network.ClientWorld.PawnSpawned -= OnPawnSpawned;
            network.ClientWorld.OwnerPossessionApplied -= OnOwnerPossessionApplied;
            network.ClientWorld.MatchStarting -= OnMatchStarting;
            network.OwnerReconcileReceived -= OnOwnerReconcileReceived;
            network.AuthoritySnapshotReceived -= OnAuthoritySnapshotReceived;
            network.AnimationActionStartedReceived -= OnAnimationActionStartedReceived;
            network.AnimationActionTerminalReceived -= OnAnimationActionTerminalReceived;
            network.FireCommittedReceived -= OnFireCommittedReceived;
            network.FireRejectedReceived -= OnFireRejectedReceived;
            network.TargetStateChangedReceived -= OnTargetStateChangedReceived;
            network.TargetStateSnapshotReceived -= OnTargetStateSnapshotReceived;
            network.EnemySpawnedReceived -= OnEnemySpawnedReceived;
            network.EnemySnapshotReceived -= OnEnemySnapshotReceived;
            network.EnemyActionReceived -= OnEnemyActionReceived;
            network.OwnerGameplayStateReceived -= OnOwnerGameplayStateReceived;
            if (enemySpawnComponent != null)
            {
                enemySpawnComponent.HandleSpawned -= OnTargetHandleSpawned;
                enemySpawnComponent.HandleDisposed -= OnTargetHandleDisposed;
                enemySpawnComponent = null;
            }
            targetRegistry.Dispose();
            if (characterPhysics != null) characterPhysics.FixedStepCompleted -= OnFixedStepCompleted;
            World.GameplayReady -= OnGameplayReady;
            if (ownerPawnRegistration != null && !ownerPawnRegistration.IsDisposed)
            {
                World.UnregisterActor(ownerPawnRegistration);
            }
            network.ClientWorld.ReleasePawn(network.ClientWorld.ControlledPawnId);
            foreach (ActorRegistration registration in remotePawnRegistrationsById.Values)
            {
                if (registration != null && !registration.IsDisposed) World.UnregisterActor(registration);
            }
            remotePawnRegistrationsById.Clear();
            remoteSnapshotBuffersById.Clear();
            remoteSnapshotPresentationsById.Clear();
            remoteAnimationActionBridgesById.Clear();
            remoteFirePresentersById.Clear();
            animationActionRouter.Clear();
            movementPrediction.Clear();
            enemyReplicationTracker.Clear();
            enemyPresentationRegistry?.Dispose();
        }

        private void OnPawnSpawned(ClientPawnState pawn)
        {
            if (pawn.OwnerPlayerId == network.ClientWorld.LocalPlayerId || remotePawnRegistrationsById.ContainsKey(pawn.PawnId)) return;
            pendingRemotePawns.Add(pawn);
        }

        private void OnGameplayReady()
        {
            foreach (ClientPawnState pawn in pendingRemotePawns)
            {
                if (remotePawnRegistrationsById.ContainsKey(pawn.PawnId)) continue;
                _ = SpawnRemotePawnAsync(pawn);
            }

            pendingRemotePawns.Clear();
        }

        private void OnOwnerPossessionApplied(ClientPawnState pawn)
        {
            if (ownerPawnSpawnRequested) return;
            ownerPawnSpawnRequested = true;
            _ = SpawnOwnerPawnAsync(pawn);
        }

        private async Task SpawnOwnerPawnAsync(ClientPawnState pawn)
        {
            try
            {
                SpawnPointStatus spawnPoint = World.LevelRuntime.GetStatus(pawn.SpawnPointId);
                ownerNetworkBinding = new NetworkPawnBinding(
                    pawn.PawnId,
                    pawn.OwnerPlayerId,
                    pawn.PossessionRevision,
                    NetworkPawnRole.LocalAutonomous);
                ownerPawn = await pawnFactory.CreateAsync(
                    playerStateDefinition.PawnData,
                    playerStateDefinition.InputProfile,
                    spawnPoint.Transform.position,
                    spawnPoint.Transform.rotation,
                    ownerNetworkBinding);
                ownerPawnRegistration = World.RegisterActor(ownerPawn, critical: true);
                ((PlayerController)PlayerController).Possess(ownerPawn);
                movementPrediction.Configure(ownerPawn, ownerNetworkBinding);
                ownerAnimationActionBridge = new NetworkAnimationActionBridge(
                    pawn.PawnId,
                    pawn.PossessionRevision,
                    networkTickClock,
                    new ExistingLocalPredictionPresenter());
                animationActionRouter.SetOwner(ownerNetworkBinding, ownerAnimationActionBridge);
                ownerPawn.BindDiscreteActionReplicationGateway(
                    new OwnerNetworkAnimationActionGateway(
                        pawn.PawnId,
                        pawn.PossessionRevision,
                        network,
                        ownerAnimationActionBridge));
                ownerFireBridge = new NetworkFireBridge(pawn.PawnId, pawn.PossessionRevision);
                ownerGameplayHud = ownerPawn.Root.GetComponent<OwnerGameplayHud>() ?? ownerPawn.Root.AddComponent<OwnerGameplayHud>();
                ownerGameplayHud.Apply(latestOwnerGameplayState, true);
                ownerPawn.BindFireAuthorityGateway(new OwnerNetworkFireGateway(
                    network,
                    ownerFireBridge,
                    () => networkTickClock.EstimatedServerTick));
                NetworkStatus = "Owner pawn possessed";
                initialOwnerPawnReady.TrySetResult(true);
                TryCompleteNetworkStart();
            }
            catch (Exception exception)
            {
                NetworkStatus = exception.Message;
                initialOwnerPawnReady.TrySetException(exception);
            }
        }

        private async Task SpawnRemotePawnAsync(ClientPawnState pawn)
        {
            try
            {
                SpawnPointStatus spawnPoint = World.LevelRuntime.GetStatus(pawn.SpawnPointId);
                Pawn remotePawn = await pawnFactory.CreateRemoteAsync(
                    playerStateDefinition.PawnData,
                    spawnPoint.Transform.position,
                    spawnPoint.Transform.rotation,
                    new NetworkPawnBinding(
                        pawn.PawnId,
                        pawn.OwnerPlayerId,
                        pawn.PossessionRevision,
                        NetworkPawnRole.RemoteSimulated),
                    transform => new RemoteAnimationSnapshotSource(transform));
                ActorRegistration remoteRegistration = World.RegisterActor(remotePawn, critical: false);
                remotePawnRegistrationsById.Add(pawn.PawnId, remoteRegistration);
                if (World.State == WorldState.Playing) World.ActivateActor(remoteRegistration);
                remoteSnapshotBuffersById.Add(pawn.PawnId, new RemoteSnapshotBuffer());
                remoteSnapshotPresentationsById.Add(pawn.PawnId, new RemoteSnapshotPresentation(remotePawn));
                var remoteFirePresenter = new RemotePawnNetworkAnimationActionPresenter(
                    remotePawn,
                    remotePawn.GetComponent<PawnAnimationComponent>(),
                    playerStateDefinition.InitialInventorySet);
                var remoteBridge = new NetworkAnimationActionBridge(
                        pawn.PawnId,
                        pawn.PossessionRevision,
                        networkTickClock,
                        remoteFirePresenter);
                remoteAnimationActionBridgesById.Add(pawn.PawnId, remoteBridge);
                remoteFirePresentersById.Add(pawn.PawnId, remoteFirePresenter);
                animationActionRouter.AddRemote(pawn.PawnId, remoteBridge);
                NetworkStatus = $"Remote pawn {pawn.PawnId} spawned";
            }
            catch (Exception exception)
            {
                NetworkStatus = exception.Message;
            }
        }

        private void OnMatchStarting(long matchId)
        {
            startedMatchId = matchId;
            // Dedicated revisions start at zero for every match.  Never let a
            // previous match's higher revision suppress this match's first HUD
            // state when the same PawnId is reused.
            latestOwnerGameplayState = null;
            ownerGameplayHud?.Reset();
            movementPrediction.SetMatchId(matchId);
            TryCompleteNetworkStart();
        }

        private void OnTargetStateChangedReceived(TargetStateMessage state)
        {
            targetRegistry.Apply(state);
            Debug.Log($"[Network][049] TargetStateReceived MatchId={network.ClientWorld.MatchId} TargetId={state?.TargetId} Revision={state?.Revision} Health={state?.Health} IsDead={state?.IsDead}");
        }

        private void OnTargetStateSnapshotReceived(TargetStateSnapshotMessage snapshot) => targetRegistry.ApplySnapshot(snapshot);

        private void OnEnemySpawnedReceived(EnemySpawnedEvent spawned)
        {
            if (!enemyReplicationTracker.ApplySpawn(spawned)) return;
            enemyPresentationRegistry?.Spawn(spawned);
            Debug.Log($"[Network][051] EnemySpawned MatchId={network.ClientWorld.MatchId} EnemyId={spawned.EnemyId} ArchetypeId={spawned.ArchetypeId} ServerTick={spawned.AuthorityServerTick}");
        }

        private void OnEnemySnapshotReceived(EnemySnapshotEvent snapshot)
        {
            if (enemyReplicationTracker.ApplySnapshot(snapshot, Time.realtimeSinceStartup))
                enemyPresentationRegistry?.ApplySnapshot(snapshot);
        }

        private void OnEnemyActionReceived(EnemyActionEvent action)
        {
            if (enemyReplicationTracker.ApplyAction(action, Time.realtimeSinceStartup))
                enemyPresentationRegistry?.ApplyAction(action);
        }

        private void OnOwnerGameplayStateReceived(OwnerGameplayStateEvent state)
        {
            bool isOwner = state != null && ownerNetworkBinding != null && state.PawnId == ownerNetworkBinding.PawnId;
            if (isOwner) latestOwnerGameplayState = state;
            ownerGameplayHud?.Apply(state, isOwner);
            if (isOwner)
                Debug.Log($"[Network][052] OwnerGameplayState PawnId={state.PawnId} VitalsRevision={state.VitalsRevision} Health={state.Health}/{state.MaxHealth} EquipmentRevision={state.EquipmentRevision} Ammo={state.MagazineAmmo}/{state.MagazineCapacity}");
        }

        private void OnTargetHandleSpawned(EnemySpawnHandle handle) => targetRegistry.Register(handle);

        private void OnTargetHandleDisposed(EnemySpawnHandle handle)
        {
            targetRegistry.Unregister(handle);
            Debug.Log($"[Network][049] TargetHandleDisposed MatchId={network.ClientWorld.MatchId} TargetId={handle?.PointId}");
        }

        private void TryCompleteNetworkStart()
        {
            if (startedMatchId > 0 && initialOwnerPawnReady.Task.IsCompletedSuccessfully)
            {
                networkStartReady.TrySetResult(true);
            }
        }

        private void OnFixedStepCompleted(long clientTick)
        {
            FixedStepObservedCount++;
            networkTickClock.AdvanceOneTick();
            foreach (RemotePawnNetworkAnimationActionPresenter presenter in remoteFirePresentersById.Values)
                presenter.AdvanceRecoil(Time.fixedDeltaTime);
            ApplyRemoteSnapshots();
            enemyPresentationRegistry?.Tick(Time.fixedDeltaTime, Time.realtimeSinceStartup);
            movementPrediction.OnFixedStepCompleted(clientTick);
            foreach (EnemyResyncRequest request in enemyReplicationTracker.CollectExpiredResyncRequests(Time.realtimeSinceStartup))
            {
                _ = SendEnemyResyncRequestAsync(request);
            }
        }

        private async Task SendEnemyResyncRequestAsync(EnemyResyncRequest request)
        {
            try
            {
                await network.SendEnemyResyncRequestAsync(request);
                Debug.Log($"[Network][051] EnemyResyncRequested MatchId={network.ClientWorld.MatchId} EnemyId={request.EnemyId} LastTick={request.LastKnownAuthorityServerTick}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network][051] EnemyResyncFailed EnemyId={request.EnemyId} Reason={exception.Message}");
            }
        }

        private void OnOwnerReconcileReceived(OwnerReconcile reconcile)
        {
            movementPrediction.OnReconcileReceived(reconcile);
        }

        private void OnAuthoritySnapshotReceived(AuthoritySnapshot snapshot)
        {
            AuthoritySnapshotReceivedCount++;
            networkTickClock.Observe(snapshot.State.ServerTick);
            string role = ownerNetworkBinding != null && snapshot.PawnId == ownerNetworkBinding.PawnId
                ? NetworkPawnRole.LocalAutonomous.ToString()
                : NetworkPawnRole.RemoteSimulated.ToString();
            if (snapshot.State.ServerTick % 60 == 0)
                Debug.Log($"[Network][039] AuthoritySnapshotReceived MatchId={snapshot.MatchId} PawnId={snapshot.PawnId} ServerTick={snapshot.State.ServerTick} Role={role} {FormatState(snapshot.State)}");
            if (ownerNetworkBinding != null && snapshot.PawnId == ownerNetworkBinding.PawnId)
            {
                LastOwnerAuditSnapshot = snapshot;
                bool isMoving = snapshot.State.BaseVelocity.XMillimeters != 0 ||
                    snapshot.State.BaseVelocity.ZMillimeters != 0;
                ownerLoopPhaseSynchronizer.Synchronize(
                    ownerPawn?.GetComponent<PawnAnimationComponent>()?.Animator,
                    snapshot.State.ServerTick,
                    isMoving);
                return;
            }

            if (remoteSnapshotBuffersById.TryGetValue(snapshot.PawnId, out RemoteSnapshotBuffer buffer))
                buffer.Add(snapshot);
        }

        private void OnAnimationActionStartedReceived(NetworkAnimationActionStarted action)
        {
            animationActionRouter.ApplyStarted(action);
        }

        private void OnAnimationActionTerminalReceived(NetworkAnimationActionTerminal terminal)
        {
            animationActionRouter.ApplyTerminal(terminal);
        }

        private void OnFireCommittedReceived(FireCommitted committed)
        {
            if (committed == null)
            {
                Debug.LogWarning("[Network][045] FireCommittedIgnored Reason=NullPayload");
                return;
            }

            Debug.Log($"[Network][045] FireCommittedReceived PawnId={committed.PawnId} ShotSequence={committed.ShotSequence} Equipment={committed.EquipmentInstanceId} HasImpact={committed.HasImpact} ImpactId={committed.ImpactId}");
            if (committed != null && ownerNetworkBinding != null &&
                committed.PawnId == ownerNetworkBinding.PawnId)
            {
                ownerFireBridge?.ApplyCommitted(committed);
                ApplyOwnerAuthoritativeMagazine(committed.AuthoritativeMagazineAmmo);
                ExecuteAuthorityImpactCue(ownerPawn, committed, "Owner");
                return;
            }
            if (committed != null && remoteFirePresentersById.TryGetValue(committed.PawnId, out RemotePawnNetworkAnimationActionPresenter presenter))
            {
                bool applied = presenter.ApplyCommittedFire(committed.EquipmentInstanceId, committed.RecoilProfileId);
                Debug.Log($"[Network][045] RemoteFireCommitted PawnId={committed.PawnId} ShotSequence={committed.ShotSequence} Equipment={committed.EquipmentInstanceId} RecoilApplied={applied}");
                ExecuteAuthorityImpactCue(presenter.Pawn, committed, "Remote");
                return;
            }

            Debug.LogWarning($"[Network][045] FireCommittedIgnored PawnId={committed.PawnId} Reason=NoPresentation");
        }

        private static void ExecuteAuthorityImpactCue(Pawn sourcePawn, FireCommitted committed, string receiver)
        {
            if (!committed.HasImpact || committed.ImpactId <= 0)
            {
                Debug.Log($"[CueDebug][045] ImpactSkipped Receiver={receiver} ShotSequence={committed.ShotSequence} Reason=NoAuthorityImpact");
                return;
            }

            if (GameplayCueRouter.Current == null ||
                !GameplayTagManager.Instance.TryRequestTag("GameplayCue.Weapon.Impact", out GameplayTag impactTag))
            {
                Debug.LogWarning($"[CueDebug][045] ImpactSkipped Receiver={receiver} ImpactId={committed.ImpactId} Reason=RouteUnavailable");
                return;
            }

            Vector3 position = new Vector3(committed.ImpactPositionX, committed.ImpactPositionY, committed.ImpactPositionZ);
            Vector3 normal = new Vector3(committed.ImpactNormalX, committed.ImpactNormalY, committed.ImpactNormalZ);
            var context = new GameplayEffectContext(sourcePawn, sourcePawn?.Root, null);
            GameplayCueRouter.Current.Execute(
                impactTag,
                new GameplayCueParameters(context, sourcePawn, position, true, normal, normal.sqrMagnitude > 0.0001f));
            Debug.Log($"[CueDebug][045] ImpactExecuted Receiver={receiver} ImpactId={committed.ImpactId} Position={position} Normal={normal} Surface={committed.SurfaceId}");
        }

        private void OnFireRejectedReceived(FireRejected rejected)
        {
            if (ownerFireBridge?.ApplyRejected(rejected) == true)
                ApplyOwnerAuthoritativeMagazine(rejected.AuthoritativeMagazineAmmo);
        }

        private void ApplyOwnerAuthoritativeMagazine(int authoritativeMagazineAmmo)
        {
            if (authoritativeMagazineAmmo < 0 || ownerPawn == null ||
                !ownerPawn.TryGetComponent(out EquipmentManagerComponent equipment) ||
                equipment.CurrentWeapon == null)
                return;
            equipment.CurrentWeapon.Item.SetAmmo(
                authoritativeMagazineAmmo,
                equipment.CurrentWeapon.Item.ReserveAmmo);
            Debug.Log($"[Network][044] OwnerAmmoReconciled Magazine={authoritativeMagazineAmmo}");
        }

        private void ApplyRemoteSnapshots()
        {
            foreach (KeyValuePair<long, RemoteSnapshotBuffer> entry in remoteSnapshotBuffersById)
            {
                if (!remoteSnapshotPresentationsById.TryGetValue(entry.Key, out RemoteSnapshotPresentation presentation)) continue;
                long interpolationTick = Math.Max(1, networkTickClock.EstimatedServerTick - 2);
                if (!entry.Value.TrySample(interpolationTick, out AuthorityState state)) continue;
                presentation.Apply(state);
                SnapshotAppliedCount++;
                if (state.ServerTick % 60 == 0)
                    Debug.Log($"[Network][039] SnapshotApplied MatchId={startedMatchId} PawnId={entry.Key} ServerTick={state.ServerTick} Role={NetworkPawnRole.RemoteSimulated} {FormatState(state)}");
            }
        }

        private static string FormatState(AuthorityState state) =>
            $"Position={state.Position.XMillimeters},{state.Position.YMillimeters},{state.Position.ZMillimeters} " +
            $"Rotation={state.Rotation.X},{state.Rotation.Y},{state.Rotation.Z},{state.Rotation.W} " +
            $"BaseVelocity={state.BaseVelocity.XMillimeters},{state.BaseVelocity.YMillimeters},{state.BaseVelocity.ZMillimeters} " +
            $"MovementState={state.MovementState} Grounded={state.Grounded} " +
            $"GroundNormal={state.GroundNormal.XMillimeters},{state.GroundNormal.YMillimeters},{state.GroundNormal.ZMillimeters} " +
            $"AttachedBaseId={state.AttachedBaseId}";
    }
}
