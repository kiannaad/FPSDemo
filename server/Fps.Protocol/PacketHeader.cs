namespace Fps.Protocol;

[Flags]
public enum PacketFlags : byte
{
    None = 0,
    Request = 1,
    Response = 2
}

public readonly record struct PacketHeader(
    byte Version,
    MessageId MessageId,
    PacketFlags Flags,
    ulong RequestId,
    long MatchId);
