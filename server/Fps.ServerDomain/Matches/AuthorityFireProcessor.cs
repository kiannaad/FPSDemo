using Fps.Protocol;

namespace Fps.ServerDomain.Matches;

public sealed class AuthorityFireProcessor
{
    private readonly Dictionary<long, FireResolution> resolutionsByClientSequence = new();
    private long nextShotSequence;
    private long lastAcceptedTick = long.MinValue;

    public FireResolution Process(
        FireRequestMessage request,
        long expectedPawnId,
        long expectedPossessionRevision,
        long authorityTick,
        AuthorityActionEquipmentState equipment,
        AuthorityFireImpact? impact = null)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (equipment is null) throw new ArgumentNullException(nameof(equipment));
        if (resolutionsByClientSequence.TryGetValue(request.ClientShotSequence, out FireResolution cached)) return cached;

        FireResolution resolution;
        if (request.PawnId != expectedPawnId || request.PossessionRevision != expectedPossessionRevision ||
            request.PredictionNonce <= 0 || request.ClientShotSequence <= 0)
        {
            resolution = Reject(request, FireRejectionReason.PossessionMismatch, equipment.MagazineAmmo);
        }
        else if (lastAcceptedTick != long.MinValue && authorityTick - lastAcceptedTick < 6)
        {
            resolution = Reject(request, FireRejectionReason.RateLimited, equipment.MagazineAmmo);
        }
        else if (!equipment.TryConsumeFire(request.EquipmentInstanceId, out string reason))
        {
            resolution = Reject(request, reason == "OutOfAmmo" ? FireRejectionReason.OutOfAmmo : FireRejectionReason.EquipmentMismatch, equipment.MagazineAmmo);
        }
        else
        {
            lastAcceptedTick = authorityTick;
            AuthorityFireImpact resolvedImpact = impact.GetValueOrDefault();
            long shotSequence = checked(++nextShotSequence);
            resolution = new FireResolution(new FireCommittedMessage(
                expectedPawnId,
                expectedPossessionRevision,
                request.PredictionNonce,
                request.ClientShotSequence,
                shotSequence,
                authorityTick,
                equipment.MagazineAmmo,
                "Default",
                request.EquipmentInstanceId,
                resolvedImpact.Hit ? shotSequence : 0,
                resolvedImpact.Hit,
                resolvedImpact.PositionX,
                resolvedImpact.PositionY,
                resolvedImpact.PositionZ,
                resolvedImpact.NormalX,
                resolvedImpact.NormalY,
                resolvedImpact.NormalZ,
                resolvedImpact.SurfaceId ?? string.Empty), null);
        }
        resolutionsByClientSequence.Add(request.ClientShotSequence, resolution);
        return resolution;
    }

    public FireResolution RejectAuthorityUnavailable(FireRequestMessage request, int authoritativeMagazineAmmo)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (resolutionsByClientSequence.TryGetValue(request.ClientShotSequence, out FireResolution cached)) return cached;

        FireResolution rejected = Reject(request, FireRejectionReason.AuthorityUnavailable, authoritativeMagazineAmmo);
        resolutionsByClientSequence.Add(request.ClientShotSequence, rejected);
        return rejected;
    }

    private static FireResolution Reject(FireRequestMessage request, FireRejectionReason reason, int ammo) => new(
        null,
        new FireRejectedMessage(request.PawnId, request.PossessionRevision, request.PredictionNonce, request.ClientShotSequence, reason, ammo));
}

public readonly record struct FireResolution(FireCommittedMessage? Committed, FireRejectedMessage? Rejected);

public readonly record struct AuthorityFireImpact(
    bool Hit,
    float PositionX,
    float PositionY,
    float PositionZ,
    float NormalX,
    float NormalY,
    float NormalZ,
    string? SurfaceId);
