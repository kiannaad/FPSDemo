using System.IO;
using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public class ImportedWeaponArtTests
    {
        private const string WeaponArtRoot = "Assets/Art/Weapon/KINEMATION";

        [Test]
        public void CharacterAnimationFbx_AreHumanoidAndRootMotionLocked()
        {
            string[] paths = AssetDatabase.FindAssets("t:Model", new[] { WeaponArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".FBX", System.StringComparison.OrdinalIgnoreCase)
                    && path.Contains("/Animation/Character/"))
                .ToArray();

            Assert.AreEqual(82, paths.Length);
            foreach (string path in paths)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(ModelImporterAnimationType.Human, importer.animationType, path);
                Assert.AreEqual(ModelImporterAvatarSetup.CopyFromOther, importer.avatarSetup, path);
                Assert.IsNotNull(importer.sourceAvatar, path);
                Assert.IsTrue(importer.sourceAvatar.isHuman, path);
                Assert.IsTrue(importer.sourceAvatar.isValid, path);

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(asset => !asset.name.StartsWith("__preview__"));
                Assert.IsNotNull(clip, path);
                Assert.IsTrue(clip.isHumanMotion, path);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(path), clip.name, path);

                ModelImporterClipAnimation settings = importer.clipAnimations.Single();
                Assert.IsTrue(settings.lockRootRotation, path);
                Assert.IsTrue(settings.lockRootHeightY, path);
                Assert.IsTrue(settings.lockRootPositionXZ, path);
                Assert.AreEqual(-0.9f, settings.heightOffset, 0.001f, path);
            }
        }

        [Test]
        public void WeaponModelFbx_AreRenderableAndUseValidMaterials()
        {
            string[] paths = AssetDatabase.FindAssets("t:Model", new[] { WeaponArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".FBX", System.StringComparison.OrdinalIgnoreCase)
                    && path.Contains("/Model/"))
                .ToArray();

            Assert.AreEqual(92, paths.Length);
            foreach (string path in paths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(model, path);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers, path);
                foreach (Material material in renderers.SelectMany(renderer => renderer.sharedMaterials))
                {
                    Assert.IsNotNull(material, path);
                    Assert.IsNotNull(material.shader, path + ":" + material.name);
                    Assert.AreNotEqual("Hidden/InternalErrorShader", material.shader.name, path + ":" + material.name);
                    if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                    {
                        Assert.AreNotEqual(
                            "Universal Render Pipeline/Lit",
                            material.shader.name,
                            path + ":" + material.name + " is incompatible with the active Built-in Render Pipeline.");
                    }
                }
            }
        }

        [Test]
        public void RifleAkDefinition_UsesHumanoidArtAndPresentationAnchors()
        {
            const string definitionPath = "Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset";
            const string prefabPath = "Assets/Art/Weapon/KINEMATION/AK/Prefabs/RifleAKPresentation.prefab";
            WeaponAnimationDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(definitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(definition);
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                definition.Validate(new WeaponId("rifle")));
            Assert.AreEqual(
                new WeaponRuntimeCapabilities(
                    true,
                    true,
                    false),
                definition.Capabilities);
            Assert.IsTrue(
                definition.OverlayPose.AnimationClip
                    .isHumanMotion);
            Assert.IsNotNull(definition.Fire);
            Assert.IsFalse(definition.Fire.AnimationClip.legacy);
            Assert.IsFalse(definition.Fire.AnimationClip.isLooping);
            Assert.IsNotEmpty(
                AnimationUtility.GetCurveBindings(
                    definition.Fire.AnimationClip));
            Assert.IsTrue(
                AssetDatabase.GetAssetPath(
                        definition.Fire.AnimationClip)
                    .StartsWith(
                        "Assets/Art/Weapon/KINEMATION/AK/"
                        + "Animation/Character/"));
            Assert.IsNotNull(definition.Reload);
            Assert.IsFalse(
                definition.Reload.AnimationClip.isLooping);
            Assert.IsTrue(
                AssetDatabase.GetAssetPath(
                        definition.OverlayPose)
                    .StartsWith(
                        "Assets/Art/Animation/Weapon/"
                        + "KINEMATION/AK/"));
            Assert.IsNotNull(prefab);
            Assert.AreSame(prefab, definition.WeaponPrefab);
            Transform rightHandMount = prefab.transform.Find("RightHandMount");
            Assert.IsNotNull(rightHandMount);
            Assert.AreEqual(Vector3.zero, rightHandMount.localPosition);
            Quaternion expectedMountRotation = Quaternion.Euler(328.5f, 26.66f, 263.85f);
            Assert.Less(Quaternion.Angle(expectedMountRotation, rightHandMount.localRotation), 0.1f);
            Assert.IsNotNull(prefab.transform.Find("LeftHandGrip"));
            Assert.IsNotNull(prefab.transform.Find("Muzzle"));
            Assert.IsNotEmpty(prefab.GetComponentsInChildren<Renderer>(true));
            Assert.IsNotNull(prefab.GetComponent<WeaponPresentationInstance>().ModelActionPlayer);
        }
    }
}
