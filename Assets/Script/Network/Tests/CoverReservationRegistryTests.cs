using NUnit.Framework;

namespace CGame.Network.Tests
{
    public sealed class CoverReservationRegistryTests
    {
        [Test]
        public void Reservation_IsExclusiveIdempotentAndReusableAfterRelease()
        {
            var registry = new CoverReservationRegistry();

            Assert.That(registry.TryReserve("Cover.A", 101), Is.True);
            Assert.That(registry.TryReserve("Cover.A", 101), Is.True);
            Assert.That(registry.TryReserve("Cover.A", 102), Is.False);
            Assert.That(registry.IsReservedBy("Cover.A", 101), Is.True);

            registry.ReleaseByEnemy(101);

            Assert.That(registry.IsReservedBy("Cover.A", 101), Is.False);
            Assert.That(registry.TryReserve("Cover.A", 102), Is.True);
        }
    }
}
