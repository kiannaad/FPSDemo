using CGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CGame.Editor
{
    public static class SampleSceneGameplayAssemblySetup
    {
        private const string BootstrapPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/GameBootstrap.asset";
        private const string ExperiencePath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/DefaultExperienceDefinition.asset";
        private const string FeaturePath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/DefaultCoreGameFeature.asset";
        private const string EnemyStatePath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/TargetEnemyPlayerStateDefinition.asset";
        private const string EnemyDefinitionPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/TargetEnemyDefinition.asset";
        private const string EnemyActionPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/TargetEnemySpawnFeatureAction.asset";

        [MenuItem("CGame/Setup/Enable SampleScene Gameplay Resources")]
        public static void EnableResources()
        {
            GameBootstrap bootstrap = AssetDatabase.LoadAssetAtPath<GameBootstrap>(BootstrapPath);
            if (bootstrap == null) throw new System.InvalidOperationException($"Missing GameBootstrap at {BootstrapPath}.");
            Undo.RecordObject(bootstrap, "Enable SampleScene Gameplay Resources");
            bootstrap.ConfigureResourceInitialization(true);
            EditorUtility.SetDirty(bootstrap);

            GameFeatureConfig feature = AssetDatabase.LoadAssetAtPath<GameFeatureConfig>(FeaturePath);
            if (feature == null)
            {
                feature = UnityEngine.ScriptableObject.CreateInstance<GameFeatureConfig>();
                AssetDatabase.CreateAsset(feature, FeaturePath);
            }
            EnemySpawnGameFeatureAction enemyAction = ConfigureEnemyAssets();
            feature.Configure("SampleScene.Core", null, new[] { "DefaultExperienceDefinition" }, enemyAction);
            EditorUtility.SetDirty(feature);

            ExperienceDefinition experience = AssetDatabase.LoadAssetAtPath<ExperienceDefinition>(ExperiencePath);
            if (experience == null) throw new System.InvalidOperationException($"Missing ExperienceDefinition at {ExperiencePath}.");
            Undo.RecordObject(experience, "Configure SampleScene Core Feature");
            experience.Configure(feature);
            EditorUtility.SetDirty(experience);
            EnsurePlayerPoint();
            EnsureEnemyPoints();
            AssetDatabase.SaveAssets();
        }

        private static EnemySpawnGameFeatureAction ConfigureEnemyAssets()
        {
            ConfigureTargetPrefab();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Character/NPC/Target.prefab");
            if (prefab == null) throw new System.InvalidOperationException("Missing Target prefab.");
            EnemyPlayerStateDefinition state = LoadOrCreate<EnemyPlayerStateDefinition>(EnemyStatePath);
            state.Configure(prefab, 100f);
            EditorUtility.SetDirty(state);
            EnemyDefinition definition = LoadOrCreate<EnemyDefinition>(EnemyDefinitionPath);
            definition.Configure(state);
            EditorUtility.SetDirty(definition);
            EnemySpawnGameFeatureAction action = LoadOrCreate<EnemySpawnGameFeatureAction>(EnemyActionPath);
            action.Configure(definition, 3);
            EditorUtility.SetDirty(action);
            return action;
        }

        private static void ConfigureTargetPrefab()
        {
            const string path = "Assets/Art/Character/NPC/Target.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Collider rootCollider = root.GetComponent<Collider>();
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                for (int index = 0; index < colliders.Length; index++)
                {
                    if (!ReferenceEquals(colliders[index], rootCollider)) colliders[index].isTrigger = true;
                }
                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                for (int index = 0; index < cameras.Length; index++) cameras[index].gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsurePlayerPoint()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity")
                throw new System.InvalidOperationException("Load SampleScene before running gameplay setup.");
            LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(
                "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/DefaultLevelDefinition.asset");
            GameObject root = System.Array.Find(scene.GetRootGameObjects(), item => item.name == LevelDefinitionScanner.PlayerContainerName);
            if (root == null || root.transform.childCount == 0)
            {
                LevelDefinitionScanner.AddPointAndScan(scene, definition, SpawnPointKind.Player);
                root = System.Array.Find(scene.GetRootGameObjects(), item => item.name == LevelDefinitionScanner.PlayerContainerName);
                GameObject legacyStart = GameObject.Find("PlayerStart");
                if (legacyStart != null)
                {
                    Undo.RecordObject(root.transform.GetChild(0), "Migrate PlayerStart Pose");
                    root.transform.GetChild(0).SetPositionAndRotation(
                        legacyStart.transform.position,
                        legacyStart.transform.rotation);
                }
            }
            LevelDefinitionScanner.ScanAndApply(scene, definition);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureEnemyPoints()
        {
            var scene = EditorSceneManager.GetActiveScene();
            LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(
                "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/DefaultLevelDefinition.asset");
            GameObject root = System.Array.Find(scene.GetRootGameObjects(), item => item.name == LevelDefinitionScanner.EnemyContainerName);
            while (root == null || root.transform.childCount < 3)
            {
                LevelDefinitionScanner.AddPointAndScan(scene, definition, SpawnPointKind.Enemy);
                root = System.Array.Find(scene.GetRootGameObjects(), item => item.name == LevelDefinitionScanner.EnemyContainerName);
            }
            Vector3[] positions = { new Vector3(-3f, 1f, 2.5f), new Vector3(0f, 1f, 2.5f), new Vector3(3f, 1f, 2.5f) };
            for (int index = 0; index < 3; index++)
            {
                Undo.RecordObject(root.transform.GetChild(index), "Configure Enemy Spawn Point");
                root.transform.GetChild(index).SetPositionAndRotation(positions[index], Quaternion.Euler(0f, 180f, 0f));
            }
            LevelDefinitionScanner.ScanAndApply(scene, definition);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
