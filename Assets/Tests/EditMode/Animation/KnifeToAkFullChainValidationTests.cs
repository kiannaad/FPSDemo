using System;
using System.IO;
using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class KnifeToAkFullChainValidationTests
    {
        private const string CatalogPath =
            "Assets/Resources/WeaponAnimationCatalog.asset";
        private const string KnifeDefinitionPath =
            "Assets/Resources/FistsWeaponAnimationDefinition.asset";
        private const string RifleDefinitionPath =
            "Assets/Art/Animation/Weapon/KINEMATION/AK/"
            + "RifleAKAnimationDefinition.asset";
        private const string CollectorPath =
            "Assets/AssetBundleCollectorSetting.asset";

        [Test]
        public void ProjectAssets_ExposeCompleteKnifeAndAkContracts()
        {
            WeaponAnimationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationCatalog>(
                    CatalogPath);
            WeaponAnimationDefinition knife =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    KnifeDefinitionPath);
            WeaponAnimationDefinition rifle =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    RifleDefinitionPath);

            Assert.NotNull(catalog);
            Assert.NotNull(knife);
            Assert.NotNull(rifle);
            Assert.AreEqual(
                WeaponAnimationCatalogError.None,
                catalog.Validate());
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                knife.Validate(new WeaponId("knife")));
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                rifle.Validate(new WeaponId("rifle")));
            Assert.AreEqual(
                new WeaponRuntimeCapabilities(false, false, true),
                knife.Capabilities);
            Assert.AreEqual(
                new WeaponRuntimeCapabilities(true, true, false),
                rifle.Capabilities);
            Assert.IsTrue(
                catalog.TryResolve(
                    new WeaponId("knife"),
                    out string knifeLocation));
            Assert.AreEqual(
                "FistsWeaponAnimationDefinition",
                knifeLocation);
            Assert.IsTrue(
                catalog.TryResolve(
                    new WeaponId("rifle"),
                    out string rifleLocation));
            Assert.AreEqual(
                "RifleAKAnimationDefinition",
                rifleLocation);

            AssertClip(knife.OverlayPose, true);
            AssertClip(knife.Equip, false);
            AssertClip(knife.Unequip, false);
            AssertClip(knife.MeleeAttack, false);
            AssertClip(rifle.OverlayPose, true);
            AssertClip(rifle.Equip, false);
            AssertClip(rifle.Unequip, false);
            AssertClip(rifle.Fire, false);
            AssertClip(rifle.Reload, false);
            Assert.AreNotSame(
                rifle.OverlayPose,
                rifle.Fire,
                "Fire must remain an explicit action asset, not the persistent overlay asset.");
        }

        [Test]
        public void DefinitionClosures_AreProjectOwnedAndAkIsYooAssetCollectable()
        {
            foreach (string definitionPath in new[]
                     {
                         KnifeDefinitionPath,
                         RifleDefinitionPath,
                     })
            {
                string[] forbidden = AssetDatabase
                    .GetDependencies(definitionPath, true)
                    .Where(path =>
                        path.StartsWith(
                            "Assets/KINEMATION/",
                            StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(
                            "Assets/ThirdParty/",
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Assert.That(
                    forbidden,
                    Is.Empty,
                    definitionPath);
            }

            string collector = File.ReadAllText(CollectorPath);
            StringAssert.Contains(
                "Assets/Resources/WeaponAnimationCatalog.asset",
                collector);
            StringAssert.Contains(
                "Assets/Resources/FistsWeaponAnimationDefinition.asset",
                collector);
            StringAssert.Contains(RifleDefinitionPath, collector);
            StringAssert.Contains(
                "9843548ba557c9840a45528f6b500206",
                collector);
        }

        [Test]
        public void AkDefinition_DoesNotReintroduceWeaponLocomotionOwnership()
        {
            WeaponAnimationDefinition rifle =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    RifleDefinitionPath);
            string[] declaredFields = typeof(WeaponAnimationDefinition)
                .GetFields(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(declaredFields, "idle");
            CollectionAssert.DoesNotContain(declaredFields, "walk");
            CollectionAssert.DoesNotContain(declaredFields, "run");
            CollectionAssert.DoesNotContain(declaredFields, "sprint");
            CollectionAssert.DoesNotContain(declaredFields, "jump");
            CollectionAssert.DoesNotContain(declaredFields, "land");
            Assert.IsTrue(
                AssetDatabase.GetAssetPath(rifle.OverlayPose)
                    .EndsWith(
                        "RifleAKIdleClipAsset.asset",
                        StringComparison.Ordinal));
        }

        private static void AssertClip(
            AnimationClipAsset asset,
            bool shouldLoop)
        {
            Assert.NotNull(asset);
            Assert.IsTrue(asset.TryValidateForPlayback(out string error), error);
            Assert.NotNull(asset.AnimationClip);
            Assert.IsFalse(asset.AnimationClip.legacy);
            Assert.AreEqual(
                shouldLoop,
                asset.AnimationClip.isLooping,
                asset.name);
        }
    }
}
