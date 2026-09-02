using Fps.Protocol;
using Fps.ServerDomain.Matches;
using NUnit.Framework;

namespace Fps.ServerDomain.Tests;

public sealed class AuthorityActionEquipmentStateTests
{
    [Test]
    public void TryBegin_RecoilForEquippedWeapon_IsRejectedWithoutChangingAuthorityMagazine()
    {
        var state = new AuthorityActionEquipmentState();
        state.TryBegin(Request(NetworkAnimationActionKind.Equip), out _, out _);
        AuthorityActionEquipmentSnapshot before = state.Capture();

        bool accepted = state.TryBegin(Request(NetworkAnimationActionKind.Recoil), out Action commit, out string reason);

        Assert.That(accepted, Is.False);
        Assert.That(reason, Is.EqualTo("AuthorityEquipmentStateRejected"));
        Assert.That(state.EquippedInstanceId, Is.EqualTo(before.EquippedInstanceId));
        Assert.That(state.IsReloading, Is.EqualTo(before.IsReloading));
        Assert.That(state.MagazineAmmo, Is.EqualTo(before.MagazineAmmo));
        Assert.That(state.ReserveAmmo, Is.EqualTo(before.ReserveAmmo));
        Assert.DoesNotThrow(() => commit());
    }

    [Test]
    public void TryBegin_ReloadEmptyMagazineWithReserve_CommitsAndThenRejectsWhenReserveIsEmpty()
    {
        var state = new AuthorityActionEquipmentState();
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Equip), out _, out _), Is.True);

        for (int index = 0; index < 12; index++)
            Assert.That(state.TryConsumeFire(9, out _), Is.True);

        Assert.That(state.MagazineAmmo, Is.Zero);
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Reload), out Action firstCommit, out string firstReason), Is.True, firstReason);
        firstCommit();
        state.Complete(NetworkAnimationActionKind.Reload);
        Assert.That(state.MagazineAmmo, Is.EqualTo(30));
        Assert.That(state.ReserveAmmo, Is.Zero);
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Reload), out _, out string rejection), Is.False);
        Assert.That(rejection, Is.EqualTo("AuthorityEquipmentStateRejected"));
    }

    [Test]
    public void SwitchingWeapons_PreservesEachWeaponAuthorityAmmoIndependently()
    {
        var state = new AuthorityActionEquipmentState();

        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Equip, 9), out _, out _), Is.True);
        for (int index = 0; index < 12; index++)
            Assert.That(state.TryConsumeFire(9, out _), Is.True);
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Reload, 9), out Action reload, out _), Is.True);
        reload();
        state.Complete(NetworkAnimationActionKind.Reload);
        Assert.That(state.MagazineAmmo, Is.EqualTo(30));
        Assert.That(state.ReserveAmmo, Is.Zero);
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Unequip, 9), out _, out _), Is.True);

        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Equip, 10), out _, out _), Is.True);
        Assert.That(state.MagazineAmmo, Is.EqualTo(12));
        Assert.That(state.ReserveAmmo, Is.EqualTo(30));
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Unequip, 10), out _, out _), Is.True);

        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Equip, 9), out _, out _), Is.True);
        Assert.That(state.MagazineAmmo, Is.EqualTo(30));
        Assert.That(state.ReserveAmmo, Is.Zero);
        Assert.That(state.TryBegin(Request(NetworkAnimationActionKind.Reload, 9), out _, out string rejection), Is.False);
        Assert.That(rejection, Is.EqualTo("AuthorityEquipmentStateRejected"));
    }

    private static NetworkAnimationActionRequestMessage Request(NetworkAnimationActionKind kind, long equipmentInstanceId = 9) => new(
        PawnId: 1,
        PossessionRevision: 1,
        PredictionNonce: 1,
        ActionKind: kind,
        VariantId: "weapon",
        EquipmentInstanceId: equipmentInstanceId);
}
