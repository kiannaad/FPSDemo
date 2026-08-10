using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.WorldRuntime.PlayMode.Tests
{
    public sealed class GameInstancePlayModeTests
    {
        [UnityTest]
        public IEnumerator AwakeStartsEmptyWorldAndDestroyShutsItDown()
        {
            var gameObject = new GameObject("GameInstance");
            GameInstance instance = gameObject.AddComponent<GameInstance>();
            while (!instance.InitializationTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(instance.InitializationTask.IsFaulted, Is.False);
            Assert.That(instance.RuntimeWorld.State, Is.EqualTo(WorldState.Playing));
            Assert.That(World.Current, Is.SameAs(instance.RuntimeWorld));

            Object.Destroy(gameObject);
            while (World.Current != null)
            {
                yield return null;
            }

            Assert.That(World.Current, Is.Null);
        }

        [UnityTest]
        public IEnumerator ResourceManagerInitializesBeforeAssetManagerAndShutsDownInReverse()
        {
            World world = World.Create(new WorldSubSystem[]
            {
                new AssetManager(),
                new ResourceManager("DefaultPackage")
            });
            Task initialization = world.InitializeAsync();
            while (!initialization.IsCompleted)
            {
                yield return null;
            }

            Assert.That(initialization.IsFaulted, Is.False);
            Assert.That(world.GetSubSystem<ResourceManager>().IsReady, Is.True);
            Assert.That(world.GetSubSystem<AssetManager>().Assets, Is.Not.Null);
            world.StartPlay();

            Task shutdown = world.ShutdownAsync();
            while (!shutdown.IsCompleted)
            {
                yield return null;
            }

            Assert.That(shutdown.IsFaulted, Is.False);
            Assert.That(World.Current, Is.Null);
        }

        [UnityTest]
        public IEnumerator DestroyDuringInitialization_CancelsObservesAndClearsCurrentWorld()
        {
            var configuration = ScriptableObject.CreateInstance<BlockingWorldConfiguration>();
            var gameObject = new GameObject("CancelingGameInstance");
            gameObject.SetActive(false);
            GameInstance instance = gameObject.AddComponent<GameInstance>();
            FieldInfo field = typeof(GameInstance).GetField(
                "worldConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(instance, configuration);
            gameObject.SetActive(true);
            yield return null;

            Assert.That(instance.RuntimeWorld.State, Is.EqualTo(WorldState.Initializing));
            Object.Destroy(gameObject);
            while (World.Current != null)
            {
                yield return null;
            }

            Object.Destroy(configuration);
            Assert.That(World.Current, Is.Null);
        }

        private sealed class BlockingWorldConfiguration : WorldConfiguration
        {
            public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems()
            {
                return new WorldSubSystem[] { new BlockingWorldSubSystem() };
            }
        }

        private sealed class BlockingWorldSubSystem : WorldSubSystem
        {
            protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                return Task.Delay(Timeout.Infinite, cancellationToken);
            }
        }
    }
}
