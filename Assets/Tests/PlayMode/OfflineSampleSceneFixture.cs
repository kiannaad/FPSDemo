using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace CGame.GameplayCue.PlayModeTests
{
    // Owns only an in-memory offline configuration for legacy local gameplay contracts.
    // Network mainline tests must continue to use the untouched scene configuration.
    internal sealed class OfflineSampleSceneFixture : IDisposable
    {
        private GameInstance instance;
        private WorldConfiguration originalConfiguration;
        private GameBootstrap configuration;
        private UnityEditor.EditorWindow gameWindow;

        public IEnumerator Start(GameInstance gameInstance)
        {
            instance = gameInstance;
            Assert.That(instance, Is.Not.Null);
            originalConfiguration = instance.Configuration;
            yield return WaitFor(() => instance.RuntimeWorld?.State == WorldState.Initialized ||
                instance.InitializationTask?.IsCompleted == true, "Initial resource initialization");
            if (instance.InitializationTask?.IsFaulted == true)
                throw instance.InitializationTask.Exception.GetBaseException();
            World previous = instance.RuntimeWorld;
            instance.ShutdownRuntimeWorld();
            var shutdown = previous.ShutdownAsync();
            yield return WaitFor(() => shutdown.IsCompleted, "Previous world shutdown");
            if (shutdown.IsFaulted) throw shutdown.Exception.GetBaseException();

            configuration = UnityEngine.Object.Instantiate((GameBootstrap)originalConfiguration);
            configuration.ConfigureClientNetwork(null);
            SetConfiguration(configuration);
            instance.EnsureRuntimeWorldStarted();
            yield return WaitFor(() => instance.InitializationTask.IsCompleted, "Offline gameplay startup");
            if (instance.InitializationTask.IsFaulted) throw instance.InitializationTask.Exception.GetBaseException();
            Assert.That(instance.RuntimeWorld.GameMode, Is.TypeOf<DefaultGameMode>());
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            gameWindow = ScriptableObject.CreateInstance(gameViewType) as UnityEditor.EditorWindow;
            gameWindow.titleContent = new GUIContent("Offline Gameplay Verification");
            gameWindow.position = new Rect(100, 100, 1280, 760);
            gameWindow.ShowUtility();
            gameWindow.Focus();
            yield return WaitFor(() => Application.isFocused, "Game View input focus");
        }

        public void Dispose()
        {
            if (instance != null)
            {
                instance.ShutdownRuntimeWorld();
                SetConfiguration(originalConfiguration);
            }
            if (configuration != null) UnityEngine.Object.Destroy(configuration);
            if (gameWindow != null) gameWindow.Close();
        }

        private void SetConfiguration(WorldConfiguration value)
        {
            var serialized = new UnityEditor.SerializedObject(instance);
            serialized.FindProperty("worldConfiguration").objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator WaitFor(Func<bool> condition, string stage)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, stage + " timed out.");
        }
    }
}
