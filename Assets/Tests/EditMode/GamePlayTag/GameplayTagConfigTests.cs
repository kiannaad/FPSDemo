using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CGame.GameplayTags.Tests
{
    public sealed class GameplayTagConfigTests
    {
        private readonly List<Object> objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            foreach (Object target in objectsToDestroy)
            {
                Object.DestroyImmediate(target);
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void Config_StoresExplicitSourceAndRedirectLists()
        {
            GameplayTagSource source = Source("Core", Node("Combat", false, Node("Fire", true)));
            GameplayTagConfig config = Config();
            config.SetDefinition(new[] { source }, new[] { new GameplayTagRedirect("Old.Fire", "Combat.Fire") });

            Assert.That(config.Sources, Is.EqualTo(new[] { source }));
            Assert.That(config.Redirects.Single().OldName, Is.EqualTo("Old.Fire"));
        }

        [Test]
        public void Config_ValidDefinitionInitializesFrozenRegistry()
        {
            GameplayTagConfig config = Config();
            config.SetDefinition(new[] { Source("Core", Node("Combat", false, Node("Fire", true))) });

            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(config.Sources, config.Redirects);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(GameplayTagManager.Instance.RequestTag("combat.fire").Name, Is.EqualTo("Combat.Fire"));
        }

        [Test]
        public void Config_MissingSourcesFailsWithoutPartialInitialization()
        {
            GameplayTagConfig config = Config();

            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(config.Sources, config.Redirects);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("MissingSources"));
            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.False);
        }

        [Test]
        public void Config_DuplicateSourcesAndInvalidRedirectFailAtomically()
        {
            GameplayTagSource first = Source("Core", Node("Combat", false, Node("Fire", true)));
            GameplayTagSource second = Source("core", Node("Utility", true));
            GameplayTagConfig config = Config();
            config.SetDefinition(
                new[] { first, second },
                new[] { new GameplayTagRedirect("Old.Fire", "Missing.Fire") });

            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(config.Sources, config.Redirects);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Select(error => error.Code), Does.Contain("DuplicateSource"));
            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.False);
        }

        [Test]
        public void Shutdown_AllowsCleanInitializationWithAReplacementConfig()
        {
            GameplayTagConfig firstConfig = Config();
            firstConfig.SetDefinition(new[] { Source("First", Node("Combat", false, Node("Fire", true))) });
            Assert.That(GameplayTagManager.Instance.Initialize(firstConfig.Sources).Succeeded, Is.True);

            GameplayTagManager.Instance.Shutdown();
            GameplayTagConfig secondConfig = Config();
            secondConfig.SetDefinition(new[] { Source("Second", Node("Utility", true)) });
            Assert.That(GameplayTagManager.Instance.Initialize(secondConfig.Sources).Succeeded, Is.True);

            Assert.That(GameplayTagManager.Instance.TryRequestTag("Combat.Fire", out _), Is.False);
            Assert.That(GameplayTagManager.Instance.TryRequestTag("Utility", out _), Is.True);
        }

        [Test]
        public void Config_SetDefinitionAcceptsSequencesDerivedFromItsCurrentViews()
        {
            GameplayTagSource first = Source("First", Node("Combat", true));
            GameplayTagSource second = Source("Second", Node("UI", true));
            GameplayTagConfig config = Config();
            config.SetDefinition(new[] { first }, new[] { new GameplayTagRedirect("Old.Combat", "Combat") });

            config.SetDefinition(config.Sources.Concat(new[] { second }), config.Redirects);

            Assert.That(config.Sources, Is.EqualTo(new[] { first, second }));
            Assert.That(config.Redirects.Select(redirect => redirect.OldName), Is.EqualTo(new[] { "Old.Combat" }));

            config.SetDefinition(config.Sources.Where(source => source != second), config.Redirects);

            Assert.That(config.Sources, Is.EqualTo(new[] { first }));
            Assert.That(config.Redirects.Select(redirect => redirect.NewName), Is.EqualTo(new[] { "Combat" }));
        }

        private GameplayTagConfig Config()
        {
            var gameObject = new GameObject("GameplayTagConfigTest");
            objectsToDestroy.Add(gameObject);
            return gameObject.AddComponent<GameplayTagConfig>();
        }

        private GameplayTagSource Source(string sourceName, params GameplayTagSourceNode[] roots)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(sourceName, roots);
            objectsToDestroy.Add(source);
            return source;
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }
    }
}
