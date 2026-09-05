using NUnit.Framework;

namespace CGame.Network.Tests
{
    [Category("Network038")]
    public sealed class DedicatedDataCredentialResolverTests
    {
        [Test]
        public void Resolve_KnownPawnCredential_ReturnsConfiguredIdentity()
        {
            var resolver = new DedicatedDataCredentialResolver("secret", new[]
            {
                new DedicatedAuthorityPawnConfiguration(100, 1, 7, "spawn-a", "connection-a"),
                new DedicatedAuthorityPawnConfiguration(200, 2, 9, "spawn-b", "connection-b")
            });

            bool resolved = resolver.TryResolve("secret:200", out DedicatedDataIdentity identity);

            Assert.That(resolved, Is.True);
            Assert.That(identity.PawnId, Is.EqualTo(200));
            Assert.That(identity.ConnectionId, Is.EqualTo("connection-b"));
            Assert.That(identity.PossessionRevision, Is.EqualTo(9));
        }

        [TestCase("secret")]
        [TestCase("secret:300")]
        [TestCase("wrong:100")]
        public void Resolve_InvalidCredential_Rejects(string credential)
        {
            var resolver = new DedicatedDataCredentialResolver("secret", new[]
            {
                new DedicatedAuthorityPawnConfiguration(100, 1, 7, "spawn-a", "connection-a")
            });

            Assert.That(resolver.TryResolve(credential, out _), Is.False);
        }
    }
}
