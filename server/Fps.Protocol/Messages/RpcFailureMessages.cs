using MessagePack;

namespace Fps.Protocol;

[MessagePackObject]
public sealed record RpcFailureResponse([property: Key(0)] string Reason);
