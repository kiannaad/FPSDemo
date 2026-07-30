using System;
using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class FistsMeleeAssetMigrationTests
    {
        private const string SourceFolder =
            "Assets/KINEMATION/ScriptableAnimationSystemDemo/Prefabs/Fists";
        private const string TargetFolder =
            "Assets/Art/Animation/Weapon/KINEMATION/Fists";
        private const string UpperBodyMaskPath =
            "Assets/Art/KinemationLegacyWeaponRuntime/ScriptableAnimationSystemDemo/Animations/Masks/UpperBody.mask";

        [TestCase("Fists_Idle.fbx", true)]
        [TestCase("Fists_Equip.fbx", false)]
        [TestCase("Fists_UnEquip.fbx", false)]
        [TestCase("Fists_Punch.fbx", false)]
        public void MigratedFbx_IsProjectOwnedAndSelfContained(
            string fileName,
            bool expectedLoopTime)
        {
            string sourcePath = $"{SourceFolder}/{fileName}";
            string targetPath = $"{TargetFolder}/{fileName}";
            Assert.NotNull(AssetDatabase.LoadMainAssetAtPath(targetPath));
            Assert.AreNotEqual(
                AssetDatabase.AssetPathToGUID(sourcePath),
                AssetDatabase.AssetPathToGUID(targetPath));

            var importer = AssetImporter.GetAtPath(targetPath) as ModelImporter;
            Assert.NotNull(importer);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(
                ModelImporterAvatarSetup.CreateFromThisModel,
                importer.avatarSetup);
            Assert.IsNull(importer.sourceAvatar);
            Assert.That(importer.clipAnimations, Has.Length.EqualTo(1));
            Assert.AreEqual(expectedLoopTime, importer.clipAnimations[0].loopTime);

            string[] forbiddenDependencies = AssetDatabase
                .GetDependencies(targetPath, true)
                .Where(IsThirdPartyDependency)
                .ToArray();
            Assert.That(
                forbiddenDependencies,
                Is.Empty,
                $"Unexpected third-party dependencies: {string.Join(", ", forbiddenDependencies)}");
        }

        [Test]
        public void ClipAssets_PreserveAuthoredSemanticsWithoutThirdPartyRuntimeReferences()
        {
            AvatarMask upperBodyMask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            Assert.NotNull(upperBodyMask);

            AssertClipAsset(
                "FistsOverlayPoseClipAsset.asset",
                "Fists_Idle",
                0.15f,
                0.15f,
                null,
                null);
            AssertClipAsset(
                "FistsEquipClipAsset.asset",
                "Fists_Equip",
                0.15f,
                0.15f,
                null,
                upperBodyMask);
            AssertClipAsset(
                "FistsUnequipClipAsset.asset",
                "Fists_UnEquip",
                0.15f,
                0.15f,
                null,
                upperBodyMask);
            AnimationClipAsset melee = AssertClipAsset(
                "FistsMeleeAttackClipAsset.asset",
                "Fists_Punch",
                0.2f,
                0.3f,
                upperBodyMask,
                null);

            Assert.IsTrue(melee.TryGetNamedCurve(
                "PelvisYawOffset",
                out AnimationCurve curve));
            Assert.NotNull(curve);
            Assert.That(curve.keys, Has.Length.EqualTo(5));
            Assert.AreEqual(0f, curve.Evaluate(0f), 0.0001f);
            Assert.Greater(curve.Evaluate(0.426239f), 1f);
            Assert.AreEqual(0f, curve.Evaluate(0.728386f), 0.0001f);
        }

        [Test]
        public void SourceDemoPrefab_IsNotAProjectWeaponPrefabCandidate()
        {
            GameObject sourcePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceFolder}/Fists.prefab");
            Assert.NotNull(sourcePrefab);
            string[] componentTypes = sourcePrefab
                .GetComponentsInChildren<Component>(true)
                .Where(component => component != null)
                .Select(component => component.GetType().FullName)
                .ToArray();

            Assert.Contains(
                "KINEMATION.FPSAnimationFramework.Runtime.Core.FPSAnimatorEntity",
                componentTypes);
            Assert.Contains("Demo.Scripts.Runtime.Item.MeleeWeapon", componentTypes);
        }

        private static AnimationClipAsset AssertClipAsset(
            string assetName,
            string expectedClipName,
            float expectedBlendIn,
            float expectedBlendOut,
            AvatarMask expectedMask,
            AvatarMask expectedOverrideMask)
        {
            string assetPath = $"{TargetFolder}/{assetName}";
            AnimationClipAsset asset =
                AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(assetPath);
            Assert.NotNull(asset, assetPath);
            Assert.IsTrue(asset.TryValidateForPlayback(out string error), error);
            Assert.AreEqual(expectedClipName, asset.AnimationClip.name);
            Assert.AreEqual(expectedBlendIn, asset.BlendInTime, 0.0001f);
            Assert.AreEqual(expectedBlendOut, asset.BlendOutTime, 0.0001f);
            Assert.AreEqual(1f, asset.Speed, 0.0001f);
            Assert.AreEqual(expectedMask, asset.Mask);
            Assert.AreEqual(expectedOverrideMask, asset.OverrideMask);
            Assert.IsFalse(asset.Additive);
            Assert.IsTrue(
                AssetDatabase.GetAssetPath(asset.AnimationClip)
                    .StartsWith(TargetFolder, StringComparison.Ordinal));

            string[] forbiddenDependencies = AssetDatabase
                .GetDependencies(assetPath, true)
                .Where(IsThirdPartyDependency)
                .ToArray();
            Assert.That(
                forbiddenDependencies,
                Is.Empty,
                $"Unexpected third-party dependencies: {string.Join(", ", forbiddenDependencies)}");
            return asset;
        }

        private static bool IsThirdPartyDependency(string path)
        {
            return path.StartsWith(
                    "Assets/KINEMATION/",
                    StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(
                    "Assets/ThirdParty/",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
