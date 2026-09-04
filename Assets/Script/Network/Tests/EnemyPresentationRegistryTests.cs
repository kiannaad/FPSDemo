using System.IO;
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
            Assert.That(instance.RemoteAnimationState.IsMoving, Is.False);
        }

        [Test]
        public void RemoteEnemyAnimationState_MapsSnapshotVelocityFacingAndGrounding()
        {
            var snapshot = new EnemySnapshotEvent
            {
                EnemyId = 1,
                AuthorityServerTick = 4,
                Rotation = QuantizedQuaternionWireMessage.FromValue(QuantizedQuaternion.FromQuaternion(Quaternion.Euler(0f, 90f, 0f))),
                PlanarVelocity = QuantizedVector3WireMessage.FromValue(QuantizedVector3.FromMeters(new Vector3(2f, 0f, 0f))),
                IsGrounded = true,
                BrainState = EnemyBrainState.CoverHold
            };

            RemoteEnemyAnimationState state = RemoteEnemyAnimationState.FromSnapshot(snapshot);

            Assert.That(state.IsMoving, Is.True);
            Assert.That(state.Speed, Is.EqualTo(2f).Within(0.001f));
            Assert.That(state.MoveDirection, Is.EqualTo(Vector2.right));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.BrainState, Is.EqualTo(EnemyBrainState.CoverHold));
            Assert.That(state.IsInCover, Is.True);
            Assert.That(state.IsPeeking, Is.False);
        }

        [TestCase(EnemyBrainState.CoverHold, true, false)]
        [TestCase(EnemyBrainState.PeekFire, true, true)]
        [TestCase(EnemyBrainState.ReturnToCover, true, false)]
        [TestCase(EnemyBrainState.Chase, false, false)]
        public void RemoteEnemyAnimationState_DerivesCoverParameters(
            EnemyBrainState brainState,
            bool expectedInCover,
            bool expectedPeeking)
        {
            var state = new RemoteEnemyAnimationState(0f, Vector2.zero, true, Quaternion.identity, brainState);

            Assert.That(state.IsInCover, Is.EqualTo(expectedInCover));
            Assert.That(state.IsPeeking, Is.EqualTo(expectedPeeking));
        }

        [Test]
        public void RemotePresentation_SourceUsesSnapshotStateWithoutClientMotorOrNavMesh()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "Script/Network/Runtime/Enemy/EnemyPresentation.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("RemoteEnemyAnimationState"));
            Assert.That(source, Does.Contain("applyRootMotion = false"));
            Assert.That(source, Does.Not.Contain("CharacterPhysicsMotor"));
            Assert.That(source, Does.Not.Contain("NavMeshAgent"));
        }

        [Test]
        public void ActionPresentation_RecordsOnlyConfirmedFireAndHitActions()
        {
            using var registry = new EnemyPresentationRegistry(catalog);
            Assert.That(registry.Spawn(Spawn()), Is.True);

            Assert.That(registry.ApplyAction(new EnemyActionEvent { EnemyId = 1, ActionSequence = 1, ActionKind = EnemyActionKind.Fire, AuthorityServerTick = 1 }), Is.True);
            EnemyPresentation instance = Object.FindObjectsByType<EnemyPresentation>(UnityEngine.FindObjectsSortMode.None)
                .Single(candidate => candidate.gameObject != prefab);
            Assert.That(instance.LastConfirmedAction, Is.EqualTo(EnemyActionKind.Fire));
            Assert.That(registry.ApplyAction(new EnemyActionEvent { EnemyId = 1, ActionSequence = 2, ActionKind = EnemyActionKind.Hit, AuthorityServerTick = 2 }), Is.True);
            Assert.That(instance.LastConfirmedAction, Is.EqualTo(EnemyActionKind.Hit));
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
