using Fps.ServerDomain.Matches;
using NUnit.Framework;

namespace Fps.ServerDomain.Tests;

public sealed class AuthorityTargetRegistryTests
{
    [Test]
    public void ApplyAcceptedHit_ThreeShots_ProducesAbsoluteTerminalState()
    {
        AuthorityTargetRegistry registry = AuthorityTargetRegistry.Create(new[] { new AuthorityTargetDefinition("EnemyPoint 1", 60f) });

        Assert.That(registry.ApplyAcceptedHit("EnemyPoint 1", 1, 1, 20f)!.State, Is.EqualTo(new AuthorityTargetState("EnemyPoint 1", 40f, 60f, 1, false)));
        Assert.That(registry.ApplyAcceptedHit("EnemyPoint 1", 2, 2, 20f)!.State, Is.EqualTo(new AuthorityTargetState("EnemyPoint 1", 20f, 60f, 2, false)));
        Assert.That(registry.ApplyAcceptedHit("EnemyPoint 1", 2, 3, 20f)!.State, Is.EqualTo(new AuthorityTargetState("EnemyPoint 1", 0f, 60f, 3, true)));
        Assert.That(registry.ApplyAcceptedHit("EnemyPoint 1", 2, 4, 20f), Is.Null);
    }

    [Test]
    public void ApplyAcceptedHit_InvalidOrUnknownTarget_DoesNotChangeSnapshot()
    {
        AuthorityTargetRegistry registry = AuthorityTargetRegistry.Create(new[] { new AuthorityTargetDefinition("EnemyPoint 1", 60f) });

        Assert.That(registry.ApplyAcceptedHit(null, 1, 1, 20f), Is.Null);
        Assert.That(registry.ApplyAcceptedHit("wall", 1, 2, 20f), Is.Null);
        Assert.That(registry.Snapshot(), Is.EqualTo(new[] { new AuthorityTargetState("EnemyPoint 1", 60f, 60f, 0, false) }));
    }
}
