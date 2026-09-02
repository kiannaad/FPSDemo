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
}
