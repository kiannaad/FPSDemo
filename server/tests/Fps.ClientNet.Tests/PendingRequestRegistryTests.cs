using Fps.ClientNet;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.ClientNet.Tests;

public sealed class PendingRequestRegistryTests
{
    [Test]
    public async Task TryComplete_UsesRequestIdWhenResponsesArriveOutOfOrder()
    {
        var registry = new PendingRequestRegistry();
        Task<RpcResponse> first = registry.Register(10, DateTimeOffset.UtcNow.AddSeconds(1));
        Task<RpcResponse> second = registry.Register(20, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.That(registry.TryComplete(CreateResponse(20)), Is.True);
        Assert.That(registry.TryComplete(CreateResponse(10)), Is.True);

        Assert.That((await first).Header.RequestId, Is.EqualTo(10));
        Assert.That((await second).Header.RequestId, Is.EqualTo(20));
    }

    [Test]
    public void Sweep_ExpiresOnlyRequestsPastTheirDeadline()
    {
        var registry = new PendingRequestRegistry();
        Task<RpcResponse> expired = registry.Register(1, DateTimeOffset.UnixEpoch.AddSeconds(1));
        Task<RpcResponse> pending = registry.Register(2, DateTimeOffset.UnixEpoch.AddSeconds(3));

        registry.Sweep(DateTimeOffset.UnixEpoch.AddSeconds(2));

        Assert.That(expired.IsFaulted, Is.True);
        Assert.That(expired.Exception!.InnerException, Is.TypeOf<TimeoutException>());
        Assert.That(pending.IsCompleted, Is.False);
    }

    [Test]
    public void Disconnect_FailsEveryPendingRequestWithDisconnectReason()
    {
        var registry = new PendingRequestRegistry();
        Task<RpcResponse> first = registry.Register(1, DateTimeOffset.MaxValue);
        Task<RpcResponse> second = registry.Register(2, DateTimeOffset.MaxValue);

        registry.Disconnect("server closed the connection");

        Assert.That(first.Exception!.InnerException, Is.TypeOf<IOException>());
        Assert.That(second.Exception!.InnerException!.Message, Is.EqualTo("server closed the connection"));
        Assert.That(registry.PendingCount, Is.Zero);
    }

    private static RpcResponse CreateResponse(ulong requestId)
    {
        return new RpcResponse(
            new PacketHeader(ProtocolVersion.Current, MessageId.HelloResponse, PacketFlags.Response, requestId, 0),
            Array.Empty<byte>());
    }
}
