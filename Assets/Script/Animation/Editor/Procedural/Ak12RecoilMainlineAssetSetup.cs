using System;
using System.Linq;
using CGame.Animation;
using CGame.Animation.Rig;
using CGame.Animation.Editor;
using CGame.InventoryEquipment;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.EditorTools
{
    public static class Ak12RecoilMainlineAssetSetup
    {
        private const string RecoilProfilePath = "Assets/Art/Weapon/Profile/AK12/AK12RecoilProfile.asset";

        [MenuItem("CGame/AK12/Configure Recoil Mainline Assets")]
        public static void Configure()
        {
            RecoilProfile recoilProfile = AssetDatabase.LoadAssetAtPath<RecoilProfile>(RecoilProfilePath);
            if (recoilProfile == null)
            {
                recoilProfile = ScriptableObject.CreateInstance<RecoilProfile>();
                recoilProfile.name = "AK12RecoilProfile";
                AssetDatabase.CreateAsset(recoilProfile, RecoilProfilePath);
            }

            recoilProfile.Validate();
            EditorUtility.SetDirty(recoilProfile);
            AssignWeapon("Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset", recoilProfile);
            AssignWeapon("Assets/Settings/Gameplay/WeaponGripAK12/LayerIntegrationTest/AK12LayerIntegrationTestWeaponDefinition.asset", recoilProfile);
            ConfigureWeaponProfile("Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset");
            ConfigureWeaponProfile("Assets/Settings/Gameplay/WeaponGripAK12/LayerIntegrationTest/AK12LayerIntegrationTestWeaponDefinition.asset");
            ConfigureAdditive("Assets/Art/Weapon/Profile/AK12/AK12ProceduralBoneProfile.asset");
            ConfigureAdditive("Assets/Art/Weapon/Profile/AK12/AK12BoneProfile.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AK12 recoil mainline asset setup completed.");
        }

        private static void AssignWeapon(string path, RecoilProfile recoilProfile)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition == null) return;

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("recoilProfile").objectReferenceValue = recoilProfile;
            serialized.FindProperty("fireInterval").floatValue = 0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureWeaponProfile(string path)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition?.ArmedProfile != null)
            {
                ConfigureAdditive(AssetDatabase.GetAssetPath(definition.ArmedProfile));
            }
        }

        private static void ConfigureAdditive(string path)
        {
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(path);
            if (profile == null || profile.Rig == null)
            {
                Debug.LogWarning($"AK12 additive setup skipped for {path}: profile or rig missing.");
                return;
            }

            EnsureLayer<LookLayerSettings>(profile);
            EnsureLayer<TurnLayerSettings>(profile);
            AdditiveLayerSettings additive = profile.Layers.OfType<AdditiveLayerSettings>().FirstOrDefault();
            if (additive == null)
            {
                additive = BoneProfileLayerAssetService.AddLayer(profile, typeof(AdditiveLayerSettings)) as AdditiveLayerSettings;
            }

            int additiveIndex = -1;
            for (int index = 0; index < profile.Layers.Count; index++)
            {
                if (ReferenceEquals(profile.Layers[index], additive))
                {
                    additiveIndex = index;
                    break;
                }
            }
            if (additive != null && additiveIndex != 3 && profile.Layers.Count > 3)
            {
                BoneProfileLayerAssetService.MoveLayer(profile, additiveIndex, 3);
            }

            SerializedObject serialized = new SerializedObject(additive);
            serialized.FindProperty("rig").objectReferenceValue = profile.Rig;
            SetElement(serialized.FindProperty("weaponIkBone"), profile.Rig, "IK WeaponBone");
            SetElement(serialized.FindProperty("additiveBone"), profile.Rig, "IK WeaponBoneRight");
            serialized.FindProperty("interpolationSpeed").floatValue = 12f;
            serialized.FindProperty("aimingCurve").stringValue = "AimingWeight";
            serialized.FindProperty("adsScalar").floatValue = 0.65f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(additive);
            EnsureAdditiveLayerReference(profile);
            EnsureOrderedLayers(profile);
            string profilePath = AssetDatabase.GetAssetPath(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(profilePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"AK12 additive setup configured {path} with {profile.Layers.Count} layers.");
        }

        private static void EnsureLayer<T>(BoneProfile profile) where T : AnimationLayerSettings
        {
            if (!profile.Layers.OfType<T>().Any())
            {
                T layer = ScriptableObject.CreateInstance<T>();
                layer.name = typeof(T).Name.Replace("LayerSettings", string.Empty);
                layer.Configure(profile.Rig);
                AssetDatabase.AddObjectToAsset(layer, profile);
                SerializedObject serializedProfile = new SerializedObject(profile);
                SerializedProperty layers = serializedProfile.FindProperty("layers");
                int newIndex = layers.arraySize;
                layers.arraySize++;
                layers.GetArrayElementAtIndex(newIndex).objectReferenceValue = layer;
                serializedProfile.ApplyModifiedProperties();
                EditorUtility.SetDirty(layer);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureOrderedLayers(BoneProfile profile)
        {
            Type[] order =
            {
                typeof(PoseSamplerLayerSettings),
                typeof(AttachHandLayerSettings),
                typeof(ViewLayerSettings),
                typeof(AdditiveLayerSettings),
                typeof(LookLayerSettings),
                typeof(TurnLayerSettings),
                typeof(IkLayerSettings)
            };

            for (int targetIndex = 0; targetIndex < order.Length; targetIndex++)
            {
                int currentIndex = -1;
                for (int index = targetIndex; index < profile.Layers.Count; index++)
                {
                    if (profile.Layers[index] != null && profile.Layers[index].GetType() == order[targetIndex])
                    {
                        currentIndex = index;
                        break;
                    }
                }

                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    BoneProfileLayerAssetService.MoveLayer(profile, currentIndex, targetIndex);
                }
            }
        }

        public static void EnsureAdditiveLayerReference(BoneProfile profile)
        {
            if (profile == null) return;
            AdditiveLayerSettings additive = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(profile))
                .OfType<AdditiveLayerSettings>()
                .FirstOrDefault();
            if (additive == null) return;

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            int existingIndex = -1;
            for (int index = 0; index < layers.arraySize; index++)
            {
                if (layers.GetArrayElementAtIndex(index).objectReferenceValue == additive)
                {
                    existingIndex = index;
                    break;
                }
            }

            int insertIndex = Mathf.Min(3, layers.arraySize);
            if (existingIndex >= 0)
            {
                if (existingIndex != insertIndex)
                {
                    layers.MoveArrayElement(existingIndex, insertIndex);
                    serializedProfile.ApplyModifiedProperties();
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                }
                return;
            }
            layers.arraySize++;
            layers.MoveArrayElement(layers.arraySize - 1, insertIndex);
            layers.GetArrayElementAtIndex(insertIndex).objectReferenceValue = additive;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static void SetElement(SerializedProperty property, KRig rig, string name)
        {
            KRigElement element = rig.Hierarchy.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(element.Name))
            {
                element = rig.Hierarchy.FirstOrDefault(candidate => candidate.Name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            property.FindPropertyRelative("Index").intValue = element.Index;
            property.FindPropertyRelative("Name").stringValue = element.Name;
            property.FindPropertyRelative("Depth").intValue = element.Depth;
        }
    }
}
