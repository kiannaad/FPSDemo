using NUnit.Framework;

namespace CGame.Tests
{
    public sealed class CharacterDefinitionLocationResolverTests
    {
        [Test]
        public void LocalPlayer_ResolvesToCharacterDefinitionLocation()
        {
            ICharacterDefinitionLocationResolver resolver =
                new CharacterDefinitionLocationResolver();

            bool resolved = resolver.TryResolveLocation(
                new CharacterDefinitionId("local-player"),
                out string location);

            Assert.IsTrue(resolved);
            Assert.AreEqual("CharacterDefinition", location);
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown")]
        [TestCase("Local-Player")]
        public void InvalidOrUnknownId_DoesNotResolve(
            string definitionId)
        {
            ICharacterDefinitionLocationResolver resolver =
                new CharacterDefinitionLocationResolver();

            bool resolved = resolver.TryResolveLocation(
                new CharacterDefinitionId(definitionId),
                out string location);

            Assert.IsFalse(resolved);
            Assert.IsTrue(string.IsNullOrEmpty(location));
        }
    }
}
