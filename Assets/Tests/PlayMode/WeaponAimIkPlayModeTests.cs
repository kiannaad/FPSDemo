using System.Collections;
using System.IO;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public class WeaponAimIkPlayModeTests
    {
        [UnityTest]
        public IEnumerator FixedCamera_CapturesAimIkAcceptanceSet()
        {
            CharacterDefinition characterDefinition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = Object.Instantiate(characterDefinition.VisualPrefab);
            Animator animator = character.GetComponentInChildren<Animator>();
            CharacterAnimationGraph graph = new CharacterAnimationGraph(animator, config);
            CharacterWeaponAnimationBridge bridge = new CharacterWeaponAnimationBridge(animator, config, graph);
            GameObject cameraObject = new GameObject("RifleAimEvidenceCamera");
            GameObject lightObject = new GameObject("RifleAimEvidenceLight");
            Camera camera = cameraObject.AddComponent<Camera>();
            Light light = lightObject.AddComponent<Light>();
            string evidenceDirectory = Path.Combine(Application.temporaryCachePath, "RifleAimAndIkEvidence");
            Directory.CreateDirectory(evidenceDirectory);
            try
            {
                camera.transform.position = new Vector3(2.6f, 1.45f, 3.2f);
                camera.transform.LookAt(new Vector3(0f, 1.05f, 0f));
                camera.fieldOfView = 34f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                graph.Context.IsGrounded = true;
                var equipped = new WeaponEquipmentSnapshot(new WeaponId("rifle"), 10u);

                yield return SetPoseAndCapture(bridge, graph, camera, equipped, 0f, -50f, 0f, evidenceDirectory, "01-aim-up.png");
                yield return SetPoseAndCapture(bridge, graph, camera, equipped, 0f, 0f, 0f, evidenceDirectory, "02-aim-center.png");
                yield return SetPoseAndCapture(bridge, graph, camera, equipped, 0f, 40f, 0f, evidenceDirectory, "03-aim-down.png");
                yield return SetPoseAndCapture(bridge, graph, camera, equipped, -60f, 0f, 0f, evidenceDirectory, "04-aim-left.png");
                yield return SetPoseAndCapture(bridge, graph, camera, equipped, 60f, 0f, 0f, evidenceDirectory, "05-aim-right.png");
                yield return SetPoseAndCapture(bridge, graph, camera, equipped, 35f, -20f, 2f, evidenceDirectory, "06-moving-aim.png");

                bridge.Update(new WeaponEquipmentSnapshot(default, 11u), 0f, 0f, 0.2f);
                for (int i = 0; i < 20; i++)
                {
                    graph.Update(1f / 60f);
                    bridge.Update(new WeaponEquipmentSnapshot(default, 11u), 0f, 0f, 1f / 60f);
                    yield return null;
                }
                Capture(camera, Path.Combine(evidenceDirectory, "07-unequipped.png"));

                GameObject degradedWeapon = Object.Instantiate(config.WeaponDefinitions[0].PresentationPrefab);
                WeaponPresentationInstance degradedPresentation = degradedWeapon.GetComponent<WeaponPresentationInstance>();
                degradedPresentation.AttachTo(FindBone(animator, "Right_Hand"));
                var degradedBinding = new WeaponPresentationBinding(12u, degradedPresentation, null, degradedPresentation.Muzzle);
                graph.ApplyWeaponEquipment(new WeaponEquipmentSnapshot(new WeaponId("rifle"), 12u), degradedBinding);
                graph.SetAimInput(0f, 0f);
                for (int i = 0; i < 20; i++)
                {
                    graph.Update(1f / 60f);
                    yield return null;
                }
                Capture(camera, Path.Combine(evidenceDirectory, "08-missing-grip-degraded.png"));
                Assert.AreEqual(0f, graph.Context.LeftHandIkWeight);
                Object.Destroy(degradedWeapon);

                Assert.IsTrue(File.Exists(Path.Combine(evidenceDirectory, "01-aim-up.png")));
                Assert.IsTrue(File.Exists(Path.Combine(evidenceDirectory, "08-missing-grip-degraded.png")));
                TestContext.Out.WriteLine(evidenceDirectory);
            }
            finally
            {
                bridge.Dispose();
                graph.Dispose();
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
                Object.Destroy(character);
            }
        }

        [UnityTest]
        public IEnumerator RealCharacter_EquipAimMoveUnequipAndRapidGenerationRemainContinuous()
        {
            CharacterDefinition characterDefinition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = Object.Instantiate(characterDefinition.VisualPrefab);
            Animator animator = character.GetComponentInChildren<Animator>();
            CharacterAnimationGraph graph = new CharacterAnimationGraph(animator, config);
            GameObject weapon = null;
            GameObject secondWeapon = null;
            try
            {
                weapon = Object.Instantiate(config.WeaponDefinitions[0].PresentationPrefab);
                WeaponPresentationInstance presentation = weapon.GetComponent<WeaponPresentationInstance>();
                Transform rightHand = FindBone(animator, "Right_Hand");
                Transform leftHand = FindBone(animator, "Left_Hand");
                Assert.IsTrue(presentation.AttachTo(rightHand));
                graph.ApplyWeaponEquipment(
                    new WeaponEquipmentSnapshot(new WeaponId("rifle"), 1u),
                    presentation.CreateBinding(1u));
                graph.Context.WorldVelocity = Vector3.zero;
                graph.Context.LocalVelocity = Vector3.zero;
                graph.Context.IsGrounded = true;
                graph.SetAimInput(45f, -30f);

                for (int i = 0; i < 90; i++)
                {
                    graph.Update(1f / 60f);
                    yield return null;
                }

                Assert.Greater(graph.AimOffset.CurrentWeight, 0.5f);
                if (animator.isHuman)
                {
                    Assert.Greater(graph.LeftHandIk.CurrentWeight, 0.5f);
                    float gripDistance = Vector3.Distance(
                        leftHand.position,
                        presentation.LeftHandGrip.position);
                    Assert.Less(gripDistance, 0.12f);
                }
                else
                {
                    Assert.IsNull(graph.LeftHandIk);
                    Assert.AreEqual(0f, graph.Context.LeftHandIkWeight);
                }
                Vector3 weaponForward = (presentation.Muzzle.position - presentation.transform.position).normalized;
                Assert.Greater(Vector3.Dot(animator.transform.forward, weaponForward), 0.95f,
                    $"The mounted rifle must point along the character forward axis instead of across the chest. " +
                    $"weaponForward={weaponForward}; handLocalRotation=" +
                    $"{(Quaternion.Inverse(animator.transform.rotation) * rightHand.rotation).eulerAngles}; " +
                    $"mountLocalRotation={presentation.RightHandMount.localRotation.eulerAngles}");
                Assert.Less(Mathf.Abs(Vector3.Dot(animator.transform.up, weaponForward)), 0.15f,
                    "The mounted rifle must remain close to level in the reference idle pose.");
                if (animator.isHuman)
                {
                    float wristTargetAngle = Quaternion.Angle(
                        leftHand.rotation,
                        presentation.LeftHandGrip.rotation);
                    Assert.Greater(wristTargetAngle, 5f,
                        "Left-hand IK must preserve the authored wrist pose instead of fully forcing the grip rotation.");
                }
                graph.Context.WorldVelocity = Vector3.forward * 2f;
                graph.Context.LocalVelocity = Vector3.forward * 2f;
                graph.Context.MoveSpeed = 2f;
                graph.SetAimInput(-45f, 30f);
                for (int i = 0; i < 8; i++)
                {
                    graph.Update(1f / 60f);
                    yield return null;
                }
                Assert.AreEqual("Move", graph.CurrentLocomotionState);

                secondWeapon = Object.Instantiate(config.WeaponDefinitions[0].PresentationPrefab);
                WeaponPresentationInstance secondPresentation = secondWeapon.GetComponent<WeaponPresentationInstance>();
                secondPresentation.AttachTo(rightHand);
                graph.ApplyWeaponEquipment(
                    new WeaponEquipmentSnapshot(new WeaponId("rifle"), 2u),
                    secondPresentation.CreateBinding(2u));
                graph.Update(1f / 60f);
                yield return null;
                Assert.AreEqual(2u, graph.Context.ActiveWeaponGeneration);
                if (animator.isHuman)
                {
                    Assert.Greater(graph.LeftHandIk.CurrentWeight, 0f);
                }
                else
                {
                    Assert.AreEqual(0f, graph.Context.LeftHandIkWeight);
                }

                graph.ApplyWeaponEquipment(new WeaponEquipmentSnapshot(default, 3u));
                for (int i = 0; i < 12; i++)
                {
                    graph.Update(1f / 60f);
                    yield return null;
                }
                Assert.Less(graph.AimOffset.CurrentWeight, 0.1f);
                if (animator.isHuman)
                {
                    Assert.Less(graph.LeftHandIk.CurrentWeight, 0.1f);
                }
                else
                {
                    Assert.IsNull(graph.LeftHandIk);
                }
            }
            finally
            {
                graph.Dispose();
                Object.Destroy(weapon);
                Object.Destroy(secondWeapon);
                Object.Destroy(character);
            }
        }

        private static IEnumerator SetPoseAndCapture(
            CharacterWeaponAnimationBridge bridge,
            CharacterAnimationGraph graph,
            Camera camera,
            WeaponEquipmentSnapshot snapshot,
            float yaw,
            float pitch,
            float moveSpeed,
            string directory,
            string fileName)
        {
            graph.Context.MoveSpeed = moveSpeed;
            graph.Context.WorldVelocity = Vector3.forward * moveSpeed;
            graph.Context.LocalVelocity = Vector3.forward * moveSpeed;
            for (int i = 0; i < 24; i++)
            {
                bridge.Update(snapshot, yaw, pitch, 1f / 60f);
                graph.Update(1f / 60f);
                yield return null;
            }
            Capture(camera, Path.Combine(directory, fileName));
        }

        private static Transform FindBone(Animator animator, string boneName)
        {
            foreach (Transform candidate in animator.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == boneName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void Capture(Camera camera, string path)
        {
            var renderTexture = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, 960f, 720f), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(renderTexture);
                Object.Destroy(texture);
            }
        }
    }
}
