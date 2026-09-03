using System;

namespace CGame.Network
{
    [Serializable]
    public sealed class DedicatedFireQueryRequest
    {
        public long MatchId;
        public long PawnId;
        public float OriginX;
        public float OriginY;
        public float OriginZ;
        public float DirectionX;
        public float DirectionY;
        public float DirectionZ;
        public float Range = 1000f;
    }

    [Serializable]
    public sealed class DedicatedFireQueryResult
    {
        public bool Accepted;
        public bool Hit;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float NormalX;
        public float NormalY;
        public float NormalZ;
        public string SurfaceId;
        public string TargetId;
        public string Failure;
    }
}
