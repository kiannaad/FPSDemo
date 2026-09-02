using Fps.Protocol;
using Fps.ServerDomain.Matches;
using NUnit.Framework;

namespace Fps.ServerDomain.Tests;

public sealed class AuthorityAnimationActionTimelineTests
{
    [Test]
    public void ReloadCommit_ChangesAmmoOnlyAtCommitTickAndOnlyOnce()
    {
        int magazineAmmo = 12;
        var timeline = new AuthorityAnimationActionTimeline();
        var request = new NetworkAnimationActionRequestMessage(
            7, 3, 99, NetworkAnimationActionKind.Reload, "ak12-reload", 9);

        Assert.That(timeline.TryStart(
            request, 7, 3, 240, 120, 30, () => magazineAmmo = 30,
            out NetworkAnimationActionStartedMessage? started, out string reason), Is.True, reason);
        Assert.That(started!.CommitTick, Is.EqualTo(270));

        timeline.AdvanceTo(269);
        Assert.That(magazineAmmo, Is.EqualTo(12));
        timeline.AdvanceTo(270);
        timeline.AdvanceTo(271);
        Assert.That(magazineAmmo, Is.EqualTo(30));
    }

    [Test]
    public void Start_RejectsWrongPossessionBeforeAllocatingSequence()
    {
        var timeline = new AuthorityAnimationActionTimeline();
        var request = new NetworkAnimationActionRequestMessage(
            7, 2, 99, NetworkAnimationActionKind.Reload, "ak12-reload", 9);

        Assert.That(timeline.TryStart(
            request, 7, 3, 240, 120, 30, null!, out _, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("PossessionMismatch"));
    }

    [Test]
    public void EquipmentState_RestoreAfterRejectedTimeline_KeepsAuthorityStateUnchanged()
    {
        var state = new AuthorityActionEquipmentState();
        var equip = new NetworkAnimationActionRequestMessage(7, 3, 1, NetworkAnimationActionKind.Equip, "ak12", 9);
        Assert.That(state.TryBegin(equip, out _, out _), Is.True);
        AuthorityActionEquipmentSnapshot beforeRejectedReload = state.Capture();
        var invalidReload = new NetworkAnimationActionRequestMessage(7, 3, 2, NetworkAnimationActionKind.Reload, "", 9);

        Assert.That(state.TryBegin(invalidReload, out _, out _), Is.True);
        state.Restore(beforeRejectedReload);

        Assert.That(state.EquippedInstanceId, Is.EqualTo(9));
        Assert.That(state.IsReloading, Is.False);
        Assert.That(state.MagazineAmmo, Is.EqualTo(12));
    }
}
