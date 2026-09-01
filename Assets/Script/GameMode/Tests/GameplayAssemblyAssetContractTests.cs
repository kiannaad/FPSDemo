using System;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Tests.Gameplay
{
    public sealed class GameplayAssemblyAssetContractTests
    {
        [Test]
        public void PlayerStateDefinition_RejectsMissingPawnAssemblyMembers()
        {
            PlayerStateDefinition playerState = ScriptableObject.CreateInstance<PlayerStateDefinition>();
            PawnData pawn = ScriptableObject.CreateInstance<PawnData>();
            try
            {
                playerState.Configure(pawn, null, null);
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    playerState.ValidateRequiredReferences);
                StringAssert.Contains("ControllerDefinition", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerState);
                UnityEngine.Object.DestroyImmediate(pawn);
            }
        }

        [Test]
        public void ExperienceDefinition_RejectsDuplicateFeatureIds()
        {
            ExperienceDefinition experience = ScriptableObject.CreateInstance<ExperienceDefinition>();
            GameFeatureConfig first = ScriptableObject.CreateInstance<GameFeatureConfig>();
            GameFeatureConfig second = ScriptableObject.CreateInstance<GameFeatureConfig>();
            try
            {
                first.Configure("Feature.Targets");
                second.Configure("Feature.Targets");
                Assert.Throws<InvalidOperationException>(() => experience.Configure(first, second));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(experience);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void GameBootstrap_RequiresLevelAndGameModeTogether()
        {
            GameBootstrap bootstrap = ScriptableObject.CreateInstance<GameBootstrap>();
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => bootstrap.ConfigureGameplayAssembly(level, null));
                Assert.Throws<ArgumentNullException>(() => bootstrap.ConfigureGameplayAssembly(null, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bootstrap);
                UnityEngine.Object.DestroyImmediate(level);
            }
        }
    }
}
