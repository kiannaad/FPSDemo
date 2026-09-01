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
        private readonly PawnFactory pawnFactory = new PawnFactory();
        private readonly Dictionary<long, GameObject> remoteViewsByPawnId = new Dictionary<long, GameObject>();
        private readonly TaskCompletionSource<bool> initialOwnerPawnReady = new TaskCompletionSource<bool>();
        private ActorRegistration ownerPawnRegistration;
        private bool ownerPawnSpawnRequested;

        public NetworkGameMode(World world, Player player, PlayerStateDefinition playerStateDefinition, ClientNetworkSubSystem network)
            : base(world, player)
        {
            this.playerStateDefinition = playerStateDefinition ?? throw new ArgumentNullException(nameof(playerStateDefinition));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            network.ClientWorld.PawnSpawned += OnPawnSpawned;
            network.ClientWorld.OwnerPossessionApplied += OnOwnerPossessionApplied;
        }

        public string RoomId { get; private set; } = string.Empty;
        public string NetworkStatus { get; private set; } = "Connecting";

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
            if (ownerPawnRegistration != null && !ownerPawnRegistration.IsDisposed) World.UnregisterActor(ownerPawnRegistration);
            foreach (GameObject view in remoteViewsByPawnId.Values)
            {
                if (view != null) UnityEngine.Object.Destroy(view);
            }

            remoteViewsByPawnId.Clear();
        }

        private void OnPawnSpawned(ClientPawnState pawn)
        {
            if (pawn.OwnerPlayerId == network.ClientWorld.LocalPlayerId || remoteViewsByPawnId.ContainsKey(pawn.PawnId)) return;
            SpawnPointStatus spawnPoint = World.LevelRuntime.GetStatus(pawn.SpawnPointId);
            GameObject view = UnityEngine.Object.Instantiate(
                playerStateDefinition.PawnData.PawnPrefab,
                spawnPoint.Transform.position,
                spawnPoint.Transform.rotation);
            foreach (Camera camera in view.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (MonoBehaviour behaviour in view.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            view.name = $"RemotePawnView:{pawn.PawnId}";
            view.SetActive(true);
            remoteViewsByPawnId.Add(pawn.PawnId, view);
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
                Pawn ownerPawn = await pawnFactory.CreateAsync(
                    playerStateDefinition.PawnData,
                    playerStateDefinition.InputProfile,
                    spawnPoint.Transform.position,
                    spawnPoint.Transform.rotation);
                ownerPawnRegistration = World.RegisterActor(ownerPawn, critical: true);
                ((PlayerController)PlayerController).Possess(ownerPawn);
                NetworkStatus = "Owner pawn possessed";
                initialOwnerPawnReady.TrySetResult(true);
            }
            catch (Exception exception)
            {
                NetworkStatus = exception.Message;
                initialOwnerPawnReady.TrySetException(exception);
            }
        }
    }
}
