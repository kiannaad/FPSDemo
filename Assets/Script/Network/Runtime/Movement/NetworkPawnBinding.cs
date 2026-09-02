using System;

namespace CGame.Network
{
    public sealed class NetworkPawnBinding : ActorComponent
    {
        public NetworkPawnBinding(
            long pawnId,
            long ownerPlayerId,
            long possessionRevision,
            NetworkPawnRole role)
        {
            if (pawnId <= 0) throw new ArgumentOutOfRangeException(nameof(pawnId));
            if (ownerPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerPlayerId));

            PawnId = pawnId;
            OwnerPlayerId = ownerPlayerId;
            PossessionRevision = possessionRevision;
            Role = role;
            IsBound = true;
        }

        public long PawnId { get; private set; }
        public long OwnerPlayerId { get; private set; }
        public long PossessionRevision { get; private set; }
        public NetworkPawnRole Role { get; private set; }
        public bool IsBound { get; private set; }

        protected override void OnShutdown()
        {
            PawnId = 0;
            OwnerPlayerId = 0;
            PossessionRevision = 0;
            Role = NetworkPawnRole.RemoteSimulated;
            IsBound = false;
        }
    }
}
