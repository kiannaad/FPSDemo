using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.GameplayTags.PlayMode.Tests
{
    public sealed class GameplayTagLauncherGatePlayModeTests
    {
        private GameObject launcherObject;
        private GameObject configObject;
        private GameplayTagSource source;

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            if (launcherObject != null)
            {
                UnityEngine.Object.DestroyImmediate(launcherObject);
            }

            if (configObject != null)
            {
                UnityEngine.Object.DestroyImmediate(configObject);
            }

            if (source != null)
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void LauncherGate_ValidConfigInitializesBeforeLaunchSteps()
        {
            Component launcher = CreateInactiveLauncher();
            GameplayTagConfig config = CreateConfig(valid: true);
            SetConfig(launcher, config);

            bool initialized = (bool)launcher.GetType()
                .GetMethod("InitializeGameplayTags", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(launcher, null);

            Assert.That(initialized, Is.True);
            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.True);
            Assert.That(GameplayTagManager.Instance.TryRequestTag("Combat.Fire", out _), Is.True);
        }

        [UnityTest]
        public IEnumerator LauncherAwake_MissingConfigBlocksStartup()
        {
            Component launcher = CreateInactiveLauncher();
            LogAssert.Expect(LogType.Error, "GameLauncherMgr requires a GameplayTagConfig reference before launch.");

            launcherObject.SetActive(true);
            yield return null;

            bool initialized = (bool)launcher.GetType()
                .GetProperty("GameplayTagsInitialized", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(launcher);
            Assert.That(initialized, Is.False);
            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.False);
        }

        [Test]
        public void LauncherGate_InvalidSourceLeavesManagerUninitialized()
        {
            Component launcher = CreateInactiveLauncher();
            GameplayTagConfig config = CreateConfig(valid: false);
            SetConfig(launcher, config);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("GameplayTag initialization failed: MissingSources"));

            bool initialized = (bool)launcher.GetType()
                .GetMethod("InitializeGameplayTags", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(launcher, null);

            Assert.That(initialized, Is.False);
            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.False);
        }

        [Test]
        public void LauncherOnDestroy_ShutsDownManagerForDomainReloadDisabledFlow()
        {
            Component launcher = CreateInactiveLauncher();
            SetConfig(launcher, CreateConfig(valid: true));
            launcher.GetType().GetMethod("InitializeGameplayTags").Invoke(launcher, null);
            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.True);

            launcher.GetType().GetMethod("OnDestroy").Invoke(launcher, null);
            UnityEngine.Object.DestroyImmediate(launcherObject);
            launcherObject = null;

            Assert.That(GameplayTagManager.Instance.IsInitialized, Is.False);
        }

        private Component CreateInactiveLauncher()
        {
            Type launcherType = Type.GetType("CGame.GameLauncherMgr, Assembly-CSharp", throwOnError: true);
            launcherObject = new GameObject("GameplayTagLauncherGateTest");
            launcherObject.SetActive(false);
            return launcherObject.AddComponent(launcherType);
        }

        private GameplayTagConfig CreateConfig(bool valid)
        {
            configObject = new GameObject("GameplayTagConfigTest");
            GameplayTagConfig config = configObject.AddComponent<GameplayTagConfig>();
            if (valid)
            {
                source = ScriptableObject.CreateInstance<GameplayTagSource>();
                source.SetDefinition(
                    "Core",
                    new[]
                    {
                        new GameplayTagSourceNode(
                            "Combat",
                            children: new[] { new GameplayTagSourceNode("Fire", true) })
                    });
                config.SetDefinition(new[] { source });
            }

            return config;
        }

        private static void SetConfig(Component launcher, GameplayTagConfig config)
        {
            launcher.GetType()
                .GetField("gameplayTagConfig", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(launcher, config);
        }
    }
}
