using Fps.Protocol;
using Fps.ServerDomain.Matches;
using NUnit.Framework;

namespace Fps.ServerDomain.Tests;

public sealed class AuthorityFireProcessorTests
{
    [Test]
    public void Process_DuplicateClientSequence_ReusesCommitAndConsumesOneRound()
    {
        var equipment = new AuthorityActionEquipmentState();
        Assert.That(equipment.TryBegin(new NetworkAnimationActionRequestMessage(7, 1, 1, NetworkAnimationActionKind.Equip, "ak", 9), out _, out _), Is.True);
        var processor = new AuthorityFireProcessor();
        var request = new FireRequestMessage(7, 1, 2, 1, 9, 0, 0, 0, 0, 0, 0, 1);

        FireResolution first = processor.Process(request, 7, 1, 20, equipment);
        FireResolution repeated = processor.Process(request, 7, 1, 21, equipment);

        Assert.That(first.Committed, Is.Not.Null);
        Assert.That(repeated.Committed!.ShotSequence, Is.EqualTo(first.Committed!.ShotSequence));
        Assert.That(equipment.MagazineAmmo, Is.EqualTo(11));
    }

    [Test]
    public void Process_TooSoon_DoesNotConsumeRound()
    {
        var equipment = new AuthorityActionEquipmentState();
        Assert.That(equipment.TryBegin(new NetworkAnimationActionRequestMessage(7, 1, 1, NetworkAnimationActionKind.Equip, "ak", 9), out _, out _), Is.True);
        var processor = new AuthorityFireProcessor();

        processor.Process(new FireRequestMessage(7, 1, 2, 1, 9, 0, 0, 0, 0, 0, 0, 1), 7, 1, 20, equipment);
        FireResolution rejected = processor.Process(new FireRequestMessage(7, 1, 3, 2, 9, 0, 0, 0, 0, 0, 0, 1), 7, 1, 21, equipment);

        Assert.That(rejected.Rejected!.Reason, Is.EqualTo(FireRejectionReason.RateLimited));
        Assert.That(equipment.MagazineAmmo, Is.EqualTo(11));
    }

    [Test]
    public void Process_HitQuery_EmbedsOneAuthorityImpactInCommit()
    {
        var equipment = new AuthorityActionEquipmentState();
        Assert.That(equipment.TryBegin(new NetworkAnimationActionRequestMessage(7, 1, 1, NetworkAnimationActionKind.Equip, "ak", 9), out _, out _), Is.True);
        var processor = new AuthorityFireProcessor();
        var request = new FireRequestMessage(7, 1, 2, 1, 9, 0, 0, 0, 0, 0, 0, 1);

        FireResolution resolution = processor.Process(
            request,
            7,
            1,
            20,
            equipment,
            new AuthorityFireImpact(true, 1f, 2f, 3f, 0f, 1f, 0f, "Ground"));

        Assert.That(resolution.Committed, Is.Not.Null);
        Assert.That(resolution.Committed!.HasImpact, Is.True);
        Assert.That(resolution.Committed.ImpactId, Is.EqualTo(resolution.Committed.ShotSequence));
        Assert.That(resolution.Committed.ImpactPositionY, Is.EqualTo(2f));
        Assert.That(resolution.Committed.SurfaceId, Is.EqualTo("Ground"));
    }
}
