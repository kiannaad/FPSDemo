using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Gameplay.PlayMode.Tests
{
    public sealed class PawnSpawnTransactionPlayModeTests
    {
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (World.Current != null)
            {
                yield return Await(World.Current.ShutdownAsync());
            }

            for (int index = runtimeAssets.Count - 1; index >= 0; index--)
            {
                if (runtimeAssets[index] != null)
                {
                    UnityEngine.Object.Destroy(runtimeAssets[index]);
                }
            }

            runtimeAssets.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionPawn_ActivatesOnlyAfterPossessionAndWorldStart()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: true, requireCamera: true);
            World world = CreateWorld(definition);

            yield return Await(world.InitializeAsync());

            Pawn pawn = ((DefaultGameMode)world.GameMode).DefaultPawn;
            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            Assert.That(pawn.State, Is.EqualTo(ActorState.Initialized));
            Assert.That(pawn.Root.activeSelf, Is.False);
            Assert.That(controller.PossessedPawn, Is.SameAs(pawn));

            world.StartPlay();
            world.FixedTick(0.02f);
            world.UpdateTick(0.02f);
            world.LateTick(0.02f);
            Assert.That(pawn.State, Is.EqualTo(ActorState.Playing));
            Assert.That(pawn.Root.activeSelf, Is.True);
            Assert.That(pawn.GetComponent<PawnCameraComponent>().Camera.enabled, Is.True);

            GameObject root = pawn.Root;
            yield return Await(world.ShutdownAsync());
            yield return null;

            Assert.That(root == null, Is.True);
            Assert.That(World.Current, Is.Null);
        }

        [UnityTest]
        public IEnumerator MissingCamera_FailsBeforePlayAndLeavesNoClone()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: false, requireCamera: true);
            World world = CreateWorld(definition);
            Task initialization = world.InitializeAsync();
            while (!initialization.IsCompleted)
            {
                yield return null;
            }

            Assert.That(initialization.IsFaulted, Is.True);
            Assert.That(initialization.Exception?.InnerException?.Message, Does.Contain("requires a Camera"));
            Assert.That(world.RegisteredActorCount, Is.Zero);
            yield return null;
            Assert.That(CountRuntimePawnClones(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator RebuildPawn_DestroysOldRootAndKeepsOnePossession()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: true, requireCamera: true);
            World world = CreateWorld(definition);
            yield return Await(world.InitializeAsync());
            world.StartPlay();
            DefaultGameMode gameMode = (DefaultGameMode)world.GameMode;
            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            PlayerState playerState = controller.PlayerState;
            Pawn previousPawn = gameMode.DefaultPawn;
            GameObject previousRoot = previousPawn.Root;

            yield return Await(gameMode.SpawnDefaultPawn(controller));
            yield return null;

            Assert.That(previousRoot == null, Is.True);
            Assert.That(gameMode.DefaultPawn, Is.SameAs(controller.PossessedPawn));
            Assert.That(controller.PlayerState, Is.SameAs(playerState));
            Assert.That(playerState.CurrentPawn, Is.SameAs(gameMode.DefaultPawn));
            Assert.That(world.RegisteredActorCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator TickFault_DestroysPawnAndClearsControllerReferences()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: false, requireCamera: false);
            World world = CreateWorld(definition, new FaultingPawnFactory());
            yield return Await(world.InitializeAsync());
            world.StartPlay();
            DefaultGameMode gameMode = (DefaultGameMode)world.GameMode;
            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            Pawn pawn = gameMode.DefaultPawn;
            GameObject root = pawn.Root;

            world.UpdateTick(0.02f);
            yield return null;

            Assert.That(root == null, Is.True);
            Assert.That(pawn.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(gameMode.DefaultPawn, Is.Null);
            Assert.That(controller.PossessedPawn, Is.Null);
            Assert.That(controller.PlayerState.CurrentPawn, Is.Null);
            Assert.That(world.RegisteredActorCount, Is.EqualTo(2));
        }

        private World CreateWorld(PawnDefinition definition, PawnFactory factory = null)
        {
            TestConfiguration configuration = Track(ScriptableObject.CreateInstance<TestConfiguration>());
            configuration.Definition = definition;
            configuration.Factory = factory;
            return World.Create(configuration);
        }

        private PawnDefinition CreateDefinition(bool hasCamera, bool requireCamera)
        {
            GameObject prefab = Track(new GameObject("PlayModePawnPrefab"));
            prefab.SetActive(false);
            if (hasCamera)
            {
                GameObject cameraObject = new GameObject("PawnCamera");
                cameraObject.transform.SetParent(prefab.transform, false);
                cameraObject.AddComponent<Camera>().enabled = false;
            }

            return Track(PawnDefinition.CreateRuntime(prefab, requireCamera, new AbilitySet()));
        }

        private T Track<T>(T asset) where T : UnityEngine.Object
        {
            runtimeAssets.Add(asset);
            return asset;
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception.InnerException;
            }
        }

        private static int CountRuntimePawnClones()
        {
            int count = 0;
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null
                    && objects[index].scene.IsValid()
                    && objects[index].name.StartsWith("Pawn:", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class TestConfiguration : WorldConfiguration
        {
            public PawnDefinition Definition { get; set; }
            public PawnFactory Factory { get; set; }

            public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems() =>
                Array.Empty<WorldSubSystem>();

            public override GameMode CreateGameMode(World world, Player player) =>
                new DefaultGameMode(world, player, Definition, null, Factory);
        }

        private sealed class FaultingPawnFactory : PawnFactory
        {
            public override Task<Pawn> CreateAsync(
                PawnDefinition definition,
                Vector3 position,
                Quaternion rotation,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = new GameObject("Pawn:PlayModeFault");
                root.SetActive(false);
                return Task.FromResult<Pawn>(new Pawn(
                    root,
                    new ActorComponent[] { new FaultingTickComponent() }));
            }
        }

        private sealed class FaultingTickComponent : ActorComponent
        {
            protected override void OnInitialize()
            {
                AddTickTask("Pawn.PlayModeFault", TickGroup.TG_Gameplay, deltaTime =>
                    throw new InvalidOperationException("pawn play mode tick fault"));
            }
        }
    }
}
