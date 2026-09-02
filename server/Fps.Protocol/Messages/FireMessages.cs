using MessagePack;

namespace Fps.Protocol;

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
public sealed record FireRequestMessage(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long PossessionRevision,
    [property: Key(2)] long PredictionNonce,
    [property: Key(3)] long ClientShotSequence,
    [property: Key(4)] long EquipmentInstanceId,
    [property: Key(5)] long ClientFireTick,
    [property: Key(6)] float OriginX,
    [property: Key(7)] float OriginY,
    [property: Key(8)] float OriginZ,
    [property: Key(9)] float DirectionX,
    [property: Key(10)] float DirectionY,
    [property: Key(11)] float DirectionZ);

[MessagePackObject]
public sealed record FireCommittedMessage(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long PossessionRevision,
    [property: Key(2)] long PredictionNonce,
    [property: Key(3)] long ClientShotSequence,
    [property: Key(4)] long ShotSequence,
    [property: Key(5)] long ServerTick,
    [property: Key(6)] int AuthoritativeMagazineAmmo,
    [property: Key(7)] string RecoilProfileId,
    [property: Key(8)] long EquipmentInstanceId,
    [property: Key(9)] long ImpactId,
    [property: Key(10)] bool HasImpact,
    [property: Key(11)] float ImpactPositionX,
    [property: Key(12)] float ImpactPositionY,
    [property: Key(13)] float ImpactPositionZ,
    [property: Key(14)] float ImpactNormalX,
    [property: Key(15)] float ImpactNormalY,
    [property: Key(16)] float ImpactNormalZ,
    [property: Key(17)] string SurfaceId);

[MessagePackObject]
public sealed record FireRejectedMessage(
    [property: Key(0)] long PawnId,
    [property: Key(1)] long PossessionRevision,
    [property: Key(2)] long PredictionNonce,
    [property: Key(3)] long ClientShotSequence,
    [property: Key(4)] FireRejectionReason Reason,
    [property: Key(5)] int AuthoritativeMagazineAmmo);
