using System;

namespace CGame.Network
{
    public enum AuthorityMoveRejection
    {
        None,
        Connection,
        Match,
        Pawn,
        PossessionRevision,
        DuplicateSequence,
        SequenceGap,
        ExpiredTick,
        FutureTick
    }

    public readonly struct AuthorityMoveValidation
    {
        public AuthorityMoveValidation(AuthorityMoveRejection rejection)
        {
            Rejection = rejection;
        }

        public AuthorityMoveRejection Rejection { get; }
        public bool Accepted => Rejection == AuthorityMoveRejection.None;
    }

    public sealed class AuthorityMoveValidator
    {
        private readonly string connectionId;
        private readonly long matchId;
        private readonly long pawnId;
        private readonly long possessionRevision;
        private readonly int maxPastTicks;
        private readonly int maxFutureTicks;
        private long? clientToServerTickOffset;

        public AuthorityMoveValidator(
            string connectionId,
            long matchId,
            long pawnId,
            long possessionRevision,
            int maxPastTicks = 120,
            int maxFutureTicks = 8)
        {
            if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentException("Connection is required.", nameof(connectionId));
            this.connectionId = connectionId;
            this.matchId = matchId;
            this.pawnId = pawnId;
            this.possessionRevision = possessionRevision;
            this.maxPastTicks = maxPastTicks;
            this.maxFutureTicks = maxFutureTicks;
        }

        public long LastAcceptedSequence { get; private set; }
        public long LastAcceptedClientTick { get; private set; }

        public AuthorityMoveValidation Validate(string senderConnectionId, PawnMove move, long serverTick)
        {
            AuthorityMoveRejection rejection = GetRejection(senderConnectionId, move, serverTick);
            if (rejection == AuthorityMoveRejection.None)
            {
                if (!clientToServerTickOffset.HasValue)
                    clientToServerTickOffset = serverTick - move.ClientTick;
                LastAcceptedSequence = move.Sequence;
                LastAcceptedClientTick = move.ClientTick;
            }

            return new AuthorityMoveValidation(rejection);
        }

        private AuthorityMoveRejection GetRejection(string senderConnectionId, PawnMove move, long serverTick)
        {
            if (!string.Equals(senderConnectionId, connectionId, StringComparison.Ordinal)) return AuthorityMoveRejection.Connection;
            if (move.MatchId != matchId) return AuthorityMoveRejection.Match;
            if (move.PawnId != pawnId) return AuthorityMoveRejection.Pawn;
            if (move.PossessionRevision != possessionRevision) return AuthorityMoveRejection.PossessionRevision;
            if (move.Sequence <= LastAcceptedSequence) return AuthorityMoveRejection.DuplicateSequence;
            if (!clientToServerTickOffset.HasValue) return AuthorityMoveRejection.None;
            long sequenceDelta = move.Sequence - LastAcceptedSequence;
            long clientTickDelta = move.ClientTick - LastAcceptedClientTick;
            if (clientTickDelta <= 0) return AuthorityMoveRejection.ExpiredTick;
            if (clientTickDelta > sequenceDelta + maxFutureTicks) return AuthorityMoveRejection.FutureTick;
            long observedOffset = serverTick - move.ClientTick;
            if (observedOffset < clientToServerTickOffset.Value)
                clientToServerTickOffset = observedOffset;
            long projectedServerTick = move.ClientTick + clientToServerTickOffset.Value;
            if (projectedServerTick < serverTick - maxPastTicks) return AuthorityMoveRejection.ExpiredTick;
            if (projectedServerTick > serverTick + maxFutureTicks) return AuthorityMoveRejection.FutureTick;
            return AuthorityMoveRejection.None;
        }
    }
}
