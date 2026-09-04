using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CGame.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Tests.Gameplay
{
    public sealed class LevelRuntimeTests
    {
        private const string ScenePath = "Assets/LevelRuntimeTests.unity";
        private Scene scene;
        private LevelDefinition definition;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            definition = ScriptableObject.CreateInstance<LevelDefinition>();
            Transform players = CreateRoot(LevelDefinitionScanner.PlayerContainerName);
            CreateChild(players, "PlayerPoint 1", Vector3.zero);
            Transform enemies = CreateRoot(LevelDefinitionScanner.EnemyContainerName);
            CreateChild(enemies, "EnemyPoint 1", Vector3.right);
            CreateChild(enemies, "EnemyPoint 2", Vector3.right * 2);
            CreateChild(enemies, "EnemyPoint 3", Vector3.right * 3);
            LevelDefinitionScanner.ScanAndApply(scene, definition);
        }

        [TearDown]
        public void TearDown()
        {
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(ScenePath);
        }

        [Test]
        public void Reservation_TransitionsAndRegistrationIdentityAreStrict()
        {
            var runtime = new LevelRuntime(definition, scene);
            SpawnPointReservation reservation = runtime.ReserveFirstPlayerPoint();
            Assert.That(runtime.GetStatus(reservation.PointId).State, Is.EqualTo(SpawnPointState.Reserved));
            Guid registrationId = Guid.NewGuid();
            reservation.Commit(registrationId);
            SpawnPointStatus occupied = runtime.GetStatus("PlayerPoint 1");
            Assert.That(occupied.State, Is.EqualTo(SpawnPointState.Occupied));
            Assert.That(occupied.HasOccupiedTag, Is.True);
            Assert.That(runtime.ReleaseOccupied("PlayerPoint 1", Guid.NewGuid()), Is.False);
            Assert.That(runtime.ReleaseOccupied("PlayerPoint 1", registrationId), Is.True);
            Assert.That(runtime.ReleaseOccupied("PlayerPoint 1", registrationId), Is.False);
        }

        [Test]
        public void EnemySelection_IsWithoutReplacementAndInsufficientCountHasNoSideEffects()
        {
            var runtime = new LevelRuntime(definition, scene);
            Assert.Throws<InvalidOperationException>(() => runtime.ReserveRandomEnemyPoints(4, new System.Random(1)));
            Assert.That(runtime.GetStatus("EnemyPoint 1").State, Is.EqualTo(SpawnPointState.Available));
            var reservations = runtime.ReserveRandomEnemyPoints(3, new System.Random(1));
            Assert.That(reservations.Select(item => item.PointId).Distinct().Count(), Is.EqualTo(3));
            foreach (SpawnPointReservation reservation in reservations) reservation.Dispose();
            Assert.That(runtime.GetStatus("EnemyPoint 2").State, Is.EqualTo(SpawnPointState.Available));
        }

        [Test]
        public void ReserveEnemyPoint_UsesTheNamedAvailableEnemyPointOnly()
        {
            var runtime = new LevelRuntime(definition, scene);

            SpawnPointReservation reservation = runtime.ReserveEnemyPoint("EnemyPoint 2");

            Assert.That(reservation.PointId, Is.EqualTo("EnemyPoint 2"));
            Assert.That(runtime.GetStatus("EnemyPoint 2").State, Is.EqualTo(SpawnPointState.Reserved));
            Assert.Throws<InvalidOperationException>(() => runtime.ReserveEnemyPoint("EnemyPoint 2"));
            Assert.Throws<InvalidOperationException>(() => runtime.ReserveEnemyPoint("PlayerPoint 1"));
            reservation.Dispose();
        }

        [Test]
        public void TransformMismatchPreventsRuntimeCreation()
        {
            GameObject.Find("EnemyPoint 1").transform.position += Vector3.up;
            Assert.Throws<InvalidOperationException>(() => new LevelRuntime(definition, scene));
        }

        [Test]
        public void ShutdownClearsReservedAndOccupiedPoints()
        {
            var runtime = new LevelRuntime(definition, scene);
            SpawnPointReservation player = runtime.ReserveFirstPlayerPoint();
            player.Commit(Guid.NewGuid());
            SpawnPointReservation enemy = runtime.ReserveRandomEnemyPoints(1, new System.Random(2))[0];
            runtime.Shutdown();
            Assert.That(runtime.GetStatus("PlayerPoint 1").State, Is.EqualTo(SpawnPointState.Available));
            Assert.That(runtime.GetStatus(enemy.PointId).State, Is.EqualTo(SpawnPointState.Available));
            Assert.Throws<ObjectDisposedException>(() => runtime.ReserveFirstPlayerPoint());
        }

        [Test]
        public void World_CreatesOwnsAndShutsDownLevelRuntime()
        {
            var runtime = new LevelRuntime(definition, scene);
            var configuration = ScriptableObject.CreateInstance<TestWorldConfiguration>();
            configuration.Runtime = runtime;
            World world = null;
            try
            {
                world = World.Create(configuration);
                world.InitializeAsync().GetAwaiter().GetResult();
                Assert.That(world.LevelRuntime, Is.SameAs(runtime));
                world.ShutdownAsync().GetAwaiter().GetResult();
                Assert.That(world.LevelRuntime, Is.Null);
                Assert.Throws<ObjectDisposedException>(() => runtime.ReserveFirstPlayerPoint());
            }
            finally
            {
                if (world != null && world.State != WorldState.Destroyed) world.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void PawnFactoryFailure_RollsBackReservationActorsAndPlayerOwnership()
        {
            var runtime = new LevelRuntime(definition, scene);
            PawnData pawnData = PawnData.CreateRuntime();
            PlayerStateDefinition playerState = ScriptableObject.CreateInstance<PlayerStateDefinition>();
            playerState.Configure(pawnData, null, null);
            var configuration = ScriptableObject.CreateInstance<FailingPawnWorldConfiguration>();
            configuration.Runtime = runtime;
            configuration.PlayerState = playerState;
            World world = null;
            try
            {
                world = World.Create(configuration);
                Assert.Throws<InvalidOperationException>(() => world.InitializeAsync().GetAwaiter().GetResult());
                Assert.That(runtime.GetStatus("PlayerPoint 1").State, Is.EqualTo(SpawnPointState.Available));
                Assert.That(world.RegisteredActorCount, Is.Zero);
                Assert.That(world.LocalPlayer, Is.Null);
                Assert.That(world.GameMode, Is.Null);
            }
            finally
            {
                if (world != null && world.State != WorldState.Destroyed) world.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(configuration);
                UnityEngine.Object.DestroyImmediate(playerState);
                UnityEngine.Object.DestroyImmediate(pawnData);
            }
        }

        [Test]
        public void EnemyBatchFailure_RollsBackPriorActorsAndAllReservations()
        {
            var runtime = new LevelRuntime(definition, scene);
            var configuration = ScriptableObject.CreateInstance<TestWorldConfiguration>();
            configuration.Runtime = runtime;
            var prefab = new GameObject("EnemyTestPrefab");
            var state = ScriptableObject.CreateInstance<EnemyPlayerStateDefinition>();
            state.Configure(prefab);
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.Configure(state);
            World world = null;
            EnemySpawnGameComponent component = null;
            try
            {
                world = World.Create(configuration);
                world.InitializeAsync().GetAwaiter().GetResult();
                world.StartPlay();
                component = new EnemySpawnGameComponent(
                    world,
                    enemy,
                    3,
                    new EnemyPawnSpawner(new FailingSecondEnemyPawnFactory()));

                Assert.Throws<InvalidOperationException>(() => component.StartInitialSpawn().GetAwaiter().GetResult());
                Assert.That(world.RegisteredActorCount, Is.Zero);
                Assert.That(component.Handles.Count, Is.Zero);
                Assert.That(runtime.GetStatus("EnemyPoint 1").State, Is.EqualTo(SpawnPointState.Available));
                Assert.That(runtime.GetStatus("EnemyPoint 2").State, Is.EqualTo(SpawnPointState.Available));
                Assert.That(runtime.GetStatus("EnemyPoint 3").State, Is.EqualTo(SpawnPointState.Available));
            }
            finally
            {
                component?.Dispose();
                if (world != null && world.State != WorldState.Destroyed) world.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(configuration);
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(state);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static void CreateChild(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.position = position;
        }

        private sealed class TestWorldConfiguration : WorldConfiguration
        {
            public LevelRuntime Runtime { get; set; }
            public override System.Collections.Generic.IReadOnlyList<WorldSubSystem> CreateWorldSubSystems() => Array.Empty<WorldSubSystem>();
            public override LevelRuntime CreateLevelRuntime() => Runtime;
        }

        private sealed class FailingPawnWorldConfiguration : WorldConfiguration
        {
            public LevelRuntime Runtime { get; set; }
            public PlayerStateDefinition PlayerState { get; set; }
            public override System.Collections.Generic.IReadOnlyList<WorldSubSystem> CreateWorldSubSystems() => Array.Empty<WorldSubSystem>();
            public override LevelRuntime CreateLevelRuntime() => Runtime;
            public override GameMode CreateGameMode(World world, Player player) =>
                new DefaultGameMode(world, player, PlayerState, new FailingPawnFactory());
        }

        private sealed class FailingPawnFactory : PawnFactory
        {
            public override Task<Pawn> CreateAsync(
                PawnDefinition pawnDefinition,
                InputProfile inputProfile,
                Vector3 position,
                Quaternion rotation,
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Injected PawnFactory failure.");
        }

        private sealed class FailingSecondEnemyPawnFactory : EnemyTargetPawnFactory
        {
            private int calls;
            public override Task<Pawn> CreateAsync(EnemyPlayerStateDefinition definition, Vector3 position, Quaternion rotation)
            {
                calls++;
                if (calls == 2) throw new InvalidOperationException("Injected second enemy failure.");
                var root = new GameObject($"EnemyCandidate{calls}");
                root.transform.SetPositionAndRotation(position, rotation);
                root.SetActive(false);
                return Task.FromResult(new Pawn(root, new ActorComponent[] { new HealthComponent() }));
            }
        }

    }
}
