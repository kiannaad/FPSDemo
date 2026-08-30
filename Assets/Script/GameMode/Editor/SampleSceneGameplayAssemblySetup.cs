using CGame;
using CGame.Ability.Attributes;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using CGame.InventoryEquipment;
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
        private const string PlayerStatePath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/DefaultPlayerStateDefinition.asset";
        private const string HealthSetPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/HealthAttributeSetDefinition.asset";
        private const string CombatSetPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/CombatAttributeSetDefinition.asset";
        private const string PlayerInitializationEffectPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/GE_Player_Initialization.asset";
        private const string EnemyInitializationEffectPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/GE_TargetEnemy_Initialization.asset";
        private const string PlayerAbilityInitializationPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/PlayerAbilitySystemInitialization.asset";
        private const string EnemyAbilityInitializationPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/TargetEnemyAbilitySystemInitialization.asset";
        private const string DamageEffectFolder =
            "Assets/Settings/Gameplay/Weapon/GameplayEffects";
        private const string DamageEffectPath =
            DamageEffectFolder + "/GE_Damage_Instant.asset";
        private const string Ak12WeaponDefinitionPath =
            "Assets/Settings/Gameplay/Weapon/WeaponDefinition/AK12/AK12WeaponDefinition.asset";
        private const string Mk18WeaponDefinitionPath =
            "Assets/Settings/Gameplay/Weapon/WeaponGripAK12/MK18/MK18WeaponDefinition.asset";

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

        [MenuItem("CGame/Setup/Configure SampleScene Ability System Initialization")]
        public static void ConfigureAbilitySystemInitialization()
        {
            AttributeSetDefinition healthSet = LoadOrCreate<AttributeSetDefinition>(HealthSetPath);
            healthSet.Configure(AttributeSetKind.Health);
            EditorUtility.SetDirty(healthSet);
            AttributeSetDefinition combatSet = LoadOrCreate<AttributeSetDefinition>(CombatSetPath);
            combatSet.Configure(AttributeSetKind.Combat);
            EditorUtility.SetDirty(combatSet);

            GameplayEffectDefinition playerEffect = LoadOrCreate<GameplayEffectDefinition>(PlayerInitializationEffectPath);
            playerEffect.Configure(
                GameplayEffectDurationPolicy.Instant,
                GameplayTag.Empty,
                GameplayEffectModifierDefinition.Constant(HealthSet.MaxHealthAttribute, GameplayEffectModifierOperation.Override, 100f),
                GameplayEffectModifierDefinition.Constant(HealthSet.HealthAttribute, GameplayEffectModifierOperation.Override, 100f),
                GameplayEffectModifierDefinition.Constant(CombatSet.BaseDamageAttribute, GameplayEffectModifierOperation.Override, 20f));
            EditorUtility.SetDirty(playerEffect);

            GameplayEffectDefinition enemyEffect = LoadOrCreate<GameplayEffectDefinition>(EnemyInitializationEffectPath);
            enemyEffect.Configure(
                GameplayEffectDurationPolicy.Instant,
                GameplayTag.Empty,
                GameplayEffectModifierDefinition.Constant(HealthSet.MaxHealthAttribute, GameplayEffectModifierOperation.Override, 60f),
                GameplayEffectModifierDefinition.Constant(HealthSet.HealthAttribute, GameplayEffectModifierOperation.Override, 60f),
                GameplayEffectModifierDefinition.Constant(CombatSet.BaseDamageAttribute, GameplayEffectModifierOperation.Override, 10f));
            EditorUtility.SetDirty(enemyEffect);

            AbilitySystemInitializationDefinition playerInitialization =
                LoadOrCreate<AbilitySystemInitializationDefinition>(PlayerAbilityInitializationPath);
            playerInitialization.Configure(new[] { healthSet, combatSet }, new[] { playerEffect });
            EditorUtility.SetDirty(playerInitialization);
            AbilitySystemInitializationDefinition enemyInitialization =
                LoadOrCreate<AbilitySystemInitializationDefinition>(EnemyAbilityInitializationPath);
            enemyInitialization.Configure(new[] { healthSet, combatSet }, new[] { enemyEffect });
            EditorUtility.SetDirty(enemyInitialization);

            PlayerStateDefinition playerState = AssetDatabase.LoadAssetAtPath<PlayerStateDefinition>(PlayerStatePath);
            if (playerState == null) throw new System.InvalidOperationException($"Missing PlayerStateDefinition at {PlayerStatePath}.");
            playerState.SetAbilitySystemInitialization(playerInitialization);
            EditorUtility.SetDirty(playerState);
            EnemyPlayerStateDefinition enemyState = AssetDatabase.LoadAssetAtPath<EnemyPlayerStateDefinition>(EnemyStatePath);
            if (enemyState == null || enemyState.PawnPrefab == null)
                throw new System.InvalidOperationException($"Missing configured EnemyPlayerStateDefinition at {EnemyStatePath}.");
            enemyState.Configure(enemyState.PawnPrefab, enemyInitialization);
            EditorUtility.SetDirty(enemyState);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("CGame/Setup/Configure Formal Weapon Damage Effect")]
        public static void ConfigureFormalWeaponDamageEffect()
        {
            if (!AssetDatabase.IsValidFolder(DamageEffectFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings/Gameplay/Weapon", "GameplayEffects");
            }

            if (!GameplayTag.TryCreateSerialized(
                    "GameplayCue.Weapon.DamageTaken",
                    out GameplayTag damageTakenTag))
            {
                throw new System.InvalidOperationException("DamageTaken GameplayCue tag is invalid.");
            }

            GameplayTagSource tagSource = AssetDatabase.LoadAssetAtPath<GameplayTagSource>(
                "Assets/Settings/Gameplay/SampleScene/InputConfig/SampleInputTagSource.asset");
            if (tagSource == null)
            {
                throw new System.InvalidOperationException("SampleScene GameplayTagSource is missing.");
            }

            if (!CGame.GameplayTags.Editor.GameplayTagSourceMutationService.AddTagPath(
                    tagSource,
                    damageTakenTag.Name,
                    "Formal weapon damage cue",
                    out string tagError) &&
                !tagError.Contains("already exists", System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException(tagError);
            }

            GameplayEffectDefinition damageEffect = LoadOrCreate<GameplayEffectDefinition>(DamageEffectPath);
            damageEffect.Configure(
                GameplayEffectDurationPolicy.Instant,
                damageTakenTag,
                GameplayEffectModifierDefinition.SourceAttribute(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    CombatSet.BaseDamageAttribute));
            EditorUtility.SetDirty(damageEffect);
            ConfigureDamageTakenCue(damageTakenTag);
            ConfigureWeaponDamageEffect(Ak12WeaponDefinitionPath, damageEffect);
            ConfigureWeaponDamageEffect(Mk18WeaponDefinitionPath, damageEffect);
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

        private static void ConfigureWeaponDamageEffect(
            string weaponPath,
            GameplayEffectDefinition damageEffect)
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath);
            if (weapon == null)
            {
                throw new System.InvalidOperationException($"Missing WeaponDefinition at {weaponPath}.");
            }

            weapon.ConfigureDamageEffect(damageEffect);
            EditorUtility.SetDirty(weapon);
        }

        private static void ConfigureDamageTakenCue(GameplayTag damageTakenTag)
        {
            GameplayCueSet cueSet = AssetDatabase.LoadAssetAtPath<GameplayCueSet>(
                "Assets/Settings/Gameplay/Cues/WeaponGameplayCueSet.asset");
            CueNotifyDefinition notify = AssetDatabase.LoadAssetAtPath<CueNotifyDefinition>(
                "Assets/Settings/Gameplay/Cues/DebugParticleCueNotify.asset");
            if (cueSet == null || notify == null)
            {
                throw new System.InvalidOperationException("Formal Weapon GameplayCue assets are missing.");
            }

            var entries = new System.Collections.Generic.List<GameplayCueSetEntry>(cueSet.Entries);
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null && entries[index].CueTag == damageTakenTag)
                {
                    entries[index].SetDefinition(damageTakenTag, notify);
                    cueSet.SetDefinition(cueSet.Priority, entries);
                    EditorUtility.SetDirty(cueSet);
                    return;
                }
            }

            var damageTakenEntry = new GameplayCueSetEntry();
            damageTakenEntry.SetDefinition(damageTakenTag, notify);
            entries.Add(damageTakenEntry);
            cueSet.SetDefinition(cueSet.Priority, entries);
            EditorUtility.SetDirty(cueSet);
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
