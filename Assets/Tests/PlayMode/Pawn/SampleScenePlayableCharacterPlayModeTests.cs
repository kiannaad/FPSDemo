using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.Pawn.PlayMode.Tests
{
    public sealed class SampleScenePlayableCharacterPlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_UsesTheProductionGameInstanceAndStartsAPlayingWorld()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return new WaitUntil(() => load.isDone);
            yield return null;

            GameInstance gameInstance = Object.FindFirstObjectByType<GameInstance>();
            Assert.That(gameInstance, Is.Not.Null, "SampleScene must use the production GameInstance.");
            Assert.That(gameInstance.InitializationTask, Is.Not.Null);
            yield return new WaitUntil(() => gameInstance.InitializationTask.IsCompleted);
            Assert.That(gameInstance.InitializationTask.IsFaulted, Is.False);
            Assert.That(gameInstance.RuntimeWorld, Is.Not.Null);
            Assert.That(gameInstance.RuntimeWorld.State, Is.EqualTo(WorldState.Playing));
        }


    }
}
