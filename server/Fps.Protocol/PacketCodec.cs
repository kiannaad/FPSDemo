using System.Buffers.Binary;

namespace Fps.Protocol;

public static class PacketCodec
{
    public const int HeaderLength = 24;

    public static byte[] Encode(PacketHeader header, ReadOnlySpan<byte> payload)
    {
        if (header.Version != ProtocolVersion.Current)
        {
            throw new ArgumentOutOfRangeException(nameof(header), "Only the current protocol version can be encoded.");
        }

        var packet = new byte[HeaderLength + payload.Length];
        Span<byte> target = packet;
        target[0] = header.Version;
        BinaryPrimitives.WriteUInt16LittleEndian(target[1..], (ushort)header.MessageId);
        target[3] = (byte)header.Flags;
        BinaryPrimitives.WriteInt32LittleEndian(target[4..], payload.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(target[8..], header.RequestId);
        BinaryPrimitives.WriteInt64LittleEndian(target[16..], header.MatchId);
        payload.CopyTo(target[HeaderLength..]);
        return packet;
    }

    public static bool TryDecode(ReadOnlySpan<byte> packet, out PacketHeader header, out ReadOnlyMemory<byte> payload)
    {
        header = default;
        payload = default;
        if (packet.Length < HeaderLength || packet[0] != ProtocolVersion.Current)
        {
            return false;
        }

        MessageId messageId = (MessageId)BinaryPrimitives.ReadUInt16LittleEndian(packet[1..]);
        if (!Enum.IsDefined(messageId))
        {
            return false;
        }

        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(packet[4..]);
        if (payloadLength < 0 || packet.Length != HeaderLength + payloadLength)
        {
            return false;
        }

        header = new PacketHeader(
            packet[0],
            messageId,
            (PacketFlags)packet[3],
            BinaryPrimitives.ReadUInt64LittleEndian(packet[8..]),
            BinaryPrimitives.ReadInt64LittleEndian(packet[16..]));
        payload = packet[HeaderLength..].ToArray();
        return true;
    }
}
