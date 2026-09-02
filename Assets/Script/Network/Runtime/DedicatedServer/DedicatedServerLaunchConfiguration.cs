using System.Collections.Generic;

namespace CGame.Network
{
    public sealed class DedicatedServerLaunchConfiguration
    {
        public DedicatedServerLaunchConfiguration(
            long matchId,
            int dataPort,
            int healthPort,
            string credential,
            string levelId,
            string contentVersion,
            IReadOnlyList<DedicatedAuthorityPawnConfiguration> authorityPawns)
        {
            MatchId = matchId;
            DataPort = dataPort;
            HealthPort = healthPort;
            Credential = credential;
            LevelId = levelId;
            ContentVersion = contentVersion;
            AuthorityPawns = authorityPawns;
        }

        public long MatchId { get; }
        public int DataPort { get; }
        public int HealthPort { get; }
        public string Credential { get; }
        public string LevelId { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<DedicatedAuthorityPawnConfiguration> AuthorityPawns { get; }
    }

    public sealed class DedicatedAuthorityPawnConfiguration
    {
        public DedicatedAuthorityPawnConfiguration(
            long pawnId,
            long ownerPlayerId,
            long possessionRevision,
            string spawnPointId,
            string connectionId = "integration-client")
        {
            PawnId = pawnId;
            OwnerPlayerId = ownerPlayerId;
            PossessionRevision = possessionRevision;
            SpawnPointId = spawnPointId;
            ConnectionId = connectionId;
        }

        public long PawnId { get; }
        public long OwnerPlayerId { get; }
        public long PossessionRevision { get; }
        public string SpawnPointId { get; }
        public string ConnectionId { get; }
    }
}
