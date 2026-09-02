using System;

namespace CGame.Network
{
    [Serializable]
    public sealed class DedicatedServerHealthSnapshot
    {
        public string status;
        public long matchId;
        public int dataPort;
        public int healthPort;
        public string levelId;
        public string contentVersion;
        public int authorityPawnCount;
        public long fixedStepCount;
        public string failure;
    }
}
