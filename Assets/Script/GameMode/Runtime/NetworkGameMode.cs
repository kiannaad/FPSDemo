using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly NetworkTickClock networkTickClock = new NetworkTickClock();
        private readonly List<ClientPawnState> pendingRemotePawns = new List<ClientPawnState>();
        private readonly TaskCompletionSource<bool> initialOwnerPawnReady = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> networkStartReady = new TaskCompletionSource<bool>();
        private ActorRegistration ownerPawnRegistration;
        private readonly LocalMovePredictionBuffer movePrediction = new LocalMovePredictionBuffer(NetworkPawnRole.LocalAutonomous);
        private CharacterPhysicsSubSystem characterPhysics;
        private Pawn ownerPawn;
        private NetworkPawnBinding ownerNetworkBinding;
        private NetworkAnimationActionBridge ownerAnimationActionBridge;
        private LocalPawnMoveReplayTarget replayTarget;
        private long nextMoveSequence;
        private bool ownerPawnSpawnRequested;
        private long startedMatchId;

        public NetworkGameMode(
            World world,
            Player player,
            PlayerStateDefinition playerStateDefinition,
            ClientNetworkSubSystem network,
            PawnFactory pawnFactory = null)
            : base(world, player)
        {
            this.playerStateDefinition = playerStateDefinition ?? throw new ArgumentNullException(nameof(playerStateDefinition));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.pawnFactory = pawnFactory ?? new PawnFactory();
            network.ClientWorld.PawnSpawned += OnPawnSpawned;
            network.ClientWorld.OwnerPossessionApplied += OnOwnerPossessionApplied;
            network.ClientWorld.MatchStarting += OnMatchStarting;
            network.OwnerReconcileReceived += OnOwnerReconcileReceived;
            network.AuthoritySnapshotReceived += OnAuthoritySnapshotReceived;
            network.AnimationActionStartedReceived += OnAnimationActionStartedReceived;
            network.AnimationActionTerminalReceived += OnAnimationActionTerminalReceived;
            world.GameplayReady += OnGameplayReady;
            characterPhysics = world.GetSubSystem<CharacterPhysicsSubSystem>();
            characterPhysics.FixedStepCompleted += OnFixedStepCompleted;
        }

        public string RoomId { get; private set; } = string.Empty;
        public string NetworkStatus { get; private set; } = "Connecting";
        public int SavedMoveCount => movePrediction.SavedMoves.Count;
        public OwnerReconcileKind? LastReconcileKind { get; private set; }
        public int ReplayCompletedCount { get; private set; }
        public int FixedStepObservedCount { get; private set; }
        public int MoveCreatedCount { get; private set; }
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
                replayTarget = new LocalPawnMoveReplayTarget(ownerPawn);
                ownerAnimationActionBridge = new NetworkAnimationActionBridge(
                    pawn.PawnId,
                    pawn.PossessionRevision,
                    networkTickClock,
                    new ExistingLocalPredictionPresenter());
                ownerPawn.BindDiscreteActionReplicationGateway(
                    new OwnerNetworkAnimationActionGateway(
                        pawn.PawnId,
                        pawn.PossessionRevision,
                        network,
                        ownerAnimationActionBridge));
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
                remoteAnimationActionBridgesById.Add(
                    pawn.PawnId,
                    new NetworkAnimationActionBridge(
                        pawn.PawnId,
                        pawn.PossessionRevision,
                        networkTickClock,
                        new RemotePawnNetworkAnimationActionPresenter(
                            remotePawn.GetComponent<PawnAnimationComponent>(),
                            playerStateDefinition.InitialInventorySet)));
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
            TryCompleteNetworkStart();
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
            ApplyRemoteSnapshots();
            if (startedMatchId <= 0 || ownerPawn == null || ownerNetworkBinding == null || !network.IsMovementConnected) return;
            Vector3 input = ownerPawn.PeekingMovementInput();
            Vector3 view = ownerPawn.ControlRotation.eulerAngles;
            CharacterMovementCommand command = ownerPawn.LastConsumedMovementCommand;
            PawnMoveFlags flags = PawnMoveFlags.None;
            if (command.JumpRequested) flags |= PawnMoveFlags.Jump;
            if (command.SprintRequested) flags |= PawnMoveFlags.Sprint;
            var move = new PawnMove(
                startedMatchId,
                ownerNetworkBinding.PawnId,
                ownerNetworkBinding.PossessionRevision,
                ++nextMoveSequence,
                clientTick,
                QuantizedInput.FromVector2(new Vector2(input.x, input.z)),
                QuantizedView.FromDegrees(view.y, Mathf.DeltaAngle(0f, view.x)),
                flags,
                QuantizedVector3.FromMeters(ownerPawn.Transform.position));
            movePrediction.Add(move);
            MoveCreatedCount++;
            network.SendPawnMove(move);
        }

        private void OnOwnerReconcileReceived(OwnerReconcile reconcile)
        {
            LastReconcileKind = reconcile.Kind;
            if (reconcile.Kind == OwnerReconcileKind.Ack)
            {
                movePrediction.Acknowledge(reconcile.AckSequence);
                return;
            }

            if (replayTarget == null) return;
            LocalCorrectionResult result = movePrediction.Correct(
                reconcile.AckSequence,
                reconcile.AuthorityState,
                replayTarget);
            ReplayCompletedCount++;
            Debug.Log($"[Network][038] ReplayCompleted Sequence={reconcile.AckSequence} HistoryFound={result.HistoryFound} Replayed={result.ReplayedMoveCount}");
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
                return;
            }

            if (remoteSnapshotBuffersById.TryGetValue(snapshot.PawnId, out RemoteSnapshotBuffer buffer))
                buffer.Add(snapshot);
        }

        private void OnAnimationActionStartedReceived(NetworkAnimationActionStarted action)
        {
            if (action != null && ownerNetworkBinding != null && action.PawnId == ownerNetworkBinding.PawnId)
            {
                ownerAnimationActionBridge?.ApplyStarted(action);
                return;
            }
            if (action != null && remoteAnimationActionBridgesById.TryGetValue(
                    action.PawnId,
                    out NetworkAnimationActionBridge bridge))
                bridge.ApplyStarted(action);
        }

        private void OnAnimationActionTerminalReceived(NetworkAnimationActionTerminal terminal)
        {
            if (terminal != null && ownerNetworkBinding != null && terminal.PawnId == ownerNetworkBinding.PawnId)
            {
                ownerAnimationActionBridge?.ApplyTerminal(terminal);
                return;
            }
            if (terminal != null && remoteAnimationActionBridgesById.TryGetValue(
                    terminal.PawnId,
                    out NetworkAnimationActionBridge bridge))
                bridge.ApplyTerminal(terminal);
        }

        private void ApplyRemoteSnapshots()
        {
            foreach (KeyValuePair<long, RemoteSnapshotBuffer> entry in remoteSnapshotBuffersById)
            {
                if (!remoteSnapshotPresentationsById.TryGetValue(entry.Key, out RemoteSnapshotPresentation presentation)) continue;
                long interpolationTick = Math.Max(1, entry.Value.LatestServerTick - 2);
                if (!entry.Value.TrySample(interpolationTick, out AuthorityState state)) continue;
                if (state.ServerTick <= presentation.LastAppliedState.ServerTick) continue;
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
