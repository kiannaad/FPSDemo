using System.Collections;
using System.IO;
using System.Linq;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class FistsMeleeAssetMigrationPlayModeTests
    {
        private const string CharacterPrefabPath =
            "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab";
        private const string TargetFolder =
            "Assets/Art/Animation/Weapon/KINEMATION/Fists";
        private const string UpperBodyMaskPath =
            "Assets/Art/KinemationLegacyWeaponRuntime/ScriptableAnimationSystemDemo/Animations/Masks/UpperBody.mask";

        [UnityTest]
        public IEnumerator MigratedClips_SampleOnProjectCharacterWithoutInvalidBoneState()
        {
            string[] assetNames =
            {
                "FistsOverlayPoseClipAsset.asset",
                "FistsEquipClipAsset.asset",
                "FistsUnequipClipAsset.asset",
                "FistsMeleeAttackClipAsset.asset",
            };

            foreach (string assetName in assetNames)
            {
                GameObject visual = null;
                CharacterPlayablesController controller = null;
                try
                {
                    GameObject prefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
                    AvatarMask upperBodyMask =
                        AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
                    AnimationClipAsset asset =
                        AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(
                            $"{TargetFolder}/{assetName}");
                    Assert.NotNull(prefab);
                    Assert.NotNull(upperBodyMask);
                    Assert.NotNull(asset);

                    visual = Object.Instantiate(prefab);
                    Animator animator = visual.GetComponentInChildren<Animator>();
                    Assert.NotNull(animator);
                    Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
                    Quaternion[] baselineRotations =
                        bones.Select(bone => bone.localRotation).ToArray();
                    controller =
                        new CharacterPlayablesController(animator, upperBodyMask);

                    AnimationPlaybackHandle handle =
                        assetName.Contains("OverlayPose")
                            ? controller.PlayPose(asset, 7001)
                            : controller.PlayAnimation(asset, 7002);
                    Assert.AreNotEqual(AnimationPlaybackState.Failed, handle.State);
                    float sampleDuration = asset.AnimationClip.length * 0.4f;
                    float elapsed = 0f;
                    while (elapsed < sampleDuration)
                    {
                        yield return null;
                        elapsed += Time.deltaTime;
                        controller.Update(Time.deltaTime);
                    }

                    AssertFiniteAndBounded(bones, assetName);
                    float largestRotationDelta = bones
                        .Select((bone, index) =>
                            Quaternion.Angle(baselineRotations[index], bone.localRotation))
                        .Max();
                    Assert.Greater(
                        largestRotationDelta,
                        0.01f,
                        $"{assetName} did not visibly affect the project skeleton.");
                    Assert.AreEqual(2, animator.playableGraph.GetOutputCount());
                }
                finally
                {
                    controller?.Dispose();
                    if (visual != null)
                    {
                        Object.Destroy(visual);
                    }
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FixedCamera_CapturesMigratedFistsAcceptanceSet()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            AvatarMask upperBodyMask =
                AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            Assert.NotNull(prefab);
            Assert.NotNull(upperBodyMask);

            GameObject visual = Object.Instantiate(prefab);
            Animator animator = visual.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer renderer
                     in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen = true;
            }

            var controller =
                new CharacterPlayablesController(animator, upperBodyMask);
            var cameraObject = new GameObject("Fists Evidence Camera");
            var lightObject = new GameObject("Fists Evidence Light");
            Camera camera = cameraObject.AddComponent<Camera>();
            Light light = lightObject.AddComponent<Light>();
            var target =
                new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            Camera[] sceneCameras = Object.FindObjectsOfType<Camera>();
            bool[] cameraStates =
                sceneCameras.Select(sceneCamera => sceneCamera.enabled).ToArray();
            string evidenceDirectory = Path.Combine(
                Application.temporaryCachePath,
                "FistsMeleeAssetMigrationEvidence");
            Directory.CreateDirectory(evidenceDirectory);

            try
            {
                foreach (Camera sceneCamera in sceneCameras)
                {
                    if (sceneCamera != camera)
                    {
                        sceneCamera.enabled = false;
                    }
                }

                camera.transform.position = new Vector3(2.6f, 1.45f, 3.2f);
                camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
                camera.fieldOfView = 34f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                camera.targetTexture = target;
                target.Create();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

                AnimationClipAsset overlay =
                    LoadAsset("FistsOverlayPoseClipAsset.asset");
                AnimationPlaybackHandle overlayHandle =
                    controller.PlayPose(overlay, 7100);
                Assert.AreNotEqual(
                    AnimationPlaybackState.Failed,
                    overlayHandle.State);
                yield return Tick(controller, 0.6f);
                yield return Capture(
                    camera,
                    Path.Combine(evidenceDirectory, "01-overlay-pose.png"));

                yield return PlaySampleAndCapture(
                    controller,
                    camera,
                    "FistsEquipClipAsset.asset",
                    7101,
                    0.4f,
                    Path.Combine(evidenceDirectory, "02-equip.png"));
                yield return PlaySampleAndCapture(
                    controller,
                    camera,
                    "FistsMeleeAttackClipAsset.asset",
                    7102,
                    0.4f,
                    Path.Combine(evidenceDirectory, "03-melee-attack.png"));
                yield return PlaySampleAndCapture(
                    controller,
                    camera,
                    "FistsUnequipClipAsset.asset",
                    7103,
                    0.4f,
                    Path.Combine(evidenceDirectory, "04-unequip.png"));

                Assert.AreEqual(
                    4,
                    Directory.GetFiles(evidenceDirectory, "*.png").Length);
                TestContext.Out.WriteLine(evidenceDirectory);
            }
            finally
            {
                for (int i = 0; i < sceneCameras.Length; i++)
                {
                    if (sceneCameras[i] != null && sceneCameras[i] != camera)
                    {
                        sceneCameras[i].enabled = cameraStates[i];
                    }
                }

                controller.Dispose();
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
                Object.Destroy(visual);
            }
        }

        private static AnimationClipAsset LoadAsset(string assetName)
        {
            AnimationClipAsset asset =
                AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(
                    $"{TargetFolder}/{assetName}");
            Assert.NotNull(asset);
            return asset;
        }

        private static IEnumerator PlaySampleAndCapture(
            CharacterPlayablesController controller,
            Camera camera,
            string assetName,
            long requestId,
            float normalizedSampleTime,
            string path)
        {
            AnimationClipAsset asset = LoadAsset(assetName);
            AnimationPlaybackHandle handle =
                controller.PlayAnimation(asset, requestId);
            Assert.AreNotEqual(AnimationPlaybackState.Failed, handle.State);
            yield return Tick(
                controller,
                asset.AnimationClip.length * normalizedSampleTime);
            yield return Capture(camera, path);
        }

        private static IEnumerator Tick(
            CharacterPlayablesController controller,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                controller.Update(Time.deltaTime);
            }
        }

        private static IEnumerator Capture(Camera camera, string path)
        {
            RenderTexture target = camera.targetTexture;
            Assert.NotNull(target);
            var texture = new Texture2D(
                target.width,
                target.height,
                TextureFormat.RGB24,
                false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.enabled = false;
                yield return new WaitForEndOfFrame();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(
                    new Rect(0f, 0f, target.width, target.height),
                    0,
                    0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(texture);
            }
        }

        private static void AssertFiniteAndBounded(
            Transform[] bones,
            string assetName)
        {
            Assert.That(bones, Is.Not.Empty);
            foreach (Transform bone in bones)
            {
                Vector3 position = bone.localPosition;
                Vector3 scale = bone.localScale;
                Quaternion rotation = bone.localRotation;
                Assert.IsTrue(IsFinite(position), $"{assetName}: {bone.name} position");
                Assert.IsTrue(IsFinite(scale), $"{assetName}: {bone.name} scale");
                Assert.IsTrue(IsFinite(rotation), $"{assetName}: {bone.name} rotation");
                Assert.Less(position.magnitude, 100f, $"{assetName}: {bone.name} position");
                Assert.Less(scale.magnitude, 20f, $"{assetName}: {bone.name} scale");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z)
                && float.IsFinite(value.w);
        }
    }
}
