using MessagePack;
using NUnit.Framework;

namespace Fps.Protocol.Tests;

public sealed class TargetMessagesTests
{
    [Test]
    public void Snapshot_RoundTrip_PreservesAbsoluteStateAndCause()
    {
        var snapshot = new TargetStateSnapshotMessage(new[]
        {
            new TargetStateMessage("EnemyPoint 1", 3, 0f, 60f, true, 2, 3),
            new TargetStateMessage("EnemyPoint 2", 0, 60f, 60f, false, 0, 0)
        });

        TargetStateSnapshotMessage decoded = MessagePackSerializer.Deserialize<TargetStateSnapshotMessage>(MessagePackSerializer.Serialize(snapshot));

        Assert.That(decoded.Targets, Is.EqualTo(snapshot.Targets));
    }
}
