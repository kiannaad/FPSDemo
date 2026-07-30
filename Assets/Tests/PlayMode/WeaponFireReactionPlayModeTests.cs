using System.Collections;
using System.IO;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public class WeaponFireReactionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealCharacter_FireLifecycleKeepsGameplayAndPresentationOneWay()
        {
            CreateFixture(out GameObject character, out CharacterAnimationGraph graph, out CharacterWeaponAnimationBridge bridge, out WeaponRuntime runtime);
            try
            {
                runtime.RequestFire(out WeaponActionFact first);
                yield return Tick(graph, bridge, runtime, 4, 0f, 0f, 0f);
                Assert.IsTrue(graph.FireAction.IsActive, "character fire action should still be active after four frames");
                Assert.AreEqual(first.ActionId, graph.FireAction.RequestId);
                Assert.Greater(graph.RecoilReaction.CurrentPitch, 0f);
                Assert.IsTrue(bridge.CurrentPresentation.ModelActionPlayer.IsPlaying, "AK mechanism clip should still be active after four frames");

                yield return Tick(graph, bridge, runtime, 12, 0f, 0f, 0f);
                Assert.IsFalse(graph.FireAction.IsActive);
                Assert.AreEqual(first.ActionId, runtime.ActiveAction.ActionId);
                Assert.IsTrue(HasDebugEvent(graph, $"PresentationEnded:{first.ActionId}:NaturalEnd"), "natural presentation end diagnostic missing");

                Assert.IsTrue(runtime.CompleteAction(first.ActionId));
                Assert.IsFalse(runtime.ActiveAction.IsValid);

                runtime.RequestFire(out WeaponActionFact second);
                yield return Tick(graph, bridge, runtime, 1, 30f, -15f, 2f);
                float firstImpulse = graph.RecoilReaction.CurrentPitch;
                runtime.RequestFire(out WeaponActionFact third);
                float stackedImpulse = graph.RecoilReaction.CurrentPitch;
                Assert.Greater(stackedImpulse, firstImpulse);
                yield return Tick(graph, bridge, runtime, 1, 30f, -15f, 2f);
                Assert.AreEqual(third.ActionId, graph.FireAction.RequestId);
                Assert.AreEqual("Move", graph.CurrentLocomotionState);
                Assert.AreEqual(1f, graph.Context.AimWeight);

                runtime.RequestUnequip();
                yield return Tick(graph, bridge, runtime, 12, 0f, 0f, 0f);
                Assert.IsFalse(graph.FireAction.IsActive);
                Assert.AreEqual(0f, graph.RecoilReaction.CurrentPitch);
                Assert.IsNull(bridge.CurrentPresentation);
            }
            finally
            {
                bridge.Dispose();
                graph.Dispose();
                Object.Destroy(character);
            }
        }

        [UnityTest]
        public IEnumerator FixedCamera_CapturesFireReactionAcceptanceSet()
        {
            CreateFixture(out GameObject character, out CharacterAnimationGraph graph, out CharacterWeaponAnimationBridge bridge, out WeaponRuntime runtime);
            GameObject cameraObject = new GameObject("RifleFireEvidenceCamera");
            GameObject lightObject = new GameObject("RifleFireEvidenceLight");
            Camera camera = cameraObject.AddComponent<Camera>();
            Light light = lightObject.AddComponent<Light>();
            var evidenceTexture = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            Camera[] sceneCameras = Object.FindObjectsOfType<Camera>();
            bool[] sceneCameraStates = new bool[sceneCameras.Length];
            string evidenceDirectory = Path.Combine(Application.temporaryCachePath, "RifleFireReactionEvidence");
            string frameDirectory = Path.Combine(evidenceDirectory, "recording");
            Directory.CreateDirectory(evidenceDirectory);
            Directory.CreateDirectory(frameDirectory);
            try
            {
                for (int i = 0; i < sceneCameras.Length; i++)
                {
                    sceneCameraStates[i] = sceneCameras[i].enabled;
                    if (sceneCameras[i] != camera)
                    {
                        sceneCameras[i].enabled = false;
                        TestContext.Out.WriteLine($"Disabled competing evidence camera: {sceneCameras[i].name}");
                    }
                }

                camera.depth = 100f;
                camera.transform.position = new Vector3(2.6f, 1.45f, 3.2f);
                camera.transform.LookAt(new Vector3(0f, 1.05f, 0f));
                camera.fieldOfView = 34f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                evidenceTexture.Create();
                camera.targetTexture = evidenceTexture;
                camera.aspect = (float)evidenceTexture.width / evidenceTexture.height;
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

                yield return Tick(graph, bridge, runtime, 12, 0f, 0f, 0f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "01-before-fire.png"));

                runtime.RequestFire(out _);
                yield return Tick(graph, bridge, runtime, 2, 0f, -15f, 0f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "02-single-fire-peak.png"));
                if (graph.Context.Animator.isHuman)
                {
                    Assert.Less(Vector3.Distance(
                        graph.Context.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                        bridge.CurrentPresentation.LeftHandGrip.position), 0.12f);
                }
                else
                {
                    Assert.AreEqual(0f, graph.Context.LeftHandIkWeight);
                }

                yield return Tick(graph, bridge, runtime, 12, 0f, -15f, 0f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "03-aim-recovered.png"));

                runtime.RequestFire(out _);
                yield return Tick(graph, bridge, runtime, 1, 25f, -10f, 2f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "04-moving-fire.png"));

                for (int frame = 0; frame < 36; frame++)
                {
                    if (frame == 0 || frame == 8 || frame == 16)
                    {
                        runtime.RequestFire(out _);
                    }
                    yield return Tick(graph, bridge, runtime, 1, 20f, -10f, 2f);
                    yield return Capture(camera, Path.Combine(frameDirectory, $"frame-{frame:D3}.png"), 640, 480);
                }
                yield return Capture(camera, Path.Combine(evidenceDirectory, "05-continuous-semi-auto.png"));

                runtime.RequestFire(out _);
                yield return Tick(graph, bridge, runtime, 1, 0f, 0f, 0f);
                runtime.RequestUnequip();
                yield return Tick(graph, bridge, runtime, 12, 0f, 0f, 0f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "06-fire-then-unequip.png"));

                Assert.IsTrue(File.Exists(Path.Combine(evidenceDirectory, "02-single-fire-peak.png")));
                Assert.AreEqual(36, Directory.GetFiles(frameDirectory, "frame-*.png").Length);
                TestContext.Out.WriteLine(evidenceDirectory);
            }
            finally
            {
                for (int i = 0; i < sceneCameras.Length; i++)
                {
                    if (sceneCameras[i] != null && sceneCameras[i] != camera)
                    {
                        sceneCameras[i].enabled = sceneCameraStates[i];
                    }
                }

                bridge.Dispose();
                graph.Dispose();
                camera.targetTexture = null;
                evidenceTexture.Release();
                Object.Destroy(evidenceTexture);
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
                Object.Destroy(character);
            }
        }

        private static void CreateFixture(
            out GameObject character,
            out CharacterAnimationGraph graph,
            out CharacterWeaponAnimationBridge bridge,
            out WeaponRuntime runtime)
        {
            CharacterDefinition characterDefinition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            character = Object.Instantiate(characterDefinition.VisualPrefab);
            Animator animator = character.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen = true;
            }
            graph = new CharacterAnimationGraph(animator, config);
            bridge = new CharacterWeaponAnimationBridge(animator, config, graph);
            runtime = new WeaponRuntime();
            runtime.RequestEquip(new WeaponId("rifle"));
            bridge.Update(runtime.Snapshot, 0f, 0f, 0f);
            bridge.BindRuntime(runtime);
            graph.Context.IsGrounded = true;
        }

        private static IEnumerator Tick(
            CharacterAnimationGraph graph,
            CharacterWeaponAnimationBridge bridge,
            WeaponRuntime runtime,
            int frames,
            float aimYaw,
            float aimPitch,
            float moveSpeed)
        {
            for (int i = 0; i < frames; i++)
            {
                graph.Context.MoveSpeed = moveSpeed;
                graph.Context.WorldVelocity = Vector3.forward * moveSpeed;
                graph.Context.LocalVelocity = Vector3.forward * moveSpeed;
                bridge.Update(runtime.Snapshot, aimYaw, aimPitch, 1f / 60f);
                graph.Update(1f / 60f);
                yield return null;
            }
        }

        private static bool HasDebugEvent(CharacterAnimationGraph graph, string eventName)
        {
            foreach (AnimationDebugEvent debugEvent in graph.Context.DebugEvents)
            {
                if (debugEvent.EventName == eventName) return true;
            }
            return false;
        }

        private static IEnumerator Capture(Camera camera, string path, int width = 960, int height = 720)
        {
            _ = width;
            _ = height;
            RenderTexture target = camera.targetTexture;
            Assert.IsNotNull(target, "evidence camera requires a persistent target texture");
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.pixelRect = new Rect(0f, 0f, target.width, target.height);
                camera.aspect = (float)target.width / target.height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                camera.enabled = false;
                yield return new WaitForEndOfFrame();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                texture.Apply();
                NormalizeUninitializedBackground(texture, camera.backgroundColor);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(texture);
            }
        }

        private static void NormalizeUninitializedBackground(Texture2D texture, Color backgroundColor)
        {
            Color32[] pixels = texture.GetPixels32();
            Color32 background = backgroundColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.r == 0 && pixel.g == 0 && pixel.b == 0)
                {
                    pixels[i] = background;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }
    }
}
