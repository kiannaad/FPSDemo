using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGame.Animation.Editor
{
    internal static class ProceduralLayerLabAssetBuilder
    {
        private const string MenuPath = "Tools/CGame/Tests/Build Procedural Layer Lab";
        private const string SourcePrefabPath = "Assets/Art/Character/KinemationVisualCharacter.prefab";
        private const string HumanoidReferencePrefabPath =
            "Assets/KINEMATION/ScriptableAnimationSystemDemo/Prefabs/Humanoid/FPSHumanoidPlayer.prefab";
        private const string LabRoot = "Assets/Tests/PlayMode/ProceduralLayerLab";
        private const string LabFolder = LabRoot + "/Resources/ProceduralLayerLab";
        private const string PrefabPath = LabFolder + "/ProceduralLayerLabCharacter.prefab";
        private const string AvatarPath = LabFolder + "/ProceduralLayerLabAvatar.asset";
        private const string RigPath = LabFolder + "/ProceduralLayerLabRig.asset";
        private const string ProfilePath = LabFolder + "/ProceduralLayerLabProfile.asset";
        private const string ScenePath = LabRoot + "/ProceduralLayerLab.unity";

        [MenuItem(MenuPath)]
        private static void Build()
        {
            EnsureFolder("Assets/Tests", "PlayMode");
            EnsureFolder("Assets/Tests/PlayMode", "ProceduralLayerLab");
            EnsureFolder(LabRoot, "Resources");
            EnsureFolder(LabRoot + "/Resources", "ProceduralLayerLab");
            RequireAsset<GameObject>(SourcePrefabPath);
            RequireAsset<GameObject>(HumanoidReferencePrefabPath);

            DeleteAssetIfPresent(LabRoot + "/ProceduralLayerLabProfile.asset");
            DeleteAssetIfPresent(LabRoot + "/ProceduralLayerLabRig.asset");
            DeleteAssetIfPresent(LabRoot + "/ProceduralLayerLabAvatar.asset");
            DeleteAssetIfPresent(LabRoot + "/ProceduralLayerLabCharacter.prefab");

            DeleteAssetIfPresent(ScenePath);
            DeleteAssetIfPresent(ProfilePath);
            DeleteAssetIfPresent(RigPath);
            DeleteAssetIfPresent(AvatarPath);
            DeleteAssetIfPresent(PrefabPath);

            if (!AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
            {
                throw new InvalidOperationException("Unable to copy the procedural lab character prefab.");
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                prefabRoot.name = "ProceduralLayerLabCharacter";
                Animator animator = prefabRoot.GetComponentInChildren<Animator>(true)
                    ?? throw new InvalidOperationException("The lab character requires an Animator.");
                KRigComponent rigComponent = prefabRoot.GetComponentInChildren<KRigComponent>(true)
                    ?? throw new InvalidOperationException("The lab character requires a KRigComponent.");
                NormalizeVirtualRigElementNames(prefabRoot);
                animator.avatar = BuildLabAvatar(prefabRoot);
                StripGameplayBehaviours(prefabRoot, rigComponent);
                rigComponent.RefreshHierarchy();
                if (prefabRoot.GetComponentsInChildren<KVirtualElement>(true).Length == 0)
                {
                    throw new InvalidOperationException("The lab character requires virtual rig elements.");
                }

                if (!animator.isHuman)
                {
                    throw new InvalidOperationException("The lab character Animator must use a humanoid Avatar.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            GameObject prefab = RequireAsset<GameObject>(PrefabPath);
            GameObject rigSource = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                KRigComponent rigComponent = rigSource.GetComponentInChildren<KRigComponent>(true);
                rigComponent.RefreshHierarchy();
                KRig rig = ScriptableObject.CreateInstance<KRig>();
                rig.name = "ProceduralLayerLabRig";
                rig.Import(rigComponent);
                AssetDatabase.CreateAsset(rig, RigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigSource);
            }

            KRig labRig = RequireAsset<KRig>(RigPath);
            BoneProfile profile = ScriptableObject.CreateInstance<BoneProfile>();
            profile.name = "ProceduralLayerLabProfile";
            ViewLayerSettings settings = ScriptableObject.CreateInstance<ViewLayerSettings>();
            settings.name = "ProceduralLayerLabViewSettings";
            settings.Configure(labRig);
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.AddObjectToAsset(settings, profile);
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("rig").objectReferenceValue = labRig;
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            layers.arraySize = 1;
            layers.GetArrayElementAtIndex(0).objectReferenceValue = settings;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(settings);

            Scene previousActive = SceneManager.GetActiveScene();
            Scene labScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(labScene);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, labScene);
                instance.name = "ProceduralLayerLabCharacter";
                if (!EditorSceneManager.SaveScene(labScene, ScenePath))
                {
                    throw new InvalidOperationException("Unable to save the procedural layer lab scene.");
                }
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                EditorSceneManager.CloseScene(labScene, true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Procedural Layer Lab assets built under " + LabRoot + ".");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException("Required asset is missing: " + path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("Unable to replace procedural lab asset: " + path);
            }
        }

        private static void StripGameplayBehaviours(
            GameObject prefabRoot,
            KRigComponent rigComponent)
        {
            MonoBehaviour[] behaviours = prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = behaviours.Length - 1; index >= 0; index--)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == rigComponent || behaviour is KVirtualElement)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
        }

        private static Avatar BuildLabAvatar(GameObject prefabRoot)
        {
            GameObject referencePrefab = RequireAsset<GameObject>(HumanoidReferencePrefabPath);
            Animator referenceAnimator = referencePrefab.GetComponentInChildren<Animator>(true);
            if (referenceAnimator == null || referenceAnimator.avatar == null
                || !referenceAnimator.avatar.isHuman)
            {
                throw new InvalidOperationException("The humanoid reference Avatar is invalid.");
            }

            HumanDescription humanDescription = referenceAnimator.avatar.humanDescription;
            MakeHumanoidBoneNamesUnique(prefabRoot, humanDescription);
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(
                prefabRoot,
                humanDescription);
            avatar.name = "ProceduralLayerLabAvatar";
            if (!avatar.isValid || !avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                throw new InvalidOperationException(
                    "Unable to build an isolated Humanoid Avatar for the lab character.");
            }

            AssetDatabase.CreateAsset(avatar, AvatarPath);
            return avatar;
        }

        private static void MakeHumanoidBoneNamesUnique(
            GameObject prefabRoot,
            HumanDescription humanDescription)
        {
            Transform skeletonRoot = prefabRoot.transform.Find("Skeleton");
            if (skeletonRoot == null)
            {
                throw new InvalidOperationException(
                    "The lab character requires the expected Skeleton hierarchy.");
            }

            var humanoidNames = new HashSet<string>(StringComparer.Ordinal);
            HumanBone[] humanBones = humanDescription.human;
            for (int index = 0; index < humanBones.Length; index++)
            {
                humanoidNames.Add(humanBones[index].boneName);
            }

            Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate == skeletonRoot || candidate.IsChildOf(skeletonRoot)
                    || !humanoidNames.Contains(candidate.name))
                {
                    continue;
                }

                candidate.name = "Lab_" + candidate.name;
            }
        }

        private static void NormalizeVirtualRigElementNames(GameObject prefabRoot)
        {
            Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate.name.StartsWith("Virtual_IK ", StringComparison.Ordinal))
                {
                    candidate.name = candidate.name.Substring("Virtual_".Length);
                }
            }
        }
    }
}
