using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CGame.Animation;
using CGame.Animation.Rig;
using CGame.GameplayTags;
using CGame.InventoryEquipment;

namespace CGame.Editor
{
    public static class WeaponAssetMigrationAudit
    {
        private const string SourceRoot = "Assets/KINEMATION/ScriptableAnimationSystemDemo";
        private const string ReportPath = "E:/UnityProgram/FPS/.harness/runs/WeaponAssetMigrationAudit-20260812/source-closure.txt";

        [MenuItem("CGame/Weapons/Create Pure Presentation Prefabs")]
        public static void CreatePurePresentationPrefabs()
        {
            CreateWeaponPrefab(
                SourceRoot + "/Prefabs/AK12/AK12_Scriptable.prefab",
                SourceRoot + "/Animations/Weapons/AK12/C_AK12_Static.anim",
                "Assets/Art/Weapon/KINEMATION/AK12",
                "AK12Weapon.prefab",
                "AK12WeaponAnimator.controller");
            CreateWeaponPrefab(
                SourceRoot + "/Prefabs/Knife/Knife.prefab",
                SourceRoot + "/Animations/Locomotion/Generic/Knife/C_Knife_Static.anim",
                "Assets/Art/Weapon/KINEMATION/Knife",
                "KnifeWeapon.prefab",
                "KnifeWeaponAnimator.controller");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("CGame/Weapons/Validate Pure Presentation Prefabs")]
        public static void ValidatePurePresentationPrefabs()
        {
            string[] targets =
            {
                "Assets/Art/Weapon/KINEMATION/AK12/AK12Weapon.prefab",
                "Assets/Art/Weapon/KINEMATION/Knife/KnifeWeapon.prefab",
                "Assets/Settings/Animation/Weapons/AK12/AK12BoneProfile.asset",
                "Assets/Settings/Animation/Weapons/Knife/KnifeBoneProfile.asset",
                "Assets/Settings/Gameplay/Weapons/AK12WeaponDefinition.asset",
                "Assets/Settings/Gameplay/Weapons/KnifeWeaponDefinition.asset"
            };
            string[] forbidden = targets.SelectMany(path => AssetDatabase.GetDependencies(path, true))
                .Where(path => path.StartsWith("Assets/KINEMATION/", StringComparison.Ordinal)
                    || path.StartsWith("Assets/Art/KinemationLegacyWeaponRuntime/", StringComparison.Ordinal)
                    || path.StartsWith("Assets/ThirdParty/KinemationLegacyWeaponRuntime/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (forbidden.Length > 0)
            {
                const string targetReport = "E:/UnityProgram/FPS/.harness/runs/WeaponAssetMigrationAudit-20260812/target-forbidden-dependencies.txt";
                File.WriteAllLines(targetReport, forbidden);
                throw new InvalidOperationException("Pure weapon prefab still depends on legacy source. Report: " + targetReport);
            }
            KRig rig = AssetDatabase.LoadAssetAtPath<KRig>("Assets/Settings/Gameplay/SampleScene/KinemationVisualCharacterRig.asset");
            ValidateWeaponConfiguration("AK12", rig);
            ValidateWeaponConfiguration("Knife", rig);
            Debug.Log("Pure weapon prefab dependency validation passed.");
        }

        private static void ValidateWeaponConfiguration(string name, KRig rig)
        {
            string prefabPath = "Assets/Art/Weapon/KINEMATION/" + name + "/" + name + "Weapon.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing migrated prefab: " + prefabPath);
            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Migrated prefab contains a MonoBehaviour: " + prefabPath);
                if (instance.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Migrated prefab contains no renderers: " + prefabPath);
                if (name == "AK12" && instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                    throw new InvalidOperationException("Migrated AK12 prefab is missing its skinned main body renderer.");
                Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
                if (animators.Length != 1 || animators[0].runtimeAnimatorController == null)
                    throw new InvalidOperationException("Migrated prefab requires exactly one configured Animator: " + prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
            string animationDirectory = "Assets/Settings/Animation/Weapons/" + name + "/";
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(animationDirectory + name + "BoneProfile.asset");
            profile?.Validate(rig);
            IkMotionLayerSettings equip = AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(animationDirectory + name + "EquipIkMotion.asset");
            IkMotionLayerSettings unequip = AssetDatabase.LoadAssetAtPath<IkMotionLayerSettings>(animationDirectory + name + "UnequipIkMotion.asset");
            equip?.Validate(rig); unequip?.Validate(rig);
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Gameplay/Weapons/" + name + "WeaponDefinition.asset");
            if (definition == null || definition.Prefab != prefab || definition.ArmedProfile != profile || definition.EquipIkMotion != equip || definition.UnequipIkMotion != unequip)
                throw new InvalidOperationException("Weapon Definition has an incomplete migrated reference set: " + name);
            definition.Validate();
        }

        [MenuItem("CGame/Weapons/Create Weapon Configuration Assets")]
        public static void CreateWeaponConfigurationAssets()
        {
            KRig rig = AssetDatabase.LoadAssetAtPath<KRig>("Assets/Settings/Gameplay/SampleScene/KinemationVisualCharacterRig.asset");
            if (rig == null) throw new InvalidOperationException("Project Pawn KRig is missing.");
            CreateWeaponConfiguration("Knife", "Weapon.Knife", "Assets/Art/Weapon/KINEMATION/Knife/KnifeWeapon.prefab", "Assets/Art/Weapon/KINEMATION/Knife/KnifeWeaponAnimator_Idle.anim", rig, false);
            CreateWeaponConfiguration("AK12", "Weapon.Rifle.AK12", "Assets/Art/Weapon/KINEMATION/AK12/AK12Weapon.prefab", "Assets/Art/Weapon/KINEMATION/AK12/AK12WeaponAnimator_Idle.anim", rig, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateWeaponConfiguration(string name, string tagName, string prefabPath, string idlePath, KRig rig, bool attachHand)
        {
            string animationDirectory = "Assets/Settings/Animation/Weapons/" + name;
            string gameplayDirectory = "Assets/Settings/Gameplay/Weapons";
            EnsureFolder(animationDirectory);
            EnsureFolder(gameplayDirectory);
            string profilePath = animationDirectory + "/" + name + "BoneProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<BoneProfile>(profilePath) != null) return;

            var profile = ScriptableObject.CreateInstance<BoneProfile>();
            profile.name = name + "BoneProfile";
            AssetDatabase.CreateAsset(profile, profilePath);
            var layers = new List<AnimationLayerSettings>();
            AddLayer<PoseSamplerLayerSettings>(profile, rig, layers, name + "PoseSampler");
            if (attachHand) AddLayer<AttachHandLayerSettings>(profile, rig, layers, name + "AttachHand");
            else AddLayer<PoseOffsetLayerSettings>(profile, rig, layers, name + "PoseOffset");
            if (attachHand) AddLayer<ViewLayerSettings>(profile, rig, layers, name + "View");
            AddLayer<IkLayerSettings>(profile, rig, layers, name + "Ik");
            profile.Configure(rig, layers);

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
            if (clip == null) throw new InvalidOperationException("Migrated idle clip is missing: " + idlePath);
            var overlay = ScriptableObject.CreateInstance<AnimationClipAsset>();
            overlay.name = name + "OverlayPose";
            if (!overlay.TryInitialize(clip)) throw new InvalidOperationException("Failed to configure overlay: " + name);
            AssetDatabase.CreateAsset(overlay, animationDirectory + "/" + name + "OverlayPose.asset");
            var equipMotion = ScriptableObject.CreateInstance<IkMotionLayerSettings>();
            equipMotion.name = name + "EquipIkMotion";
            equipMotion.Configure(rig, new KRigElement(-1, "IK WeaponBone", 0), default, default, 0.15f);
            AssetDatabase.CreateAsset(equipMotion, animationDirectory + "/" + name + "EquipIkMotion.asset");
            var unequipMotion = ScriptableObject.CreateInstance<IkMotionLayerSettings>();
            unequipMotion.name = name + "UnequipIkMotion";
            unequipMotion.Configure(rig, new KRigElement(-1, "IK WeaponBone", 0), default, default, 0.15f);
            AssetDatabase.CreateAsset(unequipMotion, animationDirectory + "/" + name + "UnequipIkMotion.asset");

            if (!GameplayTag.TryCreateSerialized(tagName, out GameplayTag tag)) throw new InvalidOperationException("Invalid weapon tag: " + tagName);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Migrated prefab is missing: " + prefabPath);
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name + "WeaponDefinition";
            definition.ConfigurePresentation(tag, prefab, "Animator", overlay, profile, equipMotion, unequipMotion);
            AssetDatabase.CreateAsset(definition, gameplayDirectory + "/" + name + "WeaponDefinition.asset");
            var item = ScriptableObject.CreateInstance<WeaponItemDefinition>();
            item.name = name + "WeaponItemDefinition";
            item.Configure(definition, name == "Knife" ? 0 : 30, name == "Knife" ? 0 : 90);
            AssetDatabase.CreateAsset(item, gameplayDirectory + "/" + name + "WeaponItemDefinition.asset");
        }

        private static void AddLayer<T>(BoneProfile profile, KRig rig, List<AnimationLayerSettings> layers, string name)
            where T : AnimationLayerSettings
        {
            var layer = ScriptableObject.CreateInstance<T>();
            layer.name = name;
            layer.Configure(rig);
            AssetDatabase.AddObjectToAsset(layer, profile);
            layers.Add(layer);
        }

        private static void CreateWeaponPrefab(
            string sourcePrefabPath,
            string sourceIdlePath,
            string targetDirectory,
            string prefabName,
            string controllerName)
        {
            EnsureFolder(targetDirectory);
            var meshes = new Dictionary<Mesh, Mesh>();
            var materials = new Dictionary<Material, Material>();
            var textures = new Dictionary<Texture, Texture>();
            GameObject source = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
            try
            {
                var root = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
                root.AddComponent<Animator>().runtimeAnimatorController = CreateController(
                    sourceIdlePath, targetDirectory + "/" + controllerName);
                var transformMap = new Dictionary<Transform, Transform> { { source.transform, root.transform } };
                CloneRenderer(source.transform, root.transform, targetDirectory, meshes, materials, textures);
                CloneChildren(source.transform, root.transform, transformMap, targetDirectory, meshes, materials, textures);
                CloneSkinnedRenderers(source.transform, transformMap, targetDirectory, meshes, materials, textures);
                PrefabUtility.SaveAsPrefabAsset(root, targetDirectory + "/" + prefabName);
                UnityEngine.Object.DestroyImmediate(root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        private static void CloneChildren(
            Transform source,
            Transform destination,
            Dictionary<Transform, Transform> transformMap,
            string targetDirectory,
            Dictionary<Mesh, Mesh> meshes,
            Dictionary<Material, Material> materials,
            Dictionary<Texture, Texture> textures)
        {
            foreach (Transform child in source)
            {
                var clone = new GameObject(child.name).transform;
                clone.SetParent(destination, false);
                clone.localPosition = child.localPosition;
                clone.localRotation = child.localRotation;
                clone.localScale = child.localScale;
                transformMap.Add(child, clone);
                MeshFilter filter = child.GetComponent<MeshFilter>();
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (filter != null && renderer != null)
                {
                    var cloneFilter = clone.gameObject.AddComponent<MeshFilter>();
                    cloneFilter.sharedMesh = CloneMesh(filter.sharedMesh, targetDirectory, meshes);
                    var cloneRenderer = clone.gameObject.AddComponent<MeshRenderer>();
                    cloneRenderer.sharedMaterials = renderer.sharedMaterials
                        .Select(material => CloneMaterial(material, targetDirectory, materials, textures)).ToArray();
                    cloneRenderer.shadowCastingMode = renderer.shadowCastingMode;
                    cloneRenderer.receiveShadows = renderer.receiveShadows;
                }

                CloneChildren(child, clone, transformMap, targetDirectory, meshes, materials, textures);
            }
        }

        private static void CloneSkinnedRenderers(
            Transform sourceRoot,
            IReadOnlyDictionary<Transform, Transform> transformMap,
            string targetDirectory,
            Dictionary<Mesh, Mesh> meshes,
            Dictionary<Material, Material> materials,
            Dictionary<Texture, Texture> textures)
        {
            foreach (SkinnedMeshRenderer source in sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!transformMap.TryGetValue(source.transform, out Transform destination))
                    throw new InvalidOperationException("Missing cloned transform for skinned renderer '" + source.name + "'.");

                var clone = destination.gameObject.AddComponent<SkinnedMeshRenderer>();
                clone.sharedMesh = CloneMesh(source.sharedMesh, targetDirectory, meshes);
                clone.sharedMaterials = source.sharedMaterials
                    .Select(material => CloneMaterial(material, targetDirectory, materials, textures)).ToArray();
                clone.bones = source.bones.Select(bone => ResolveMappedBone(bone, transformMap, source.name)).ToArray();
                clone.rootBone = source.rootBone == null ? null : ResolveMappedBone(source.rootBone, transformMap, source.name);
                clone.quality = source.quality;
                clone.updateWhenOffscreen = source.updateWhenOffscreen;
                clone.skinnedMotionVectors = source.skinnedMotionVectors;
                clone.shadowCastingMode = source.shadowCastingMode;
                clone.receiveShadows = source.receiveShadows;
                clone.localBounds = source.localBounds;
            }
        }

        private static Transform ResolveMappedBone(
            Transform sourceBone,
            IReadOnlyDictionary<Transform, Transform> transformMap,
            string rendererName)
        {
            if (sourceBone == null || !transformMap.TryGetValue(sourceBone, out Transform destinationBone))
                throw new InvalidOperationException("Skinned renderer '" + rendererName + "' references a bone outside its migrated hierarchy.");

            return destinationBone;
        }

        private static void CloneRenderer(
            Transform source,
            Transform destination,
            string targetDirectory,
            Dictionary<Mesh, Mesh> meshes,
            Dictionary<Material, Material> materials,
            Dictionary<Texture, Texture> textures)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            MeshRenderer renderer = source.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return;
            var cloneFilter = destination.gameObject.AddComponent<MeshFilter>();
            cloneFilter.sharedMesh = CloneMesh(filter.sharedMesh, targetDirectory, meshes);
            var cloneRenderer = destination.gameObject.AddComponent<MeshRenderer>();
            cloneRenderer.sharedMaterials = renderer.sharedMaterials
                .Select(material => CloneMaterial(material, targetDirectory, materials, textures)).ToArray();
            cloneRenderer.shadowCastingMode = renderer.shadowCastingMode;
            cloneRenderer.receiveShadows = renderer.receiveShadows;
        }

        private static Mesh CloneMesh(Mesh source, string directory, Dictionary<Mesh, Mesh> copies)
        {
            if (source == null) return null;
            if (copies.TryGetValue(source, out Mesh existing)) return existing;
            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name;
            string assetDirectory = directory + "/Meshes";
            EnsureFolder(assetDirectory);
            string path = AssetDatabase.GenerateUniqueAssetPath(assetDirectory + "/" + copy.name + ".asset");
            AssetDatabase.CreateAsset(copy, path);
            copies.Add(source, copy);
            return copy;
        }

        private static Material CloneMaterial(Material source, string directory, Dictionary<Material, Material> copies, Dictionary<Texture, Texture> textures)
        {
            if (source == null) return null;
            if (copies.TryGetValue(source, out Material existing)) return existing;
            Material copy = new Material(source) { name = source.name };
            if (AssetDatabase.GetAssetPath(copy.shader).StartsWith("Assets/KINEMATION/", StringComparison.Ordinal))
            {
                Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
                if (fallback == null)
                {
                    throw new InvalidOperationException("URP Lit shader is unavailable for migrated material '" + source.name + "'.");
                }
                copy.shader = fallback;
            }
            foreach (string property in copy.GetTexturePropertyNames())
            {
                copy.SetTexture(property, CloneTexture(copy.GetTexture(property), directory, textures));
            }
            string assetDirectory = directory + "/Materials";
            EnsureFolder(assetDirectory);
            string path = AssetDatabase.GenerateUniqueAssetPath(assetDirectory + "/" + copy.name + ".mat");
            AssetDatabase.CreateAsset(copy, path);
            copies.Add(source, copy);
            return copy;
        }

        private static Texture CloneTexture(Texture source, string directory, Dictionary<Texture, Texture> copies)
        {
            if (source == null) return null;
            if (copies.TryGetValue(source, out Texture existing)) return existing;
            Texture copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name;
            string assetDirectory = directory + "/Textures";
            EnsureFolder(assetDirectory);
            string path = AssetDatabase.GenerateUniqueAssetPath(assetDirectory + "/" + copy.name + ".asset");
            AssetDatabase.CreateAsset(copy, path);
            copies.Add(source, copy);
            return copy;
        }

        private static AnimatorController CreateController(string sourceIdlePath, string targetPath)
        {
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourceIdlePath);
            if (source == null) throw new InvalidOperationException("Missing source idle clip: " + sourceIdlePath);
            string clipPath = Path.ChangeExtension(targetPath, null) + "_Idle.anim";
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (idle == null)
            {
                idle = UnityEngine.Object.Instantiate(source);
                idle.name = Path.GetFileNameWithoutExtension(clipPath);
                AssetDatabase.CreateAsset(idle, clipPath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(targetPath);
                controller.layers[0].stateMachine.AddState("Idle").motion = idle;
            }
            return controller;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("CGame/Weapons/Report KINEMATION Weapon Source Closure")]
        public static void WriteSourceClosureReport()
        {
            string[] roots =
            {
                SourceRoot + "/Prefabs/AK12/AK12_Scriptable.prefab",
                SourceRoot + "/Prefabs/Knife/Knife.prefab",
                SourceRoot + "/Prefabs/AK12/AngledGrip_AK12.asset",
                SourceRoot + "/Animations/Weapons/AK12/AA_AK12_Overlay.asset",
                SourceRoot + "/Animations/Weapons/Knife/AA_Knife_Pose.asset"
            };

            string[] lines = roots.SelectMany(root => AssetDatabase.GetDependencies(root, true)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => $"{root}\t{path}\t{AssetDatabase.AssetPathToGUID(path)}\t{AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName}"))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, lines);
            Debug.Log($"Weapon source closure report written: {ReportPath} ({lines.Length} rows)");
        }
    

[MenuItem("CGame/Weapons/Report AK12 Renderer Closure")]
        public static void ReportAk12RendererClosure()
        {
            GameObject source = PrefabUtility.LoadPrefabContents(SourceRoot + "/Prefabs/AK12/AK12_Scriptable.prefab");
            try
            {
                foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    Debug.Log($"AK12 source renderer: {renderer.GetType().Name} path={AnimationUtility.CalculateTransformPath(renderer.transform, source.transform)} materials={renderer.sharedMaterials.Length}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        [MenuItem("CGame/Weapons/Configure SampleScene Knife Loadout")]
        public static void ConfigureSampleSceneKnifeLoadout()
        {
            WeaponDefinition knife = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Gameplay/Weapons/KnifeWeaponDefinition.asset");
            WeaponDefinition ak12 = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Gameplay/Weapons/AK12WeaponDefinition.asset");
            WeaponItemDefinition knifeItem = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>("Assets/Settings/Gameplay/Weapons/KnifeWeaponItemDefinition.asset");
            WeaponItemDefinition ak12Item = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>("Assets/Settings/Gameplay/Weapons/AK12WeaponItemDefinition.asset");
            if (knife == null || ak12 == null || knifeItem == null || ak12Item == null) throw new InvalidOperationException("Migrated weapon configuration assets are missing.");
            
            GameBootstrap prefabBootstrap = AssetDatabase.LoadAssetAtPath<GameBootstrap>("Assets/Prefab/Gameplay/GameBootstrap.asset");
GameBootstrap bootstrap = AssetDatabase.LoadAssetAtPath<GameBootstrap>("Assets/Settings/Gameplay/SampleScene/SampleGameBootstrap.asset");
            InitialInventorySet inventory = AssetDatabase.LoadAssetAtPath<InitialInventorySet>("Assets/Settings/Gameplay/SampleScene/SampleInputInventory.asset");
            if (bootstrap == null || prefabBootstrap == null || inventory == null) throw new InvalidOperationException("SampleScene bootstrap or inventory configuration is missing.");
            
            
            prefabBootstrap.ConfigureWeaponDefinitions(knife, ak12);
            prefabBootstrap.ConfigureResourceInitialization(false);
            EditorUtility.SetDirty(prefabBootstrap);
bootstrap.ConfigureResourceInitialization(false);
bootstrap.ConfigureWeaponDefinitions(knife, ak12);
            inventory.Configure(0, knifeItem, ak12Item);
            EditorUtility.SetDirty(bootstrap);
            EditorUtility.SetDirty(inventory);
            AssetDatabase.SaveAssets();
        }


        [MenuItem("CGame/Weapons/Configure AK12 Acceptance Loadout")]
        public static void ConfigureAk12AcceptanceLoadout()
        {
            WeaponDefinition knife = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Gameplay/WeaponDefinition/Knife/KnifeWeaponDefinition.asset");
            WeaponDefinition ak12 = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset");
            WeaponItemDefinition knifeItem = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>("Assets/Settings/Gameplay/WeaponDefinition/Knife/KnifeWeaponItemDefinition.asset");
            WeaponItemDefinition ak12Item = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>("Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponItemDefinition.asset");
            GameplayTagSource tagSource = AssetDatabase.LoadAssetAtPath<GameplayTagSource>("Assets/Settings/Gameplay/SampleScene/SampleInputTagSource.asset");
            GameplayTagSource weaponTagSource = AssetDatabase.LoadAssetAtPath<GameplayTagSource>("Assets/Data/GamePlayTag/Sources/DefaultGameplayTags.asset");
            PawnDefinition pawn = AssetDatabase.LoadAssetAtPath<PawnDefinition>("Assets/Prefab/Gameplay/DefaultPawnData.asset");
            InitialInventorySet inventory = AssetDatabase.LoadAssetAtPath<InitialInventorySet>("Assets/Settings/Gameplay/WeaponGripAK12/WeaponGripAK12InitialInventory.asset");
            DefaultGameModeDefinition gameMode = AssetDatabase.LoadAssetAtPath<DefaultGameModeDefinition>("Assets/Settings/Gameplay/WeaponGripAK12/WeaponGripAK12GameMode.asset");
            GameBootstrap bootstrap = AssetDatabase.LoadAssetAtPath<GameBootstrap>("Assets/Settings/Gameplay/WeaponGripAK12/WeaponGripAK12GameBootstrap.asset");
            if (knife == null || ak12 == null || knifeItem == null || ak12Item == null || tagSource == null || weaponTagSource == null || pawn == null || inventory == null || gameMode == null || bootstrap == null) throw new InvalidOperationException("AK12 acceptance configuration assets are missing.");
            inventory.Configure(1, knifeItem, ak12Item);
            gameMode.Configure(pawn, inventory);
            bootstrap.ConfigureGameplayTagSources(tagSource, weaponTagSource);
            bootstrap.ConfigureGameMode(gameMode);
            bootstrap.ConfigureWeaponDefinitions(knife, ak12);
            bootstrap.ConfigureResourceInitialization(false);
            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(gameMode);
            EditorUtility.SetDirty(bootstrap);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("CGame/Weapons/Configure AK12 Default Ammo (1000)")]
        public static void ConfigureAk12DefaultAmmo()
        {
            ConfigureWeaponAmmo(
                "Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset",
                "Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponItemDefinition.asset");
            ConfigureWeaponAmmo(
                "Assets/Settings/Gameplay/WeaponGripAK12/LayerIntegrationTest/AK12LayerIntegrationTestWeaponDefinition.asset",
                "Assets/Settings/Gameplay/WeaponGripAK12/LayerIntegrationTest/AK12LayerIntegrationTestWeaponItemDefinition.asset");
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureWeaponAmmo(string weaponPath, string itemPath)
        {
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath);
            WeaponItemDefinition item = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(itemPath);
            if (weapon == null || item == null)
            {
                throw new InvalidOperationException($"AK12 ammo configuration assets are missing: {weaponPath}, {itemPath}.");
            }

            item.Configure(weapon, 1000, 1000);
            EditorUtility.SetDirty(item);
        }

        [MenuItem("CGame/Weapons/Register Weapon Gameplay Tags")]
        public static void RegisterWeaponGameplayTags()
        {
            GameplayTagSource source = AssetDatabase.LoadAssetAtPath<GameplayTagSource>("Assets/Settings/Gameplay/SampleScene/SampleInputTagSource.asset");
            if (source == null) throw new InvalidOperationException("SampleScene GameplayTagSource is missing.");
            AddTag(source, "Weapon.Knife");
            AddTag(source, "Weapon.Rifle.AK12");
        }

        private static void AddTag(GameplayTagSource source, string tag)
        {
            if (!CGame.GameplayTags.Editor.GameplayTagSourceMutationService.AddTagPath(source, tag, "Migrated weapon identity", out string error)
                && !error.Contains("already exists", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(error);
            }
        }
}
}
