using CGame;
using UnityEditor;
using UnityEngine;

namespace CGame.Editor
{
    [InitializeOnLoad]
    internal static class GameInstancePlayModeLifecycle
    {
        static GameInstancePlayModeLifecycle()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                foreach (GameInstance instance in Resources.FindObjectsOfTypeAll<GameInstance>())
                {
                    if (instance.gameObject.scene.isLoaded) instance.EnsureRuntimeWorldStarted();
                }
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                foreach (GameInstance instance in Resources.FindObjectsOfTypeAll<GameInstance>())
                {
                    if (instance.gameObject.scene.isLoaded) instance.ShutdownRuntimeWorld();
                }
            }
        }
    }
}
