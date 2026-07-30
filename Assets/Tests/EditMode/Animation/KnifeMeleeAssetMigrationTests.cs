using System;
using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class KnifeMeleeAssetMigrationTests
    {
        private const string SourceFolder =
            "Assets/KINEMATION/ScriptableAnimationSystemDemo/Meshes/Weapons/Knife";
        private const string TargetFolder =
            "Assets/Art/Animation/Weapon/KINEMATION/Knife";
        private const string WeaponModelPath =
            "Assets/Art/Weapon/KINEMATION/Knife/KnifeWeapon.fbx";
        private const string UpperBodyMaskPath =
            "Assets/Art/KinemationLegacyWeaponRuntime/ScriptableAnimationSystemDemo/Animations/Masks/UpperBody.mask";

        [TestCase("C_Knife_Pose.anim", 0.016666667f)]
        [TestCase("C_Knife_Attack.anim", 1.4333334f)]
        public void MigratedClip_IsProjectOwnedAndSelfContained(
            string fileName,
            float expectedLength)
        {
            string sourcePath = $"{SourceFolder}/{fileName}";
            string targetPath = $"{TargetFolder}/{fileName}";
            Assert.NotNull(AssetDatabase.LoadMainAssetAtPath(targetPath));
            Assert.AreNotEqual(
                AssetDatabase.AssetPathToGUID(sourcePath),
                AssetDatabase.AssetPathToGUID(targetPath));

            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            Assert.NotNull(clip);
            Assert.IsFalse(clip.legacy);
            Assert.AreEqual(expectedLength, clip.length, 0.0001f);

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
        public void WeaponModel_MatchesKnifeSourceAndHasNoThirdPartyDependency()
        {
            GameObject weaponModel =
                AssetDatabase.LoadAssetAtPath<GameObject>(WeaponModelPath);
            Assert.NotNull(weaponModel);
            Assert.AreNotEqual(
                AssetDatabase.AssetPathToGUID($"{SourceFolder}/AK_bayonet.fbx"),
                AssetDatabase.AssetPathToGUID(WeaponModelPath));

            string[] forbiddenDependencies = AssetDatabase
                .GetDependencies(WeaponModelPath, true)
                .Where(IsThirdPartyDependency)
                .ToArray();
            Assert.That(
                forbiddenDependencies,
                Is.Empty,
                $"Unexpected third-party dependencies: {string.Join(", ", forbiddenDependencies)}");
        }

        [Test]
        public void ClipAssets_UseKnifePoseAndAttackWithoutFistsReferences()
        {
            AvatarMask upperBodyMask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            Assert.NotNull(upperBodyMask);

            AssertClipAsset(
                "KnifeOverlayPoseClipAsset.asset",
                "C_Knife_Pose",
                0f,
                0f,
                null,
                null);
            AssertClipAsset(
                "KnifeEquipClipAsset.asset",
                "C_Knife_Pose",
                0f,
                0f,
                null,
                upperBodyMask);
            AssertClipAsset(
                "KnifeUnequipClipAsset.asset",
                "C_Knife_Pose",
                0f,
                0f,
                null,
                upperBodyMask);
            AnimationClipAsset melee = AssertClipAsset(
                "KnifeMeleeAttackClipAsset.asset",
                "C_Knife_Attack",
                0.1f,
                0.1f,
                upperBodyMask,
                null);

            Assert.IsTrue(melee.TryGetNamedCurve(
                "WeaponBone",
                out AnimationCurve weaponBone));
            Assert.IsTrue(melee.TryGetNamedCurve(
                "MaskLeftHand",
                out AnimationCurve maskLeftHand));
            Assert.AreEqual(0f, weaponBone.Evaluate(0.5f), 0.0001f);
            Assert.AreEqual(1f, maskLeftHand.Evaluate(0.5f), 0.0001f);
        }

        [Test]
        public void ObsoleteFistsTargetFolder_IsDeleted()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(
                "Assets/Art/Animation/Weapon/KINEMATION/Fists"));
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
