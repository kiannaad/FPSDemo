using System;
using CGame.Editor;
using CGame.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests.Gameplay
{
    public sealed class GameplayAssemblyAssetContractTests
    {
        [Test]
        public void TPSBundlePresentationCatalog_ValidatesAllProjectOwnedVariants()
        {
            EnemyPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(
                "Assets/Settings/Gameplay/Enemy/EnemyPresentationCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Archetypes.Count, Is.EqualTo(3));
            Assert.DoesNotThrow(() => EnemyPresentationClosureValidator.Validate(catalog));
            foreach (EnemyArchetypeSpec archetype in catalog.Archetypes)
            {
                Assert.That(archetype.PresentationPrefab.GetComponentInChildren<EnemyPresentation>(true), Is.Not.Null);
                Assert.That(archetype.PresentationPrefab.GetComponentInChildren<Animator>(true).applyRootMotion, Is.False);
            }
        }

        [Test]
        public void EnemyPresentationCatalog_RejectsDuplicateArchetypeIds()
        {
            EnemyArchetypeSpec first = ScriptableObject.CreateInstance<EnemyArchetypeSpec>();
            EnemyArchetypeSpec second = ScriptableObject.CreateInstance<EnemyArchetypeSpec>();
            EnemyPresentationCatalog catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            // Duplicate-ID validation only needs a valid existing prefab, not a disk write.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Enemies/TPSBundle/Prefabs/NetworkEnemyPistol.prefab");
            try
            {
                Assert.That(prefab, Is.Not.Null);
                first.Configure("Enemy.Pistol", prefab);
                second.Configure("Enemy.Pistol", prefab);
                catalog.Configure(first, second);
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => EnemyPresentationClosureValidator.Validate(catalog));
                StringAssert.Contains("Duplicate enemy archetype ID", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

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
