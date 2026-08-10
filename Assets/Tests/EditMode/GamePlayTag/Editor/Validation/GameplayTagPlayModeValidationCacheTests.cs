using NUnit.Framework;

namespace CGame.GameplayTags.Editor.Tests
{
    public sealed class GameplayTagPlayModeValidationCacheTests
    {
        [SetUp]
        public void SetUp()
        {
            GameplayTagPlayModeValidationCache.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayTagPlayModeValidationCache.Clear();
        }

        [Test]
        public void Cache_RequiresFirstValidationThenAcceptsCurrentRevision()
        {
            Assert.That(GameplayTagPlayModeValidationCache.RequiresValidation, Is.True);

            GameplayTagPlayModeValidationCache.MarkValidated();

            Assert.That(GameplayTagPlayModeValidationCache.RequiresValidation, Is.False);
        }

        [Test]
        public void Cache_ProductionAssetChangeInvalidatesValidatedRevision()
        {
            GameplayTagPlayModeValidationCache.MarkValidated();

            bool invalidated = GameplayTagPlayModeValidationCache.InvalidateForPaths(
                new[] { "Assets/Gameplay/WeaponDefinition.asset" });

            Assert.That(invalidated, Is.True);
            Assert.That(GameplayTagPlayModeValidationCache.RequiresValidation, Is.True);
        }

        [Test]
        public void Cache_FailedValidationCannotLeavePreviousRevisionAccepted()
        {
            GameplayTagPlayModeValidationCache.MarkValidated();

            GameplayTagPlayModeValidationCache.MarkUnvalidated();

            Assert.That(GameplayTagPlayModeValidationCache.RequiresValidation, Is.True);
        }

        [Test]
        public void Cache_GameplayTagEditorCodeChangeInvalidatesValidatedRevision()
        {
            GameplayTagPlayModeValidationCache.MarkValidated();

            bool invalidated = GameplayTagPlayModeValidationCache.InvalidateForPaths(
                new[] { "Assets/Script/GamePlayTag/Editor/Validation/GameplayTagValidationGate.cs" });

            Assert.That(invalidated, Is.True);
            Assert.That(GameplayTagPlayModeValidationCache.RequiresValidation, Is.True);
        }

        [Test]
        public void Cache_IgnoresAssetsOutsideValidationScope()
        {
            GameplayTagPlayModeValidationCache.MarkValidated();

            bool invalidated = GameplayTagPlayModeValidationCache.InvalidateForPaths(
                new[]
                {
                    "Assets/Textures/Crosshair.png",
                    "Assets/Tests/Fixtures/TestPawn.prefab",
                    "Assets/ThirdParty/Package/Example.asset"
                });

            Assert.That(invalidated, Is.False);
            Assert.That(GameplayTagPlayModeValidationCache.RequiresValidation, Is.False);
        }
    }
}
