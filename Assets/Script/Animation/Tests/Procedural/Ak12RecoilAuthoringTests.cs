using System.Linq;
using NUnit.Framework;
using CGame.Animation.EditorTools;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class Ak12RecoilAuthoringTests
    {
        [Test]
        public void FormalAk12AssetsShareRecoilProfileAndAdditiveLayer()
        {
            Ak12RecoilMainlineAssetSetup.Configure();
            RecoilProfile recoil = AssetDatabase.LoadAssetAtPath<RecoilProfile>(
                "Assets/Art/Weapon/Profile/AK12/AK12RecoilProfile.asset");
            UnityEngine.Object weapon = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset");

            Assert.That(recoil, Is.Not.Null);
            Assert.That(weapon, Is.Not.Null);
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            Assert.That(serializedWeapon.FindProperty("recoilProfile").objectReferenceValue, Is.SameAs(recoil));
            Assert.That(serializedWeapon.FindProperty("fireInterval").floatValue, Is.EqualTo(0.1f).Within(0.0001f));
            BoneProfile profile = serializedWeapon.FindProperty("armedProfile").objectReferenceValue as BoneProfile;
            Assert.That(profile, Is.Not.Null);
            Ak12RecoilMainlineAssetSetup.EnsureAdditiveLayerReference(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile), ImportAssetOptions.ForceSynchronousImport);
            profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(AssetDatabase.GetAssetPath(profile));
            AdditiveLayerSettings additive = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(profile))
                .OfType<AdditiveLayerSettings>().SingleOrDefault();
            Assert.That(additive, Is.Not.Null);
            Assert.That(
                additive.WeaponIkBone.Name,
                Is.EqualTo("IK WeaponBone"),
                "AK12 Additive recoil must target the same IK WeaponBone that owns the visible weapon presentation.");
            Assert.That(additive.AimingCurve, Is.EqualTo("AimingWeight"));
            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty layers = serializedProfile.FindProperty("layers");
            int additiveIndex = -1;
            for (int index = 0; index < layers.arraySize; index++)
            {
                if (layers.GetArrayElementAtIndex(index).objectReferenceValue == additive)
                {
                    additiveIndex = index;
                    break;
                }
            }
            Assert.That(additiveIndex, Is.EqualTo(3));
        }
    }
}
