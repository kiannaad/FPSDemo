using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using CGame.Animation.Rig;
using CGame.GameplayTags;
using CGame.InventoryEquipment;
using UnityEditor;
using UnityEngine;

namespace CGame.Editor
{
    public static class Mk18KnifeAssetMigration
    {
        private const string Mk18Source = "Assets/KINEMATION/ScriptableAnimationSystemDemo/AnimatorProfiles/AnimatorProfile_Mk18.asset";
        private const string KnifeSource = "Assets/KINEMATION/ScriptableAnimationSystemDemo/AnimatorProfiles/AnimatorProfile_Knife.asset";
        private const string RigPath = "Assets/Settings/Gameplay/SampleScene/PawnConfig/KinemationVisualCharacterRig.asset";
        private const string Mk18ProfilePath = "Assets/Settings/Gameplay/Weapon/WeaponProfile/MK18/MK18BoneProfile.asset";
        private const string Ak12ProfilePath = "Assets/Settings/Gameplay/Weapon/WeaponProfile/AK12/AK12ProceduralBoneProfile.asset";

        [MenuItem("CGame/Weapons/Migrate MK18 And Complete Knife")]
        public static void Migrate()
        {
            KRig rig = Require<KRig>(RigPath);
            EnsureFolder("Assets/Art/Weapon/MK18");
            EnsureFolder("Assets/Art/Animation/Weapons/Knife");
            EnsureFolder("Assets/Settings/Gameplay/Weapon/WeaponProfile/MK18");
            EnsureFolder("Assets/Settings/Gameplay/Weapon/WeaponDefinition/MK18");
            CreateMk18Prefab();

            AnimationClip mk18Pose = CopySourcePoseClip(Mk18Source, "Assets/Art/Animation/Weapons/Mk18/C_MK18_DemoOverlayPose.anim");
            AnimationClip knifePose = CopySourcePoseClip(KnifeSource, "Assets/Art/Animation/Weapons/Knife/C_Knife_DemoPose.anim");
            BoneProfile mk18 = BuildProfile(Mk18ProfilePath, Mk18Source, "MK18", mk18Pose, rig, true);
            BoneProfile knife = BuildProfile("Assets/Settings/Gameplay/Weapon/WeaponProfile/Knife/KnifeBoneProfile.asset", KnifeSource, "Knife", knifePose, rig, false);
            CopyProfileBlend(mk18);

            AnimationClipAsset overlay = LoadOrCreateOverlay("Assets/Settings/Gameplay/Weapon/WeaponProfile/MK18/MK18OverlayPose.asset", "MK18OverlayPose", mk18Pose);
            LoadOrCreateOverlay("Assets/Settings/Gameplay/Weapon/WeaponProfile/Knife/C_Knife_PoseClipAsset.asset", "C_Knife_PoseClipAsset", knifePose);
            WeaponDefinition ak12 = Require<WeaponDefinition>("Assets/Settings/Gameplay/Weapon/WeaponDefinition/AK12/AK12WeaponDefinition.asset");
            WeaponDefinition mk18Definition = LoadOrClone(ak12, "Assets/Settings/Gameplay/Weapon/WeaponDefinition/MK18/MK18WeaponDefinition.asset", "MK18WeaponDefinition");
            GameplayTag.TryCreateSerialized("Weapon.Rifle.MK18", out GameplayTag mk18Tag);
            RegisterMk18Tag();
            mk18Definition.ConfigurePresentation(mk18Tag, Require<GameObject>("Assets/Art/Weapon/MK18/MK18Weapon.prefab"), "Animator", overlay, mk18, ak12.EquipIkMotion, ak12.UnequipIkMotion);
            EditorUtility.SetDirty(mk18Definition);

            WeaponItemDefinition mk18Item = LoadOrCreate<WeaponItemDefinition>("Assets/Settings/Gameplay/Weapon/WeaponDefinition/MK18/MK18WeaponItemDefinition.asset", "MK18WeaponItemDefinition");
            mk18Item.Configure(mk18Definition, 1000, 1000);
            EditorUtility.SetDirty(mk18Item);
            ConfigureSampleScene(ak12, mk18Definition, mk18Item);

            mk18.Validate(rig);
            knife.Validate(rig);
            mk18Definition.Validate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"MK18/Knife migration completed. MK18={LayerNames(mk18)} Knife={LayerNames(knife)}");
        }

        private static BoneProfile BuildProfile(string path, string sourcePath, string prefix, AnimationClip pose, KRig rig, bool rifle)
        {
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BoneProfile>();
                profile.name = prefix + "BoneProfile";
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (UnityEngine.Object child in AssetDatabase.LoadAllAssetsAtPath(path).Where(item => item != profile).ToArray())
                UnityEngine.Object.DestroyImmediate(child, true);

            var layers = new List<AnimationLayerSettings> { CreateLayer<PoseSamplerLayerSettings>(profile, rig, sourcePath, pose) };
            if (rifle)
            {
                layers.Add(CreateLayer<AttachHandLayerSettings>(profile, rig, sourcePath));
                layers.Add(CreateLayer<ViewLayerSettings>(profile, rig, sourcePath));
                layers.Add(CreateLayer<AdsLayerSettings>(profile, rig, sourcePath));
            }
            layers.Add(CreateLayer<IkMotionLayerSettings>(profile, rig, sourcePath));
            layers.Add(CreateLayer<AdditiveLayerSettings>(profile, rig, sourcePath));
            if (rifle)
            {
                layers.Add(CreateLookLayer(profile, rig, sourcePath));
                BoneProfile cameraSafeProfile = Require<BoneProfile>(Ak12ProfilePath);
                layers.Add(CloneProjectLayer<TurnLayerSettings>(profile, rig, cameraSafeProfile));
            }
            else
            {
                BoneProfile cameraSafeProfile = Require<BoneProfile>(Ak12ProfilePath);
                layers.Add(CloneProjectLayer<LookLayerSettings>(profile, rig, cameraSafeProfile));
                layers.Add(CloneProjectLayer<TurnLayerSettings>(profile, rig, cameraSafeProfile));
            }
            layers.Add(CreateLayer<IkLayerSettings>(profile, rig, sourcePath));
            profile.Configure(rig, layers);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T CreateLayer<T>(BoneProfile owner, KRig rig, string sourcePath, AnimationClip pose = null) where T : AnimationLayerSettings
        {
            ScriptableObject source = SourceLayer(sourcePath, typeof(T).Name);
            T target = ScriptableObject.CreateInstance<T>();
            EditorJsonUtility.FromJsonOverwrite(TransformJson(source), target);
            target.name = typeof(T).Name.Replace("LayerSettings", string.Empty);
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
                if (iterator.name == "Index" && iterator.propertyType == SerializedPropertyType.Integer) iterator.intValue = -1;
            if (pose != null) serialized.FindProperty("referencePose").objectReferenceValue = pose;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            target.Configure(rig);
            AssetDatabase.AddObjectToAsset(target, owner);
            return target;
        }

        private static T CloneProjectLayer<T>(BoneProfile owner, KRig rig, BoneProfile sourceProfile) where T : AnimationLayerSettings
        {
            T source = sourceProfile.Layers.OfType<T>().Single();
            T target = UnityEngine.Object.Instantiate(source);
            target.name = source.name;
            target.Configure(rig);
            AssetDatabase.AddObjectToAsset(target, owner);
            return target;
        }

        private static LookLayerSettings CreateLookLayer(BoneProfile owner, KRig rig, string sourcePath)
        {
            var source = new SerializedObject(SourceLayer(sourcePath, nameof(LookLayerSettings)));
            var target = ScriptableObject.CreateInstance<LookLayerSettings>();
            target.name = "Look";
            var destination = new SerializedObject(target);
            CopyBase(source, destination);
            destination.FindProperty("useTurnOffset").boolValue = source.FindProperty("useTurnOffset").boolValue;
            CopyLookElements(source, destination, rig, "pitchOffsetElements", "pitchElements");
            CopyLookElements(source, destination, rig, "yawOffsetElements", "yawElements");
            CopyLookElements(source, destination, rig, "rollOffsetElements", "rollElements");
            destination.ApplyModifiedPropertiesWithoutUndo();
            target.Configure(rig);
            AssetDatabase.AddObjectToAsset(target, owner);
            return target;
        }

        private static void CopyLookElements(SerializedObject source, SerializedObject destination, KRig rig, string sourceName, string targetName)
        {
            SerializedProperty from = source.FindProperty(sourceName);
            SerializedProperty to = destination.FindProperty(targetName);
            to.arraySize = from.arraySize;
            for (int index = 0; index < from.arraySize; index++)
            {
                SerializedProperty sourceEntry = from.GetArrayElementAtIndex(index);
                SerializedProperty targetEntry = to.GetArrayElementAtIndex(index);
                SerializedProperty sourceElement = sourceEntry.FindPropertyRelative("rigElement");
                SerializedProperty targetElement = targetEntry.FindPropertyRelative("Element");
                string boneName = sourceElement.FindPropertyRelative("name").stringValue;
                int expectedDepth = sourceElement.FindPropertyRelative("depth").intValue + 1;
                KRigElement resolved = rig.Hierarchy.Single(element => element.Name == boneName && element.Depth == expectedDepth);
                targetElement.FindPropertyRelative("Index").intValue = resolved.Index;
                targetElement.FindPropertyRelative("Name").stringValue = resolved.Name;
                targetElement.FindPropertyRelative("Depth").intValue = resolved.Depth;
                targetEntry.FindPropertyRelative("AngleLimits").vector2Value = sourceEntry.FindPropertyRelative("clampedAngle").vector2Value;
            }
        }

        private static void CopyBase(SerializedObject source, SerializedObject destination)
        {
            destination.FindProperty("alpha").floatValue = source.FindProperty("alpha").floatValue;
            destination.FindProperty("linkDynamically").boolValue = source.FindProperty("linkDynamically").boolValue;
            SerializedProperty from = source.FindProperty("curveBlending");
            SerializedProperty to = destination.FindProperty("curveBlending");
            to.arraySize = from.arraySize;
            for (int index = 0; index < from.arraySize; index++)
            {
                SerializedProperty sourceEntry = from.GetArrayElementAtIndex(index);
                SerializedProperty targetEntry = to.GetArrayElementAtIndex(index);
                targetEntry.FindPropertyRelative("curveName").stringValue = sourceEntry.FindPropertyRelative("name").stringValue;
                targetEntry.FindPropertyRelative("mode").enumValueIndex = sourceEntry.FindPropertyRelative("mode").enumValueIndex;
                targetEntry.FindPropertyRelative("clampMinimum").floatValue = sourceEntry.FindPropertyRelative("clampMin").floatValue;
                targetEntry.FindPropertyRelative("source").enumValueIndex = sourceEntry.FindPropertyRelative("source").enumValueIndex;
            }
        }

        private static string TransformJson(UnityEngine.Object source)
        {
            string json = EditorJsonUtility.ToJson(source);
            foreach ((string from, string to) in new[] { ("rigAsset", "rig"), ("clampMin", "clampMinimum"), ("poseToSample", "referencePose"), ("ikHandRightHint", "ikRightHandHint"), ("ikHandLeftHint", "ikLeftHandHint"), ("ikHandRight", "ikRightHand"), ("ikHandLeft", "ikLeftHand"), ("ikHandGun", "ikWeaponBone"), ("boneToAnimate", "targetBone"), ("interpSpeed", "interpolationSpeed"), ("aimingInputProperty", "aimingCurve") })
                json = json.Replace($"\"{from}\":", $"\"{to}\":");
            foreach ((string from, string to) in new[] { ("position", "Position"), ("rotation", "Rotation"), ("scale", "Scale"), ("index", "Index"), ("name", "Name"), ("depth", "Depth") })
                json = json.Replace($"\"{from}\":", $"\"{to}\":");
            return json.Replace("\"Name\":\"FullBodyWeight\"", "\"curveName\":\"FullBodyWeight\"").Replace("\"Name\":\"MaskAttachHand\"", "\"curveName\":\"MaskAttachHand\"");
        }

        private static void CopyProfileBlend(BoneProfile profile)
        {
            var source = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(Mk18Source));
            var destination = new SerializedObject(profile);
            destination.FindProperty("blendIn").floatValue = source.FindProperty("blendInTime").floatValue;
            destination.FindProperty("blendOut").floatValue = source.FindProperty("blendOutTime").floatValue;
            destination.FindProperty("easeMode").enumValueIndex = (int)EaseMode.EaseInOut;
            destination.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationClip CopySourcePoseClip(string sourceProfilePath, string targetPath)
        {
            ScriptableObject poseSampler = SourceLayer(sourceProfilePath, nameof(PoseSamplerLayerSettings));
            UnityEngine.Object sourceAnimationAsset = new SerializedObject(poseSampler).FindProperty("poseToSample").objectReferenceValue;
            AnimationClip sourceClip = new SerializedObject(sourceAnimationAsset).FindProperty("clip").objectReferenceValue as AnimationClip;
            if (sourceClip == null) throw new InvalidOperationException("Missing source pose clip in " + sourceProfilePath);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath) == null
                && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), targetPath))
            {
                throw new InvalidOperationException("Failed to migrate source pose clip to " + targetPath);
            }
            return Require<AnimationClip>(targetPath);
        }

        private static void CreateMk18Prefab()
        {
            const string sourcePath = "Assets/KINEMATION/ScriptableAnimationSystemDemo/Prefabs/Mk18/Mk18_Scriptable.prefab";
            const string targetPath = "Assets/Art/Weapon/MK18/MK18Weapon.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null)
            {
                MethodInfo method = typeof(WeaponAssetMigrationAudit).GetMethod("CreateWeaponPrefab", BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, new object[] { sourcePath, "Assets/Art/Animation/Weapons/Mk18/C_Mk18_Static.anim", "Assets/Art/Weapon/MK18", "MK18Weapon.prefab", "MK18WeaponAnimator.controller" });
            }
            SyncActiveStates(sourcePath, targetPath);
        }

        private static void SyncActiveStates(string sourcePath, string targetPath)
        {
            GameObject source = PrefabUtility.LoadPrefabContents(sourcePath);
            GameObject target = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                SyncActiveStates(source.transform, target.transform);
                PrefabUtility.SaveAsPrefabAsset(target, targetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
                PrefabUtility.UnloadPrefabContents(target);
            }
        }

        private static void SyncActiveStates(Transform source, Transform target)
        {
            target.gameObject.SetActive(source.gameObject.activeSelf);
            if (source.childCount != target.childCount)
                throw new InvalidOperationException($"MK18 prefab hierarchy mismatch at {AnimationUtility.CalculateTransformPath(source, source.root)}: source={source.childCount} target={target.childCount}");

            for (int index = 0; index < source.childCount; index++)
                SyncActiveStates(source.GetChild(index), target.GetChild(index));
        }

        private static void ConfigureSampleScene(WeaponDefinition ak12, WeaponDefinition mk18, WeaponItemDefinition mk18Item)
        {
            InitialInventorySet inventory = Require<InitialInventorySet>("Assets/Settings/Gameplay/SampleScene/InputConfig/SampleInputInventory.asset");
            var items = inventory.ItemDefinitions.Take(2).ToList();
            items.Add(mk18Item);
            inventory.Configure(inventory.SelectedSlot, items.ToArray());
            EditorUtility.SetDirty(inventory);
            WeaponDefinition knife = Require<WeaponDefinition>("Assets/Settings/Gameplay/Weapon/WeaponDefinition/Knife/KnifeWeaponDefinition.asset");
            foreach (string path in new[] { "Assets/Settings/Gameplay/SampleScene/SampleGameBootstrap.asset", "Assets/Prefab/Gameplay/GameBootstrap.asset" })
            {
                GameBootstrap bootstrap = AssetDatabase.LoadAssetAtPath<GameBootstrap>(path);
                if (bootstrap == null) continue;
                bootstrap.ConfigureWeaponDefinitions(knife, ak12, mk18);
                EditorUtility.SetDirty(bootstrap);
            }
        }

        private static void RegisterMk18Tag()
        {
            GameplayTagSource source = Require<GameplayTagSource>("Assets/Data/GamePlayTag/Sources/DefaultGameplayTags.asset");
            if (!CGame.GameplayTags.Editor.GameplayTagSourceMutationService.AddTagPath(source, "Weapon.Rifle.MK18", "MK18 weapon identity", out string error)
                && !error.Contains("already exists", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(error);
            }
            EditorUtility.SetDirty(source);
        }

        private static AnimationClipAsset LoadOrCreateOverlay(string path, string name, AnimationClip clip)
        {
            AnimationClipAsset asset = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                asset.name = name;
                AssetDatabase.CreateAsset(asset, path);
            }
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("animationClip").objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T LoadOrCreate<T>(string path, string name) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static WeaponDefinition LoadOrClone(WeaponDefinition source, string path, string name)
        {
            WeaponDefinition asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (asset != null) return asset;
            asset = UnityEngine.Object.Instantiate(source);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ScriptableObject SourceLayer(string path, string typeName) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>().First(asset => asset.GetType().Name == typeName && !asset.GetType().FullName.StartsWith("CGame.", StringComparison.Ordinal));
        private static T Require<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
        private static string LayerNames(BoneProfile profile) => string.Join(" > ", profile.Layers.Select(layer => layer.GetType().Name));

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
