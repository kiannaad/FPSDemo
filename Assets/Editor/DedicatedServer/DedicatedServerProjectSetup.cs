using CGame.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

namespace CGame.Editor
{
    public static class DedicatedServerProjectSetup
    {
        public const string BootstrapScenePath = "Assets/Scenes/DedicatedServerBootstrap.unity";
        public const string GameBootstrapPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/Bootstrap/GameBootstrap.asset";

        [MenuItem("CGame/Network/Setup Dedicated Server")]
        public static void Setup()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("DedicatedServerBootstrap");
            DedicatedServerBootstrap component = bootstrap.AddComponent<DedicatedServerBootstrap>();
            WorldConfiguration gameBootstrap = AssetDatabase.LoadAssetAtPath<WorldConfiguration>(GameBootstrapPath);
            if (gameBootstrap == null) throw new System.InvalidOperationException($"Missing {GameBootstrapPath}.");
            component.Configure(gameBootstrap);

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            var light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;

            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new System.InvalidOperationException($"Unable to save {BootstrapScenePath}.");
            }

            AssetDatabase.SaveAssets();
            string[] requiredScenes = { BootstrapScenePath, DedicatedServerBuild.SampleScenePath };
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(requiredScenes.Select(path => new EditorBuildSettingsScene(path, true)))
                .GroupBy(sceneEntry => sceneEntry.path)
                .Select(group => group.Last())
                .ToArray();
            Debug.Log($"[DedicatedServer][037] Setup Scene={BootstrapScenePath}");
        }
    }
}
