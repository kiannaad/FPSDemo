using MessagePack;

namespace CGame.Network
{
    public enum FireRejectionReason : byte
    {
        Unknown = 0,
        InvalidRequest = 1,
        PossessionMismatch = 2,
        EquipmentMismatch = 3,
        OutOfAmmo = 4,
        RateLimited = 5,
        AuthorityUnavailable = 6
    }

    [MessagePackObject]
    public sealed class FireRequest
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long PossessionRevision { get; set; }
        [Key(2)] public long PredictionNonce { get; set; }
        [Key(3)] public long ClientShotSequence { get; set; }
        [Key(4)] public long EquipmentInstanceId { get; set; }
        [Key(5)] public long ClientFireTick { get; set; }
        [Key(6)] public float OriginX { get; set; }
        [Key(7)] public float OriginY { get; set; }
        [Key(8)] public float OriginZ { get; set; }
        [Key(9)] public float DirectionX { get; set; }
        [Key(10)] public float DirectionY { get; set; }
        [Key(11)] public float DirectionZ { get; set; }
    }

    [MessagePackObject]
    public sealed class FireCommitted
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long PossessionRevision { get; set; }
        [Key(2)] public long PredictionNonce { get; set; }
        [Key(3)] public long ClientShotSequence { get; set; }
        [Key(4)] public long ShotSequence { get; set; }
        [Key(5)] public long ServerTick { get; set; }
        [Key(6)] public int AuthoritativeMagazineAmmo { get; set; }
        [Key(7)] public string RecoilProfileId { get; set; }
        [Key(8)] public long EquipmentInstanceId { get; set; }
        [Key(9)] public long ImpactId { get; set; }
        [Key(10)] public bool HasImpact { get; set; }
        [Key(11)] public float ImpactPositionX { get; set; }
        [Key(12)] public float ImpactPositionY { get; set; }
        [Key(13)] public float ImpactPositionZ { get; set; }
        [Key(14)] public float ImpactNormalX { get; set; }
        [Key(15)] public float ImpactNormalY { get; set; }
        [Key(16)] public float ImpactNormalZ { get; set; }
        [Key(17)] public string SurfaceId { get; set; }
        [Key(18)] public long HitEnemyId { get; set; }
    }

    [MessagePackObject]
    public sealed class FireRejected
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long PossessionRevision { get; set; }
        [Key(2)] public long PredictionNonce { get; set; }
        [Key(3)] public long ClientShotSequence { get; set; }
        [Key(4)] public FireRejectionReason Reason { get; set; }
        [Key(5)] public int AuthoritativeMagazineAmmo { get; set; }
    }
}
