using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network039")]
    public sealed class NetworkTickClockTests
    {
        [Test]
        public void AdvanceOneTick_AfterObservation_ProgressesBetweenSnapshots()
        {
            var clock = new NetworkTickClock();
            clock.Observe(120);

            clock.AdvanceOneTick();

            Assert.That(clock.EstimatedServerTick, Is.EqualTo(121));
        }

        [Test]
        public void AdvanceOneTick_BeforeObservation_DoesNotInventServerTime()
        {
            var clock = new NetworkTickClock();

            clock.AdvanceOneTick();

            Assert.That(clock.HasObservation, Is.False);
            Assert.That(clock.EstimatedServerTick, Is.Zero);
        }

        [Test]
        public void Observe_NewerAuthorityTickCorrectsEstimateThatRanAhead()
        {
            var clock = new NetworkTickClock();
            clock.Observe(100);
            clock.AdvanceOneTick();
            clock.AdvanceOneTick();

            clock.Observe(101);

            Assert.That(clock.LastObservedServerTick, Is.EqualTo(101));
            Assert.That(clock.EstimatedServerTick, Is.EqualTo(101));
        }
    }
}
