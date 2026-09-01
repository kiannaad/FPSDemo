using NUnit.Framework;

namespace Fps.Protocol.Tests;

public sealed class PacketCodecTests
{
    [Test]
    public void EncodeThenDecode_PreservesHeaderAndPayload()
    {
        var header = new PacketHeader(
            ProtocolVersion.Current,
            MessageId.HelloRequest,
            PacketFlags.Request,
            RequestId: 42,
            MatchId: 0);
        byte[] payload = [1, 2, 3, 4];

        byte[] packet = PacketCodec.Encode(header, payload);

        Assert.That(PacketCodec.TryDecode(packet, out PacketHeader decoded, out ReadOnlyMemory<byte> decodedPayload), Is.True);
        Assert.That(decoded, Is.EqualTo(header));
        Assert.That(decodedPayload.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public void TryDecode_WithUnknownMessageId_ReturnsFalse()
    {
        byte[] packet = PacketCodec.Encode(
            new PacketHeader(ProtocolVersion.Current, MessageId.HelloRequest, PacketFlags.Request, 1, 0),
            []);
        packet[1] = byte.MaxValue;
        packet[2] = byte.MaxValue;

        Assert.That(PacketCodec.TryDecode(packet, out _, out _), Is.False);
    }

    [Test]
    public void TryDecode_WithPayloadLengthMismatch_ReturnsFalse()
    {
        byte[] packet = PacketCodec.Encode(
            new PacketHeader(ProtocolVersion.Current, MessageId.HelloRequest, PacketFlags.Request, 1, 0),
            [1, 2]);
        packet[4] = 3;

        Assert.That(PacketCodec.TryDecode(packet, out _, out _), Is.False);
    }
}
