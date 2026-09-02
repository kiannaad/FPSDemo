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
        public AuthorityMoveValidator(
            string connectionId,
            long matchId,
            long pawnId,
            long possessionRevision)
        {
            if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentException("Connection is required.", nameof(connectionId));
            this.connectionId = connectionId;
            this.matchId = matchId;
            this.pawnId = pawnId;
            this.possessionRevision = possessionRevision;
        }

        public long LastAcceptedSequence { get; private set; }
        public long LastAcceptedClientTick { get; private set; }

        public AuthorityMoveValidation Validate(string senderConnectionId, PawnMove move, long serverTick)
        {
            AuthorityMoveRejection rejection = GetRejection(senderConnectionId, move, serverTick);
            if (rejection == AuthorityMoveRejection.None)
            {
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
            return AuthorityMoveRejection.None;
        }
    }
}
