using MessagePack;
using NUnit.Framework;

namespace Fps.Protocol.Tests;

public sealed class MovementMessagesTests
{
    [Test]
    public void PawnMove_RoundTrip_PreservesStableIdentityAndQuantizedIntent()
    {
        var move = new PawnMoveMessage(
            7,
            100,
            3,
            42,
            1200,
            new QuantizedInputMessage(32767, -16384),
            new QuantizedViewMessage(9000, -1250),
            PawnMoveFlags.Jump | PawnMoveFlags.Sprint,
            new QuantizedVector3Message(1234, 0, -500));

        byte[] bytes = MessagePackSerializer.Serialize(move);
        PawnMoveMessage decoded = MessagePackSerializer.Deserialize<PawnMoveMessage>(bytes);

        Assert.That(decoded, Is.EqualTo(move));
        Assert.That((ushort)MessageId.PawnMove, Is.EqualTo(30));
    }

    [Test]
    public void OwnerReconcile_CorrectionRoundTrip_PreservesAuthorityState()
    {
        var state = new AuthorityStateMessage(
            1201,
            new QuantizedVector3Message(100, 200, 300),
            new QuantizedQuaternionMessage(0, 0, 0, 32767),
            new QuantizedVector3Message(10, 0, -10),
            2,
            true,
            new QuantizedVector3Message(0, 1000, 0),
            0);
        var reconcile = new OwnerReconcileMessage(OwnerReconcileKind.Correction, 42, state, "PositionError");

        OwnerReconcileMessage decoded = MessagePackSerializer.Deserialize<OwnerReconcileMessage>(
            MessagePackSerializer.Serialize(reconcile));

        Assert.That(decoded, Is.EqualTo(reconcile));
        Assert.That((ushort)MessageId.OwnerReconcile, Is.EqualTo(31));
    }

    [Test]
    public void AuthoritySnapshot_RoundTrip_PreservesJoinIdentityAndEveryDigestField()
    {
        var state = new AuthorityStateMessage(
            177,
            new QuantizedVector3Message(101, 202, 303),
            new QuantizedQuaternionMessage(1, 2, 3, 32760),
            new QuantizedVector3Message(-4, 5, -6),
            2,
            true,
            new QuantizedVector3Message(7, 999, 8),
            91);
        var snapshot = new AuthoritySnapshotMessage(17, 200, state);

        AuthoritySnapshotMessage decoded = MessagePackSerializer.Deserialize<AuthoritySnapshotMessage>(
            MessagePackSerializer.Serialize(snapshot));

        Assert.That(decoded, Is.EqualTo(snapshot));
        Assert.That(decoded.MatchId, Is.EqualTo(17));
        Assert.That(decoded.PawnId, Is.EqualTo(200));
        Assert.That((ushort)MessageId.AuthoritySnapshot, Is.EqualTo(32));
    }
}
