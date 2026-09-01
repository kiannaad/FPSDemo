using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CGame.GameplayCue.PlayModeTests
{
    public sealed class ExperienceGatedPlayerSpawnPlayModeTests
    {
        [UnityTest]
        public IEnumerator LoadingExperience_DoesNotCreatePlayerOrGameModeUntilReady()
        {
            if (World.Current != null) yield return WaitForTask(World.Current.ShutdownAsync());
            var configuration = UnityEngine.ScriptableObject.CreateInstance<GateConfiguration>();
            World world = World.Create(configuration);
            Task initialization = world.InitializeAsync();
            yield return null;

            Assert.That(world.GameState, Is.Not.Null);
            Assert.That(world.LocalPlayer, Is.Null);
            Assert.That(world.GameMode, Is.Null);
            Assert.That(configuration.GameModeCreateCount, Is.Zero);

            configuration.GameState.Complete();
            yield return WaitForTask(initialization);
            Assert.That(world.LocalPlayer, Is.Not.Null);
            Assert.That(configuration.GameModeCreateCount, Is.EqualTo(1));
            yield return WaitForTask(world.ShutdownAsync());
            UnityEngine.Object.DestroyImmediate(configuration);
        }

        [UnityTest]
        public IEnumerator FailedExperience_LeavesNoPlayerGameModeOrGameplayReady()
        {
            if (World.Current != null) yield return WaitForTask(World.Current.ShutdownAsync());
            var configuration = UnityEngine.ScriptableObject.CreateInstance<GateConfiguration>();
            configuration.Fail = true;
            World world = World.Create(configuration);
            Task initialization = world.InitializeAsync();
            yield return null;
            configuration.GameState.Complete();
            while (!initialization.IsCompleted) yield return null;

            Assert.That(initialization.IsFaulted, Is.True);
            Assert.That(world.State, Is.EqualTo(WorldState.Faulted));
            Assert.That(world.LocalPlayer, Is.Null);
            Assert.That(world.GameMode, Is.Null);
            Assert.That(world.IsGameplayReady, Is.False);
            Assert.That(configuration.GameModeCreateCount, Is.Zero);
            yield return WaitForTask(world.ShutdownAsync());
            UnityEngine.Object.DestroyImmediate(configuration);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.GetBaseException();
        }

        private sealed class GateConfiguration : WorldConfiguration
        {
            public readonly GateGameState GameState = new GateGameState();
            public bool Fail;
            public int GameModeCreateCount;
            public override IReadOnlyList<WorldSubSystem> CreateWorldSubSystems() => System.Array.Empty<WorldSubSystem>();
            public override GameState CreateGameState(World world)
            {
                GameState.Fail = Fail;
                return GameState;
            }
            public override GameMode CreateGameMode(World world, Player player)
            {
                GameModeCreateCount++;
                return null;
            }
        }

        private sealed class GateGameState : GameState
        {
            private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            public bool Fail;
            public void Complete()
            {
                if (Fail) completion.SetException(new System.InvalidOperationException("experience failed"));
                else completion.SetResult(true);
            }
            public override Task LoadExperienceAsync() => completion.Task;
            public override Task ShutdownExperienceAsync() => Task.CompletedTask;
        }
    }
}
