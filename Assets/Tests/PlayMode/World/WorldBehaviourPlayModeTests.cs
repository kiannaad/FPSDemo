using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace CGame.WorldRuntime.PlayMode.Tests
{
    public sealed class WorldBehaviourPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            WorldBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<WorldBehaviour>();
            foreach (WorldBehaviour behaviour in behaviours)
            {
                UnityEngine.Object.Destroy(behaviour.gameObject);
            }

            yield return null;
            if (World.Current != null)
            {
                Task shutdown = World.Current.ShutdownAsync();
                while (!shutdown.IsCompleted)
                {
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator SampleScene_StartsWorldAndPlayerPresentation()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            WorldBehaviour behaviour = UnityEngine.Object.FindObjectOfType<WorldBehaviour>();
            Assert.That(behaviour, Is.Not.Null);
            Assert.That(behaviour.InitializationTask, Is.Not.Null);
            while (!behaviour.InitializationTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(behaviour.InitializationTask.Result.Succeeded, Is.True,
                behaviour.InitializationTask.Result.Error);
            Assert.That(behaviour.RuntimeWorld.State, Is.EqualTo(WorldState.Running));
            Assert.That(behaviour.PlayerCamera, Is.Not.Null);
            Assert.That(GameObject.Find("PawnCandidate:SamplePawnData"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator WorldBehaviour_IsTheOnlyDriverAndProducesRequestWithoutGameplayObjects()
        {
            GameObject worldObject = new GameObject("World Driver");
            WorldBehaviour behaviour = worldObject.AddComponent<WorldBehaviour>();
            Task<WorldStartResult> startTask = behaviour.StartWorld(
                Array.Empty<IWorldCoreService>(),
                new GameLauncher(new Func<ILaunchStep>[] { () => new ImmediateStep() }));

            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.Result.Succeeded, Is.True);
            Assert.That(behaviour.RuntimeWorld.State, Is.EqualTo(WorldState.Launching));
            Assert.That(UnityEngine.Object.FindObjectsOfType<WorldBehaviour>().Length, Is.EqualTo(1));
            Assert.That(GameObject.Find("[GameManager]"), Is.Null);

            GameObject duplicateObject = new GameObject("Duplicate World Driver");
            WorldBehaviour duplicate = duplicateObject.AddComponent<WorldBehaviour>();
            Assert.Throws<InvalidOperationException>(() => duplicate.StartWorld(
                Array.Empty<IWorldCoreService>(),
                new GameLauncher(Array.Empty<Func<ILaunchStep>>())));
        }

        private sealed class ImmediateStep : ILaunchStep
        {
            public string Name => "Immediate";

            public Task<LaunchStepResult> ExecuteAsync(LaunchContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(LaunchStepResult.Success());
            }

            public Task ExitAsync(LaunchContext context)
            {
                return Task.CompletedTask;
            }
        }
    }
}
