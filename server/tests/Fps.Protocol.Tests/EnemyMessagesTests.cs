using MessagePack;
using NUnit.Framework;

namespace Fps.Protocol.Tests;

public sealed class EnemyMessagesTests
{
    [Test]
    public void SpawnSnapshotAndAction_RoundTrip_PreserveStableIdentityAndOrderingFields()
    {
        var spawn = new EnemySpawnedMessage(
            501,
            "Enemy.Rifle",
            new QuantizedVector3Message(1250, 0, -3400),
            new QuantizedQuaternionMessage(0, 7071, 0, 7071),
            40,
            100);
        var snapshot = new EnemySnapshotMessage(
            501,
            42,
            new QuantizedVector3Message(1300, 0, -3300),
            new QuantizedQuaternionMessage(0, 7071, 0, 7071),
            new QuantizedVector3Message(500, 0, 0),
            3,
            100);
        var action = new EnemyActionMessage(501, 7, EnemyActionKind.Fire, 42, 3);

        Assert.That(MessagePackSerializer.Deserialize<EnemySpawnedMessage>(MessagePackSerializer.Serialize(spawn)), Is.EqualTo(spawn));
        Assert.That(MessagePackSerializer.Deserialize<EnemySnapshotMessage>(MessagePackSerializer.Serialize(snapshot)), Is.EqualTo(snapshot));
        Assert.That(MessagePackSerializer.Deserialize<EnemyActionMessage>(MessagePackSerializer.Serialize(action)), Is.EqualTo(action));
    }

    [TestCase(MessageId.EnemySpawned, PacketFlags.None)]
    [TestCase(MessageId.EnemyAction, PacketFlags.None)]
    [TestCase(MessageId.EnemyResyncRequest, PacketFlags.Request)]
    public void EnemyControlMessages_ArePacketCodecCompatible(MessageId messageId, PacketFlags flags)
    {
        var header = new PacketHeader(ProtocolVersion.Current, messageId, flags, 0, 9);

        byte[] packet = PacketCodec.Encode(header, [1, 2, 3]);

        Assert.That(PacketCodec.TryDecode(packet, out PacketHeader decoded, out ReadOnlyMemory<byte> payload), Is.True);
        Assert.That(decoded, Is.EqualTo(header));
        Assert.That(payload.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }
}
