using System;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class WeaponResourcesAndInitialWeaponTests
    {
        private const string KnifeDefinitionPath =
            "Assets/Resources/KnifeWeaponAnimationDefinition.asset";
        private const string RifleDefinitionPath =
            "Assets/Art/Animation/Weapon/KINEMATION/AK/"
            + "RifleAKAnimationDefinition.asset";

        [Test]
        public void ProjectAssets_MatchCodeMaintainedLocations()
        {
            var resolver =
                new WeaponAnimationDefinitionLocationResolver();
            WeaponAnimationDefinition knife =
                AssetDatabase.LoadAssetAtPath<
                    WeaponAnimationDefinition>(
                    KnifeDefinitionPath);
            WeaponAnimationDefinition rifle =
                AssetDatabase.LoadAssetAtPath<
                    WeaponAnimationDefinition>(
                    RifleDefinitionPath);

            Assert.NotNull(knife);
            Assert.NotNull(rifle);
            Assert.IsTrue(resolver.TryResolveLocation(
                new WeaponId("knife"),
                out string knifeLocation));
            Assert.AreEqual(
                "KnifeWeaponAnimationDefinition",
                knifeLocation);
            Assert.IsTrue(resolver.TryResolveLocation(
                new WeaponId("rifle"),
                out string rifleLocation));
            Assert.AreEqual(
                "RifleAKAnimationDefinition",
                rifleLocation);
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                knife.Validate(new WeaponId("knife")));
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                rifle.Validate(new WeaponId("rifle")));
        }

        [Test]
        public void DefinitionValidation_RequiresCommonFields()
        {
            WeaponAnimationDefinition source =
                AssetDatabase.LoadAssetAtPath<
                    WeaponAnimationDefinition>(
                    KnifeDefinitionPath);
            var definition =
                ScriptableObject.CreateInstance<
                    WeaponAnimationDefinition>();
            try
            {
                EditorUtility.CopySerialized(source, definition);
                SetField(definition, "weaponPrefab", null);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError
                        .MissingWeaponPrefab,
                    definition.Validate());

                EditorUtility.CopySerialized(source, definition);
                SetField(definition, "overlayPose", null);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError
                        .MissingOverlayPose,
                    definition.Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void WeaponRuntime_InitializesFromPureCapabilities()
        {
            var runtime = new WeaponRuntime();
            var capabilities =
                new WeaponRuntimeCapabilities(false, false, true);

            Assert.IsTrue(runtime.Initialize(
                new WeaponId("knife"),
                capabilities));
            Assert.AreEqual(capabilities, runtime.Capabilities);
            Assert.IsFalse(runtime.Initialize(
                new WeaponId("other"),
                new WeaponRuntimeCapabilities(true, true, false)));
            Assert.IsTrue(runtime.RequestPrimaryAction(
                out WeaponActionFact melee));
            Assert.AreEqual(
                WeaponActionKind.MeleeAttack,
                melee.Kind);
        }

        [Test]
        public void KnifeDefinitionDependencies_AreProjectOwned()
        {
            string[] forbidden = AssetDatabase
                .GetDependencies(KnifeDefinitionPath, true)
                .Where(path =>
                    path.StartsWith(
                        "Assets/KINEMATION/",
                        StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(
                        "Assets/ThirdParty/",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.That(forbidden, Is.Empty);
        }

        private static void SetField(
            WeaponAnimationDefinition definition,
            string name,
            object value)
        {
            typeof(WeaponAnimationDefinition)
                .GetField(
                    name,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic)
                ?.SetValue(definition, value);
        }
    }
}
