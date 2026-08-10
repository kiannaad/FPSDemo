using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.Ability;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Gameplay.Tests
{
    public sealed class PawnSpawnTransactionTests
    {
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            World.Current?.ShutdownAsync().GetAwaiter().GetResult();
            for (int index = runtimeAssets.Count - 1; index >= 0; index--)
            {
                if (runtimeAssets[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeAssets[index]);
                }
            }

            runtimeAssets.Clear();
        }

        [Test]
        public void InitializePossessActivateAndShutdown_AreOneWorldOwnedTransaction()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: true, requireCamera: true);
            World world = CreateWorld(definition);

            world.InitializeAsync().GetAwaiter().GetResult();

            DefaultGameMode gameMode = (DefaultGameMode)world.GameMode;
            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            Pawn pawn = gameMode.DefaultPawn;
            Assert.That(world.RegisteredActorCount, Is.EqualTo(3));
            Assert.That(pawn.State, Is.EqualTo(ActorState.Initialized));
            Assert.That(pawn.Root.activeSelf, Is.False);
            Assert.That(pawn.Transform, Is.SameAs(pawn.Root.transform));
            Assert.That(controller.PossessedPawn, Is.SameAs(pawn));
            Assert.That(controller.PlayerState.CurrentPawn, Is.SameAs(pawn));
            Assert.That(pawn.Controller, Is.SameAs(controller));
            Assert.That(pawn.GetComponent<PawnMovementComponent>().TickCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => pawn.DestroyCandidate());

            world.StartPlay();
            world.FixedTick(0.02f);
            world.UpdateTick(0.02f);
            world.LateTick(0.02f);

            Assert.That(pawn.State, Is.EqualTo(ActorState.Playing));
            Assert.That(pawn.Root.activeSelf, Is.True);
            Assert.That(pawn.GetComponent<PawnMovementComponent>().TickCount, Is.EqualTo(1));
            Assert.That(pawn.GetComponent<PawnAnimationComponent>().PreAnimationTickCount, Is.EqualTo(1));
            Assert.That(pawn.GetComponent<PawnAnimationComponent>().PostAnimationTickCount, Is.EqualTo(1));
            Assert.That(pawn.GetComponent<PawnCameraComponent>().TickCount, Is.EqualTo(1));

            world.ShutdownAsync().GetAwaiter().GetResult();

            Assert.That(pawn.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(pawn.IsRootDestroyed, Is.True);
            Assert.That(world.RegisteredActorCount, Is.Zero);
            Assert.That(World.Current, Is.Null);
        }

        [Test]
        public void MissingRequiredCamera_RollsBackActorControllerAndClone()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: false, requireCamera: true);
            World world = CreateWorld(definition);

            InvalidOperationException failure = null;
            try
            {
                world.InitializeAsync().GetAwaiter().GetResult();
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Assert.That(failure?.Message, Does.Contain("requires a Camera"));
            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(world.RegisteredActorCount, Is.Zero);
            Assert.That(world.LocalPlayer, Is.Null);
            Assert.That(world.GameMode, Is.Null);
            Assert.That(CountRuntimePawnClones(), Is.Zero);
        }

        [Test]
        public void RebuildPawn_KeepsPlayerStateAndCommitsOnlyOnePawn()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: true, requireCamera: true);
            World world = CreateWorld(definition);
            world.InitializeAsync().GetAwaiter().GetResult();
            world.StartPlay();
            DefaultGameMode gameMode = (DefaultGameMode)world.GameMode;
            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            PlayerState playerState = controller.PlayerState;
            Pawn previousPawn = gameMode.DefaultPawn;

            gameMode.SpawnDefaultPawn(controller).GetAwaiter().GetResult();

            Pawn replacement = gameMode.DefaultPawn;
            Assert.That(replacement, Is.Not.SameAs(previousPawn));
            Assert.That(replacement.State, Is.EqualTo(ActorState.Playing));
            Assert.That(previousPawn.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(previousPawn.IsRootDestroyed, Is.True);
            Assert.That(controller.PlayerState, Is.SameAs(playerState));
            Assert.That(playerState.CurrentPawn, Is.SameAs(replacement));
            Assert.That(controller.PossessedPawn, Is.SameAs(replacement));
            Assert.That(world.RegisteredActorCount, Is.EqualTo(3));
        }

        [Test]
        public void PawnTickFault_UnregistersRootAndClearsNonOwningPossession()
        {
            PawnDefinition definition = CreateDefinition(hasCamera: false, requireCamera: false);
            var faultingFactory = new FaultingPawnFactory();
            World world = CreateWorld(definition, faultingFactory);
            world.InitializeAsync().GetAwaiter().GetResult();
            world.StartPlay();
            PlayerController controller = (PlayerController)world.LocalPlayer.Controller;
            Pawn pawn = ((DefaultGameMode)world.GameMode).DefaultPawn;

            world.UpdateTick(0.02f);

            Assert.That(pawn.State, Is.EqualTo(ActorState.Unregistered));
            Assert.That(pawn.IsRootDestroyed, Is.True);
            Assert.That(controller.PossessedPawn, Is.Null);
            Assert.That(controller.PlayerState.CurrentPawn, Is.Null);
            Assert.That(world.RegisteredActorCount, Is.EqualTo(2));
        }

        private World CreateWorld(PawnDefinition definition, PawnFactory pawnFactory = null)
        {
            TestConfiguration configuration = Track(ScriptableObject.CreateInstance<TestConfiguration>());
            configuration.Definition = definition;
            configuration.Factory = pawnFactory;
            return World.Create(configuration);
        }

        private PawnDefinition CreateDefinition(bool hasCamera, bool requireCamera)
        {
            GameObject prefab = Track(new GameObject("PawnTransactionPrefab"));
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
                var root = new GameObject("Pawn:Faulting");
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
                AddTickTask("Pawn.Fault", TickGroup.TG_Gameplay, deltaTime =>
                    throw new InvalidOperationException("pawn tick fault"));
            }
        }
    }
}
