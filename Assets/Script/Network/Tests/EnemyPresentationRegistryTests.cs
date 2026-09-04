using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace CGame.Network.Tests
{
    public sealed class EnemyPresentationRegistryTests
    {
        private GameObject prefab;
        private EnemyArchetypeSpec archetype;
        private EnemyPresentationCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("EnemyPresentationPrefab");
            var presentation = prefab.AddComponent<EnemyPresentation>();
            presentation.Configure(prefab.AddComponent<Animator>());
            archetype = ScriptableObject.CreateInstance<EnemyArchetypeSpec>();
            archetype.Configure("Enemy.Test", prefab);
            catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            catalog.Configure(archetype);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(archetype);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void SnapshotInterpolation_UsesOneTenthSecondStep_AndFreezesAfterHalfSecond()
        {
            using var registry = new EnemyPresentationRegistry(catalog);
            Assert.That(registry.Spawn(Spawn()), Is.True);
            Assert.That(registry.ApplySnapshot(Snapshot(10_000)), Is.True);

            registry.Tick(0.05f, Time.realtimeSinceStartup);
            EnemyPresentation instance = Object.FindObjectsByType<EnemyPresentation>(UnityEngine.FindObjectsSortMode.None)
                .Single(candidate => candidate.gameObject != prefab);
            Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.01f));

            registry.Tick(0.1f, Time.realtimeSinceStartup + 0.6f);
            Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.01f));
        }

        private static EnemySpawnedEvent Spawn() => new EnemySpawnedEvent
        {
            EnemyId = 1,
            ArchetypeId = "Enemy.Test",
            Position = QuantizedVector3WireMessage.FromValue(new QuantizedVector3(0, 0, 0)),
            Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
            AuthorityServerTick = 1,
            Health = 100
        };

        private static EnemySnapshotEvent Snapshot(int xMillimeters) => new EnemySnapshotEvent
        {
            EnemyId = 1,
            AuthorityServerTick = 2,
            Position = QuantizedVector3WireMessage.FromValue(new QuantizedVector3(xMillimeters, 0, 0)),
            Rotation = QuantizedQuaternionWireMessage.FromValue(new QuantizedQuaternion(0, 0, 0, short.MaxValue)),
            PlanarVelocity = QuantizedVector3WireMessage.FromValue(default),
            Health = 100
        };
    }
}
