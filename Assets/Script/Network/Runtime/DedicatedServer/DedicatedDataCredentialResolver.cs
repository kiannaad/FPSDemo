using System;
using System.Collections.Generic;

namespace CGame.Network
{
    public readonly struct DedicatedDataIdentity
    {
        public DedicatedDataIdentity(long pawnId, long possessionRevision, string connectionId)
        {
            PawnId = pawnId;
            PossessionRevision = possessionRevision;
            ConnectionId = connectionId;
        }

        public long PawnId { get; }
        public long PossessionRevision { get; }
        public string ConnectionId { get; }
    }

    public sealed class DedicatedDataCredentialResolver
    {
        private readonly Dictionary<string, DedicatedDataIdentity> identities =
            new Dictionary<string, DedicatedDataIdentity>(StringComparer.Ordinal);

        public DedicatedDataCredentialResolver(
            string baseCredential,
            IReadOnlyList<DedicatedAuthorityPawnConfiguration> authorityPawns)
        {
            if (string.IsNullOrWhiteSpace(baseCredential)) throw new ArgumentException("Credential is required.", nameof(baseCredential));
            if (authorityPawns == null) throw new ArgumentNullException(nameof(authorityPawns));
            foreach (DedicatedAuthorityPawnConfiguration pawn in authorityPawns)
            {
                identities.Add(
                    $"{baseCredential}:{pawn.PawnId}",
                    new DedicatedDataIdentity(pawn.PawnId, pawn.PossessionRevision, pawn.ConnectionId));
            }
        }

        public bool TryResolve(string credential, out DedicatedDataIdentity identity)
        {
            if (credential != null && identities.TryGetValue(credential, out identity)) return true;
            identity = default;
            return false;
        }
    }
}
