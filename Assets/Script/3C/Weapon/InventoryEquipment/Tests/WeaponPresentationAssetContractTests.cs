using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class WeaponPresentationAssetContractTests
    {
        private const string UpperBodyMaskPath = "Assets/Art/Animation/Masks/UpperBody.mask";
        private const string RuntimeAnimationConfigPath = "Assets/Resources/CharacterAnimationConfig.asset";
        private const string DefaultAnimationConfigPath =
            "Assets/Settings/Gameplay/SampleScene/DefaultConfig/Gameplay/DefaultCharacterAnimationConfig.asset";
        private const string Ak12DefinitionPath =
            "Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset";

        [Test]
        public void FormalCharacterAnimationConfigs_UseWeaponCapableUpperBodyMask()
        {
            AvatarMask expectedMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            Assert.That(expectedMask, Is.Not.Null);

            CharacterAnimationConfig[] configs =
            {
                AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(RuntimeAnimationConfigPath),
                AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(DefaultAnimationConfigPath)
            };

            Assert.That(configs, Has.All.Not.Null);
            Assert.That(configs, Has.All.Property(nameof(CharacterAnimationConfig.IsValid)).True);
            Assert.That(configs.Select(config => config.UpperBodyMask), Has.All.SameAs(expectedMask));

            string[] requiredPaths =
            {
                "Skeleton/WeaponBone",
                "Skeleton/IK_Hand_Right",
                "Skeleton/IK_Hand_Left",
                "Skeleton/ik_hand_gun"
            };
            foreach (string requiredPath in requiredPaths)
            {
                int index = FindTransformPath(expectedMask, requiredPath);
                Assert.That(index, Is.GreaterThanOrEqualTo(0),
                    $"Upper-body mask is missing '{requiredPath}'.");
                Assert.That(expectedMask.GetTransformActive(index), Is.True,
                    $"Upper-body mask disables '{requiredPath}'.");
            }
        }

        [Test]
        public void Ak12Presentation_UsesIkWeaponBoneWithoutLocalCompensation()
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Ak12DefinitionPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.PresentationLocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(definition.PresentationLocalRotation, Quaternion.identity),
                Is.LessThan(0.001f));
            Assert.That(definition.PresentationLocalScale, Is.EqualTo(Vector3.one));
        }

        private static int FindTransformPath(AvatarMask mask, string path)
        {
            for (int index = 0; index < mask.transformCount; index++)
            {
                if (mask.GetTransformPath(index) == path)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
