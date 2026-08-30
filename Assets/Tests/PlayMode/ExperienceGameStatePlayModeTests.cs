using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class ExperienceGameStatePlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_FormalBootstrapCreatesMandatoryGameStateAndReachesReadyOnce()
        {
            if (World.Current != null)
            {
                yield return WaitForTask(World.Current.ShutdownAsync());
            }

#if UNITY_EDITOR
            AsyncOperation load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/SampleScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
#endif
            while (!load.isDone) yield return null;

            GameInstance gameInstance = Object.FindObjectOfType<GameInstance>();
            Assert.That(gameInstance, Is.Not.Null);
            yield return WaitForTask(gameInstance.InitializationTask);

            World world = gameInstance.RuntimeWorld;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.State, Is.EqualTo(WorldState.Playing));
            Assert.That(world.IsGameplayReady, Is.True);
            Assert.That(world.GameplayReadyPublishCount, Is.EqualTo(1));
            Assert.That(world.GameplayReadyPublishedWorldState, Is.EqualTo(WorldState.Playing));
            Assert.That(world.GameplayReadyPublishedPawnState, Is.EqualTo(ActorState.Playing));
            Assert.That(world.GameState, Is.TypeOf<DefaultGameState>());
            DefaultGameState gameState = (DefaultGameState)world.GameState;
            Assert.That(gameState.Components.Count, Is.EqualTo(1));
            Assert.That(gameState.ExperienceManager, Is.SameAs(gameState.GetComponent<ExperienceManagerComponent>()));
            Assert.That(gameState.ExperienceManager.LoadState, Is.EqualTo(ExperienceLoadState.Ready));
            Assert.That(gameState.ExperienceManager.ReadyWriteCount, Is.EqualTo(1));
            Assert.That(gameState.ExperienceManager.FailedWriteCount, Is.EqualTo(0));
            Assert.That(world.GetSubSystem<AssetManager>(), Is.Not.Null, "Formal bootstrap must initialize the YooAsset-backed AssetManager.");
            Assert.That(gameState.ExperienceManager.Components.TryGet(out EnemySpawnGameComponent enemySpawns), Is.True);
            yield return WaitForTask(enemySpawns.SpawnTask);
            Assert.That(enemySpawns.Handles.Count, Is.EqualTo(3));
            var pointIds = new HashSet<string>();
            var registrationIds = new HashSet<Guid>();
            var initialEnemyHeights = new List<float>();
            for (int index = 0; index < enemySpawns.Handles.Count; index++)
            {
                EnemySpawnHandle handle = enemySpawns.Handles[index];
                pointIds.Add(handle.PointId);
                registrationIds.Add(handle.RegistrationId);
                initialEnemyHeights.Add(handle.Pawn.Transform.position.y);
                Assert.That(handle.Controller.State, Is.EqualTo(ActorState.Playing));
                Assert.That(handle.Pawn.State, Is.EqualTo(ActorState.Playing));
                Assert.That(handle.Pawn.PeekingMovementInput(), Is.EqualTo(Vector3.zero));
                Assert.That(handle.Pawn.Components.Count, Is.EqualTo(2));
                Assert.That(handle.Pawn.TryGetComponent(out PawnMovementComponent _), Is.True);
                Assert.That(handle.Pawn.TryGetComponent(out HealthDeathComponent _), Is.True);
                Assert.That(handle.Pawn.TryGetComponent(out PawnHeroComponent _), Is.False);
                Assert.That(handle.Pawn.TryGetComponent(out PawnAnimationComponent _), Is.False);
            }
            Assert.That(pointIds.Count, Is.EqualTo(3));
            Assert.That(registrationIds.Count, Is.EqualTo(3));
            for (int frame = 0; frame < 60; frame++) yield return null;
            for (int index = 0; index < enemySpawns.Handles.Count; index++)
            {
                EnemySpawnHandle handle = enemySpawns.Handles[index];
                CharacterPhysicsMotor motor = handle.Pawn.GetComponent<PawnMovementComponent>().Motor;
                Assert.That(handle.Pawn.Transform.position.y, Is.LessThan(initialEnemyHeights[index]));
                Assert.That(motor.GroundingStatus.IsStableOnGround, Is.True);
                Assert.That(Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).magnitude, Is.LessThan(0.01f));
                Animator animator = handle.Pawn.Root.GetComponentInChildren<Animator>(true);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).loop, Is.True);
            }
            DefaultGameMode gameMode = world.GameMode as DefaultGameMode;
            Assert.That(gameMode, Is.Not.Null);
            Assert.That(gameMode.PlayerController, Is.TypeOf<PlayerController>());
            PlayerController playerController = (PlayerController)gameMode.PlayerController;
            Assert.That(playerController.PlayerState, Is.Not.Null);
            Assert.That(playerController.PlayerStateDefinition, Is.SameAs(gameMode.PlayerStateDefinition));
            Assert.That(playerController.PossessedPawn, Is.SameAs(gameMode.DefaultPawn));
            Assert.That(gameMode.DefaultPawn.State, Is.EqualTo(ActorState.Playing));
            SpawnPointStatus playerPoint = world.LevelRuntime.GetStatus(gameMode.OccupiedPlayerPointId);
            Assert.That(playerPoint.State, Is.EqualTo(SpawnPointState.Occupied));
            Assert.That(playerPoint.RegistrationId, Is.EqualTo(gameMode.DefaultPawnRegistration.RegistrationId));

            world.UnregisterActor(gameMode.DefaultPawnRegistration);
            Assert.That(playerPoint.PointId, Is.Not.Empty);
            Assert.That(world.LevelRuntime.GetStatus(playerPoint.PointId).State, Is.EqualTo(SpawnPointState.Available));
            Assert.That(playerController.PossessedPawn, Is.Null);

            EnemySpawnHandle deathHandle = enemySpawns.Handles[0];
            string deathPoint = deathHandle.PointId;
            deathHandle.Pawn.GetComponent<HealthDeathComponent>().ApplyDamage(1000f);
            yield return null;
            Assert.That(world.LevelRuntime.GetStatus(deathPoint).State, Is.EqualTo(SpawnPointState.Available));
            Assert.That(deathHandle.IsDisposed, Is.True);

            EnemySpawnHandle unregisterHandle = enemySpawns.Handles[1];
            HealthDeathComponent lateHealth = unregisterHandle.Pawn.GetComponent<HealthDeathComponent>();
            string unregisterPoint = unregisterHandle.PointId;
            world.UnregisterActor(unregisterHandle.PawnRegistration);
            lateHealth.ApplyDamage(1000f);
            Assert.That(world.LevelRuntime.GetStatus(unregisterPoint).State, Is.EqualTo(SpawnPointState.Available));

            Object.Destroy(gameInstance.gameObject);
            yield return null;
            yield return null;
            Assert.That(World.Current, Is.Null);
        }

        [UnityTest]
        public IEnumerator ShutdownDuringLoad_WaitsThenSkipsActionAndReleasesHandle()
        {
            if (World.Current != null) yield return WaitForTask(World.Current.ShutdownAsync());
            World world = World.Create();
            var trace = new List<string>();
            var loader = new DelayedLoader(trace);
            TraceAction action = ScriptableObject.CreateInstance<TraceAction>();
            action.Trace = trace;
            GameFeatureConfig feature = ScriptableObject.CreateInstance<GameFeatureConfig>();
            feature.Configure("Delayed", null, new[] { "delayed-asset" }, action);
            ExperienceDefinition experience = ScriptableObject.CreateInstance<ExperienceDefinition>();
            experience.Configure(feature);
            var manager = new ExperienceManagerComponent(world, experience, loader);

            Task loadTask = manager.LoadAsync();
            Task shutdownTask = manager.ShutdownAsync();
            yield return null;
            Assert.That(shutdownTask.IsCompleted, Is.False);
            loader.Complete();
            yield return WaitForTask(shutdownTask);
            yield return WaitForTask(loadTask);

            CollectionAssert.AreEqual(new[] { "+delayed-asset", "-delayed-asset" }, trace);
            Assert.That(manager.LoadState, Is.EqualTo(ExperienceLoadState.Shutdown));
            Assert.That(manager.ReadyWriteCount, Is.Zero);
            Assert.That(manager.FailedWriteCount, Is.Zero);
            yield return WaitForTask(world.ShutdownAsync());
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(experience);
        }

        private static IEnumerator WaitForTask(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.GetBaseException();
        }

        private sealed class TraceAction : GameFeatureAction
        {
            public List<string> Trace;
            public override GameFeatureActivationReceipt Activate(GameFeatureActivationContext context)
            {
                Trace.Add("+action");
                return new GameFeatureActivationReceipt(context.OwnerId, () => Trace.Add("-action"));
            }
        }

        private sealed class DelayedLoader : IExperienceAssetLoader
        {
            private readonly List<string> trace;
            private readonly TaskCompletionSource<IDisposable> completion = new TaskCompletionSource<IDisposable>();
            public DelayedLoader(List<string> trace) { this.trace = trace; }
            public Task<IDisposable> LoadAsync(IReadOnlyList<string> locations)
            {
                trace.Add("+" + locations[0]);
                return completion.Task;
            }
            public void Complete() => completion.SetResult(new Lease(() => trace.Add("-delayed-asset")));
        }

        private sealed class Lease : IDisposable
        {
            private Action release;
            public Lease(Action release) { this.release = release; }
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }
    }
}
