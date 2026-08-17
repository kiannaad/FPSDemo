using System;
using System.Linq;
using CGame.Animation;
using CGame.Animation.Rig;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class WeaponAssetMigrationTests
    {
        [TestCase("Knife", "Weapon.Knife")]
        [TestCase("AK12", "Weapon.Rifle.AK12")]
        public void MigratedWeaponAssets_AreSelfContainedAndConfigured(string name, string expectedTag)
        {
            string prefabPath = $"Assets/Art/Weapon/{name}/{name}Weapon.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            Assert.That(dependencies.Any(path => path.StartsWith("Assets/KINEMATION/", StringComparison.Ordinal)), Is.False);
            Assert.That(dependencies.Any(path => path.Contains("KinemationLegacyWeaponRuntime", StringComparison.Ordinal)), Is.False);

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Assert.That(contents.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Animator[] animators = contents.GetComponentsInChildren<Animator>(true);
                Assert.That(animators, Has.Length.EqualTo(1));
                Assert.That(animators[0].runtimeAnimatorController, Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            string animationDirectory = $"Assets/Art/Weapon/Profile/{name}/";
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(animationDirectory + name + "BoneProfile.asset");
            KRig rig = AssetDatabase.LoadAssetAtPath<KRig>(
                "Assets/Settings/Gameplay/SampleScene/PawnConfig/KinemationVisualCharacterRig.asset");
            Assert.That(profile, Is.Not.Null);
            profile.Validate(rig);
            Assert.That(profile.Layers.Select(layer => layer.GetType()), Is.EqualTo(new[]
            {
                typeof(PoseSamplerLayerSettings),
                typeof(IkLayerSettings)
            }));
            Assert.That(AssetDatabase.LoadAssetAtPath<WeaponIkMotion>(animationDirectory + name + "EquipIkMotion.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<WeaponIkMotion>(animationDirectory + name + "UnequipIkMotion.asset"), Is.Not.Null);

            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                $"Assets/Settings/Gameplay/WeaponDefinition/{name}/{name}WeaponDefinition.asset");
            WeaponItemDefinition item = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(
                $"Assets/Settings/Gameplay/WeaponDefinition/{name}/{name}WeaponItemDefinition.asset");
            Assert.That(definition.Prefab, Is.SameAs(prefab));
            Assert.That(definition.ArmedProfile, Is.SameAs(profile));
            Assert.That(definition.OverlayPose, Is.Not.Null);
            Assert.That(definition.EquipIkMotion, Is.Not.Null);
            Assert.That(definition.UnequipIkMotion, Is.Not.Null);
            Assert.That(definition.WeaponTag.ToString(), Is.EqualTo(expectedTag));
            Assert.That(item.WeaponDefinition, Is.SameAs(definition));
            definition.Validate();
        }
    }
}
