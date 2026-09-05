using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class NetworkMovementQuantizationTests
    {
        [Test]
        public void QuantizedVector3_MillimeterRoundTrip_IsStable()
        {
            var source = new Vector3(1.2344f, -5.6786f, 0.00049f);

            QuantizedVector3 quantized = QuantizedVector3.FromMeters(source);
            QuantizedVector3 repeated = QuantizedVector3.FromMeters(quantized.ToMeters());

            Assert.That(quantized, Is.EqualTo(new QuantizedVector3(1234, -5679, 0)));
            Assert.That(repeated, Is.EqualTo(quantized));
        }

        [Test]
        public void QuantizedInput_FromUnitVector_ClampsAndRoundTrips()
        {
            QuantizedInput input = QuantizedInput.FromVector2(new Vector2(2f, -1f));

            Assert.That(input.X, Is.EqualTo(short.MaxValue));
            Assert.That(input.Y, Is.EqualTo(-short.MaxValue));
            Assert.That(input.ToVector2().x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(input.ToVector2().y, Is.EqualTo(-1f).Within(0.0001f));
        }

        [Test]
        public void QuantizedView_CentidegreeRoundTrip_IsStable()
        {
            QuantizedView view = QuantizedView.FromDegrees(179.999f, -89.994f);

            Assert.That(view.YawCentidegrees, Is.EqualTo(18000));
            Assert.That(view.PitchCentidegrees, Is.EqualTo(-8999));
            Assert.That(QuantizedView.FromDegrees(view.YawDegrees, view.PitchDegrees), Is.EqualTo(view));
        }

        [TestCase(NetworkMessageId.HelloRequest, NetworkDelivery.ReliableOrdered)]
        [TestCase(NetworkMessageId.MatchStarting, NetworkDelivery.ReliableOrdered)]
        [TestCase(NetworkMessageId.PawnMove, NetworkDelivery.UnreliableSequenced)]
        [TestCase(NetworkMessageId.OwnerReconcile, NetworkDelivery.UnreliableSequenced)]
        [TestCase(NetworkMessageId.AuthoritySnapshot, NetworkDelivery.UnreliableSequenced)]
        [TestCase(NetworkMessageId.FireRequest, NetworkDelivery.ReliableOrdered)]
        [TestCase(NetworkMessageId.FireCommitted, NetworkDelivery.ReliableOrdered)]
        [TestCase(NetworkMessageId.FireRejected, NetworkDelivery.ReliableOrdered)]
        public void DeliveryPolicy_MapsControlAndPhysicsDataPlanes(
            NetworkMessageId messageId,
            NetworkDelivery expected)
        {
            Assert.That(NetworkDeliveryPolicy.For(messageId), Is.EqualTo(expected));
        }
    }
}
