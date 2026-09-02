using Fps.Protocol;

namespace Fps.ServerDomain.Matches;

public sealed class AuthorityActionEquipmentState
{
    private long equippedInstanceId;
    private bool isReloading;

    public long EquippedInstanceId => equippedInstanceId;
    public bool IsReloading => isReloading;
    public int MagazineAmmo { get; private set; } = 12;
    public int ReserveAmmo { get; private set; } = 30;

    public AuthorityActionEquipmentSnapshot Capture() => new(equippedInstanceId, isReloading, MagazineAmmo, ReserveAmmo);

    public void Restore(AuthorityActionEquipmentSnapshot snapshot)
    {
        equippedInstanceId = snapshot.EquippedInstanceId;
        isReloading = snapshot.IsReloading;
        MagazineAmmo = snapshot.MagazineAmmo;
        ReserveAmmo = snapshot.ReserveAmmo;
    }

    public bool TryBegin(NetworkAnimationActionRequestMessage request, out Action commit, out string reason)
    {
        commit = static () => { };
        if (request.EquipmentInstanceId <= 0)
        {
            reason = "InvalidEquipment";
            return false;
        }

        switch (request.ActionKind)
        {
            case NetworkAnimationActionKind.Equip when equippedInstanceId == 0:
                equippedInstanceId = request.EquipmentInstanceId;
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Unequip when equippedInstanceId == request.EquipmentInstanceId && !isReloading:
                equippedInstanceId = 0;
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Melee when equippedInstanceId == request.EquipmentInstanceId && !isReloading:
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Recoil when equippedInstanceId == request.EquipmentInstanceId && !isReloading && MagazineAmmo > 0:
                MagazineAmmo--;
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Reload when equippedInstanceId == request.EquipmentInstanceId && !isReloading && MagazineAmmo > 0 && MagazineAmmo < 30 && ReserveAmmo > 0:
                isReloading = true;
                commit = CommitReload;
                reason = string.Empty;
                return true;
            default:
                Console.Error.WriteLine(
                    $"[Server][042] ActionRejected Kind={request.ActionKind} RequestedEquipment={request.EquipmentInstanceId} "
                    + $"Equipped={equippedInstanceId} Reloading={isReloading} Magazine={MagazineAmmo} Reserve={ReserveAmmo}");
                reason = "AuthorityEquipmentStateRejected";
                return false;
        }
    }

    public void Complete(NetworkAnimationActionKind actionKind)
    {
        if (actionKind == NetworkAnimationActionKind.Reload) isReloading = false;
    }

    private void CommitReload()
    {
        int required = 30 - MagazineAmmo;
        int loaded = Math.Min(required, ReserveAmmo);
        MagazineAmmo += loaded;
        ReserveAmmo -= loaded;
    }
}

public readonly record struct AuthorityActionEquipmentSnapshot(
    long EquippedInstanceId,
    bool IsReloading,
    int MagazineAmmo,
    int ReserveAmmo);
