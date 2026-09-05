using Fps.Protocol;

namespace Fps.ServerDomain.Matches;

public sealed class AuthorityActionEquipmentState
{
    private const int MagazineCapacity = 30;
    private const int InitialMagazineAmmo = 12;
    private const int InitialReserveAmmo = 30;

    private readonly Dictionary<long, AuthorityWeaponAmmoState> ammoByEquipmentInstanceId = new();
    private long equippedInstanceId;
    private bool isReloading;

    public long EquippedInstanceId => equippedInstanceId;
    public bool IsReloading => isReloading;
    public int MagazineAmmo => GetEquippedAmmo().MagazineAmmo;
    public int ReserveAmmo => GetEquippedAmmo().ReserveAmmo;

    public AuthorityActionEquipmentSnapshot Capture() => new(
        equippedInstanceId,
        isReloading,
        ammoByEquipmentInstanceId.Select(pair => new AuthorityWeaponAmmoSnapshot(
            pair.Key,
            pair.Value.MagazineAmmo,
            pair.Value.ReserveAmmo)).ToArray());

    public void Restore(AuthorityActionEquipmentSnapshot snapshot)
    {
        equippedInstanceId = snapshot.EquippedInstanceId;
        isReloading = snapshot.IsReloading;
        ammoByEquipmentInstanceId.Clear();
        foreach (AuthorityWeaponAmmoSnapshot ammo in snapshot.WeaponAmmo)
            ammoByEquipmentInstanceId.Add(ammo.EquipmentInstanceId, new AuthorityWeaponAmmoState(ammo.MagazineAmmo, ammo.ReserveAmmo));
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
                GetEquippedAmmo();
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Unequip when equippedInstanceId == request.EquipmentInstanceId && !isReloading:
                equippedInstanceId = 0;
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Melee when equippedInstanceId == request.EquipmentInstanceId && !isReloading:
                reason = string.Empty;
                return true;
            case NetworkAnimationActionKind.Reload when equippedInstanceId == request.EquipmentInstanceId && !isReloading && MagazineAmmo < MagazineCapacity && ReserveAmmo > 0:
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

    public bool TryConsumeFire(long equipmentInstanceId, out string reason)
    {
        if (equipmentInstanceId != equippedInstanceId)
        {
            reason = "EquipmentMismatch";
            return false;
        }
        if (isReloading)
        {
            reason = "Reloading";
            return false;
        }
        if (MagazineAmmo <= 0)
        {
            reason = "OutOfAmmo";
            return false;
        }
        GetEquippedAmmo().MagazineAmmo--;
        reason = string.Empty;
        return true;
    }

    private void CommitReload()
    {
        AuthorityWeaponAmmoState ammo = GetEquippedAmmo();
        int required = MagazineCapacity - ammo.MagazineAmmo;
        int loaded = Math.Min(required, ammo.ReserveAmmo);
        ammo.MagazineAmmo += loaded;
        ammo.ReserveAmmo -= loaded;
    }

    private AuthorityWeaponAmmoState GetEquippedAmmo()
    {
        if (equippedInstanceId <= 0) return new AuthorityWeaponAmmoState(0, 0);
        if (!ammoByEquipmentInstanceId.TryGetValue(equippedInstanceId, out AuthorityWeaponAmmoState? ammo))
        {
            ammo = new AuthorityWeaponAmmoState(InitialMagazineAmmo, InitialReserveAmmo);
            ammoByEquipmentInstanceId.Add(equippedInstanceId, ammo);
        }
        return ammo;
    }
}

public readonly record struct AuthorityActionEquipmentSnapshot(
    long EquippedInstanceId,
    bool IsReloading,
    AuthorityWeaponAmmoSnapshot[] WeaponAmmo)
{
    public int MagazineAmmo => GetEquippedAmmo().MagazineAmmo;
    public int ReserveAmmo => GetEquippedAmmo().ReserveAmmo;

    private AuthorityWeaponAmmoSnapshot GetEquippedAmmo()
    {
        foreach (AuthorityWeaponAmmoSnapshot ammo in WeaponAmmo)
            if (ammo.EquipmentInstanceId == EquippedInstanceId) return ammo;
        return default;
    }
}

public readonly record struct AuthorityWeaponAmmoSnapshot(
    long EquipmentInstanceId,
    int MagazineAmmo,
    int ReserveAmmo);

internal sealed class AuthorityWeaponAmmoState
{
    public AuthorityWeaponAmmoState(int magazineAmmo, int reserveAmmo)
    {
        MagazineAmmo = magazineAmmo;
        ReserveAmmo = reserveAmmo;
    }

    public int MagazineAmmo { get; set; }
    public int ReserveAmmo { get; set; }
}
