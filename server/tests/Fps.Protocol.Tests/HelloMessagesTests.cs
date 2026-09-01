using MessagePack;
using NUnit.Framework;

namespace Fps.Protocol.Tests;

public sealed class HelloMessagesTests
{
    [Test]
    public void HelloRequest_UsesStableIntegerKeysForRoundTrip()
    {
        var request = new HelloRequest("client-033", ProtocolVersion.Current);

        byte[] bytes = MessagePackSerializer.Serialize(request);
        HelloRequest decoded = MessagePackSerializer.Deserialize<HelloRequest>(bytes);

        Assert.That(decoded, Is.EqualTo(request));
    }
}
