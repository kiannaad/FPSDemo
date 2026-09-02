using Fps.Protocol;
using Fps.ServerDomain.Matches;
using NUnit.Framework;

namespace Fps.ServerDomain.Tests;

public sealed class AuthorityActionEquipmentStateTests
{
    [Test]
    public void TryBegin_RecoilForEquippedWeapon_IsAcceptedAndConsumesOneAuthorityMagazineRound()
    {
        var state = new AuthorityActionEquipmentState();
        state.TryBegin(Request(NetworkAnimationActionKind.Equip), out _, out _);
        AuthorityActionEquipmentSnapshot before = state.Capture();

        bool accepted = state.TryBegin(Request(NetworkAnimationActionKind.Recoil), out Action commit, out string reason);

        Assert.That(accepted, Is.True, reason);
        Assert.That(state.EquippedInstanceId, Is.EqualTo(before.EquippedInstanceId));
        Assert.That(state.IsReloading, Is.EqualTo(before.IsReloading));
        Assert.That(state.MagazineAmmo, Is.EqualTo(before.MagazineAmmo - 1));
        Assert.That(state.ReserveAmmo, Is.EqualTo(before.ReserveAmmo));
        Assert.DoesNotThrow(() => commit());
    }

    private static NetworkAnimationActionRequestMessage Request(NetworkAnimationActionKind kind) => new(
        PawnId: 1,
        PossessionRevision: 1,
        PredictionNonce: 1,
        ActionKind: kind,
        VariantId: "weapon",
        EquipmentInstanceId: 9);
}
