using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public sealed class ClientWorld
    {
        private readonly Dictionary<long, ClientPawnState> pawnsById = new Dictionary<long, ClientPawnState>();
        private readonly Dictionary<long, PendingPossession> pendingPossessionsByPlayerId = new Dictionary<long, PendingPossession>();
        private readonly Dictionary<long, long> appliedPossessionRevisionsByPlayerId = new Dictionary<long, long>();

        public long LocalPlayerId { get; private set; }
        public long ControlledPawnId { get; private set; }
        public long MatchId { get; private set; }
        public string DataEndpoint { get; private set; }
        public string CredentialId { get; private set; }
        public IReadOnlyCollection<ClientPawnState> Pawns => pawnsById.Values;
        public event Action<ClientPawnState> PawnSpawned;
        public event Action<ClientPawnState> OwnerPossessionApplied;
        public event Action<long> MatchStarting;

        public void SetLocalPlayer(long playerId)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            LocalPlayerId = playerId;
            ApplyPendingPossession(playerId);
        }

        public void OnPawnSpawned(PawnSpawnedEvent pawnSpawned)
        {
            if (pawnSpawned == null || pawnSpawned.PawnId <= 0 || pawnSpawned.OwnerPlayerId <= 0)
                throw new ArgumentException("PawnSpawned data is invalid.", nameof(pawnSpawned));
            if (pawnsById.ContainsKey(pawnSpawned.PawnId)) return;
            var pawn = new ClientPawnState(
                pawnSpawned.PawnId,
                pawnSpawned.OwnerPlayerId,
                pawnSpawned.SpawnPointId);
            pawnsById.Add(pawnSpawned.PawnId, pawn);
            PawnSpawned?.Invoke(pawn);
            ApplyPendingPossession(pawnSpawned.OwnerPlayerId);
        }

        public bool ReleasePawn(long pawnId)
        {
            if (!pawnsById.Remove(pawnId)) return false;
            if (ControlledPawnId == pawnId) ControlledPawnId = 0;
            return true;
        }

        public void OnPossessionChanged(PossessionChangedEvent possessionChanged)
        {
            if (possessionChanged == null || possessionChanged.PlayerId <= 0 || possessionChanged.PawnId <= 0)
                throw new ArgumentException("PossessionChanged data is invalid.", nameof(possessionChanged));
            if (appliedPossessionRevisionsByPlayerId.TryGetValue(possessionChanged.PlayerId, out long appliedRevision)
                && appliedRevision >= possessionChanged.PossessionRevision) return;
            if (pendingPossessionsByPlayerId.TryGetValue(possessionChanged.PlayerId, out PendingPossession current)
                && current.Revision >= possessionChanged.PossessionRevision) return;
            pendingPossessionsByPlayerId[possessionChanged.PlayerId] = new PendingPossession(
                possessionChanged.PawnId,
                possessionChanged.PossessionRevision);
            ApplyPendingPossession(possessionChanged.PlayerId);
        }

        public void OnMatchStarting(MatchStartingEvent matchStarting)
        {
            if (matchStarting == null || matchStarting.MatchId <= 0)
                throw new ArgumentException("MatchStarting data is invalid.", nameof(matchStarting));
            MatchId = matchStarting.MatchId;
            DataEndpoint = matchStarting.DataEndpoint;
            CredentialId = matchStarting.CredentialId;
            MatchStarting?.Invoke(matchStarting.MatchId);
        }

        private void ApplyPendingPossession(long playerId)
        {
            if (!pendingPossessionsByPlayerId.TryGetValue(playerId, out PendingPossession pending)
                || !pawnsById.TryGetValue(pending.PawnId, out ClientPawnState pawn)) return;
            pendingPossessionsByPlayerId.Remove(playerId);
            appliedPossessionRevisionsByPlayerId[playerId] = pending.Revision;
            pawn.PossessionRevision = pending.Revision;
            pawn.IsLocallyControlled = playerId == LocalPlayerId;
            pawn.Role = pawn.IsLocallyControlled
                ? NetworkPawnRole.LocalAutonomous
                : NetworkPawnRole.RemoteSimulated;
            if (pawn.IsLocallyControlled)
            {
                if (ControlledPawnId != 0 && pawnsById.TryGetValue(ControlledPawnId, out ClientPawnState previousPawn))
                {
                    previousPawn.IsLocallyControlled = false;
                    previousPawn.Role = NetworkPawnRole.RemoteSimulated;
                }
                ControlledPawnId = pawn.PawnId;
                OwnerPossessionApplied?.Invoke(pawn);
            }
        }

        private readonly struct PendingPossession
        {
            public PendingPossession(long pawnId, long revision)
            {
                PawnId = pawnId;
                Revision = revision;
            }

            public long PawnId { get; }
            public long Revision { get; }
        }
    }

    public sealed class ClientPawnState
    {
        public ClientPawnState(long pawnId, long ownerPlayerId, string spawnPointId)
        {
            PawnId = pawnId;
            OwnerPlayerId = ownerPlayerId;
            SpawnPointId = spawnPointId;
            Role = NetworkPawnRole.RemoteSimulated;
        }

        public long PawnId { get; }
        public long OwnerPlayerId { get; }
        public string SpawnPointId { get; }
        public long PossessionRevision { get; internal set; }
        public bool IsLocallyControlled { get; internal set; }
        public NetworkPawnRole Role { get; internal set; }
    }
}
