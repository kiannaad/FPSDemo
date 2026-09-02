using MessagePack;
using NUnit.Framework;

namespace Fps.Protocol.Tests;

public sealed class AnimationActionMessagesTests
{
    [Test]
    public void Started_RoundTrip_PreservesIdentityPhaseAndVariant()
    {
        var started = new NetworkAnimationActionStartedMessage(
            7, 3, 99, 11, 150, 120, 180,
            NetworkAnimationActionKind.Reload, "ak12-reload", 9);

        NetworkAnimationActionStartedMessage decoded = MessagePackSerializer.Deserialize<NetworkAnimationActionStartedMessage>(
            MessagePackSerializer.Serialize(started));

        Assert.That(decoded, Is.EqualTo(started));
        Assert.That((ushort)MessageId.AnimationActionStarted, Is.EqualTo(41));
        Assert.That((ushort)MessageId.AnimationActionCancelled, Is.EqualTo(44));
    }
}
