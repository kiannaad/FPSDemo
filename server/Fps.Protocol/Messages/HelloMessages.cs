using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public sealed record HelloRequest(
    [property: Key(0)] string ClientVersion,
    [property: Key(1)] byte RequestedProtocolVersion);

[MessagePackObject]
public sealed record HelloResponse(
    [property: Key(0)] string ServerVersion,
    [property: Key(1)] byte ProtocolVersion);
