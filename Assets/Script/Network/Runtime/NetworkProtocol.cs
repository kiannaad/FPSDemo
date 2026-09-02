using System;
using System.Buffers.Binary;
using MessagePack;

namespace CGame.Network
{
    public enum NetworkMessageId : ushort
    {
        HelloRequest = 1,
        HelloResponse = 2,
        CreateRoomRequest = 10,
        CreateRoomResponse = 11,
        JoinRoomRequest = 12,
        JoinRoomResponse = 13,
        SetReadyRequest = 14,
        SetReadyResponse = 15,
        MatchStarting = 20,
        PawnSpawned = 21,
        PossessionChanged = 22,
        PawnMove = 30,
        OwnerReconcile = 31,
        AuthoritySnapshot = 32,
        AnimationActionRequest = 40,
        AnimationActionStarted = 41,
        AnimationActionCommit = 42,
        AnimationActionEnded = 43,
        AnimationActionCancelled = 44
    }

    public enum NetworkDelivery
    {
        ReliableOrdered,
        UnreliableSequenced
    }

    public static class NetworkDeliveryPolicy
    {
        public static NetworkDelivery For(NetworkMessageId messageId)
        {
            switch (messageId)
            {
                case NetworkMessageId.PawnMove:
                case NetworkMessageId.OwnerReconcile:
                case NetworkMessageId.AuthoritySnapshot:
                    return NetworkDelivery.UnreliableSequenced;
                default:
                    return NetworkDelivery.ReliableOrdered;
            }
        }
    }

    [Flags]
    public enum NetworkPacketFlags : byte
    {
        None = 0,
        Request = 1,
        Response = 2,
        Failure = 4
    }

    public readonly struct NetworkPacketHeader
    {
        public NetworkPacketHeader(
            byte version,
            NetworkMessageId messageId,
            NetworkPacketFlags flags,
            ulong requestId,
            long matchId)
        {
            Version = version;
            MessageId = messageId;
            Flags = flags;
            RequestId = requestId;
            MatchId = matchId;
        }

        public byte Version { get; }
        public NetworkMessageId MessageId { get; }
        public NetworkPacketFlags Flags { get; }
        public ulong RequestId { get; }
        public long MatchId { get; }
    }

    public static class NetworkPacketCodec
    {
        public const byte ProtocolVersion = 1;
        public const int HeaderLength = 24;

        public static byte[] Encode(NetworkPacketHeader header, ReadOnlySpan<byte> payload)
        {
            if (header.Version != ProtocolVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(header));
            }

            byte[] packet = new byte[HeaderLength + payload.Length];
            Span<byte> target = packet;
            target[0] = header.Version;
            BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(1), (ushort)header.MessageId);
            target[3] = (byte)header.Flags;
            BinaryPrimitives.WriteInt32LittleEndian(target.Slice(4), payload.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(target.Slice(8), header.RequestId);
            BinaryPrimitives.WriteInt64LittleEndian(target.Slice(16), header.MatchId);
            payload.CopyTo(target.Slice(HeaderLength));
            return packet;
        }

        public static bool TryDecode(byte[] packet, out NetworkPacketHeader header, out byte[] payload)
        {
            header = default;
            payload = null;
            if (packet == null || packet.Length < HeaderLength || packet[0] != ProtocolVersion)
            {
                return false;
            }

            NetworkMessageId messageId = (NetworkMessageId)BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(1));
            if (!Enum.IsDefined(typeof(NetworkMessageId), messageId))
            {
                return false;
            }

            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4));
            if (payloadLength < 0 || packet.Length != HeaderLength + payloadLength)
            {
                return false;
            }

            header = new NetworkPacketHeader(
                packet[0],
                messageId,
                (NetworkPacketFlags)packet[3],
                BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(8)),
                BinaryPrimitives.ReadInt64LittleEndian(packet.AsSpan(16)));
            payload = new byte[payloadLength];
            Array.Copy(packet, HeaderLength, payload, 0, payloadLength);
            return true;
        }
    }

    public static class NetworkMessageSerializer
    {
        public static byte[] Serialize<T>(T value) => MessagePackSerializer.Serialize(value);

        public static T Deserialize<T>(byte[] payload) => MessagePackSerializer.Deserialize<T>(payload);
    }

    [MessagePackObject]
    public sealed class HelloRequest
    {
        public HelloRequest(string clientVersion, byte requestedProtocolVersion)
        {
            ClientVersion = clientVersion;
            RequestedProtocolVersion = requestedProtocolVersion;
        }

        [Key(0)] public string ClientVersion { get; }
        [Key(1)] public byte RequestedProtocolVersion { get; }
    }

    [MessagePackObject]
    public sealed class HelloResponse
    {
        [Key(0)] public string ServerVersion { get; set; }
        [Key(1)] public byte ProtocolVersion { get; set; }
    }

    [MessagePackObject]
    public sealed class CreateRoomRequest
    {
        [Key(0)] public string RequestedRoomName { get; set; }
    }

    [MessagePackObject]
    public sealed class CreateRoomResponse
    {
        [Key(0)] public string RoomId { get; set; }
        [Key(1)] public long PlayerId { get; set; }
        [Key(2)] public string State { get; set; }
    }

    [MessagePackObject]
    public sealed class JoinRoomRequest
    {
        [Key(0)] public string RoomId { get; set; }
    }

    [MessagePackObject]
    public sealed class JoinRoomResponse
    {
        [Key(0)] public string RoomId { get; set; }
        [Key(1)] public long PlayerId { get; set; }
        [Key(2)] public string State { get; set; }
    }

    [MessagePackObject]
    public sealed class SetReadyRequest
    {
        [Key(0)] public string RoomId { get; set; }
        [Key(1)] public bool IsReady { get; set; }
    }

    [MessagePackObject]
    public sealed class SetReadyResponse
    {
        [Key(0)] public string RoomId { get; set; }
        [Key(1)] public string State { get; set; }
        [Key(2)] public bool MatchStarted { get; set; }
    }

    [MessagePackObject]
    public sealed class NetworkRpcFailureResponse
    {
        [Key(0)] public string Reason { get; set; }
    }

    [MessagePackObject]
    public sealed class MatchStartingEvent
    {
        [Key(0)] public long MatchId { get; set; }
        [Key(1)] public long StartTick { get; set; }
        [Key(2)] public string DataEndpoint { get; set; }
        [Key(3)] public string CredentialId { get; set; }
    }

    [MessagePackObject]
    public sealed class PawnSpawnedEvent
    {
        [Key(0)] public long PawnId { get; set; }
        [Key(1)] public long OwnerPlayerId { get; set; }
        [Key(2)] public string SpawnPointId { get; set; }
    }

    [MessagePackObject]
    public sealed class PossessionChangedEvent
    {
        [Key(0)] public long PlayerId { get; set; }
        [Key(1)] public long PawnId { get; set; }
        [Key(2)] public long PossessionRevision { get; set; }
    }
}
