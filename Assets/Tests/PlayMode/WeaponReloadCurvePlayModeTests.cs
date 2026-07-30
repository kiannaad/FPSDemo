using System.Collections;
using System.IO;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class WeaponReloadCurvePlayModeTests
    {
        [UnityTest]
        public IEnumerator RealCharacter_ReloadSynchronizesCurvesModelAndCancellation()
        {
            CharacterDefinition characterDefinition = Resources.Load<CharacterDefinition>("CharacterDefinition");
            CharacterAnimationConfig config = Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig");
            GameObject character = Object.Instantiate(characterDefinition.VisualPrefab);
            CharacterAnimationGraph graph = null;
            CharacterWeaponAnimationBridge bridge = null;
            GameObject cameraObject = new GameObject("RifleReloadEvidenceCamera");
            GameObject lightObject = new GameObject("RifleReloadEvidenceLight");
            Camera camera = cameraObject.AddComponent<Camera>();
            Light light = lightObject.AddComponent<Light>();
            var target = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            string evidenceDirectory = Path.Combine(Application.temporaryCachePath, "RifleReloadCurveEvidence");
            Directory.CreateDirectory(evidenceDirectory);
            try
            {
                Animator animator = character.GetComponentInChildren<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = new CharacterAnimationGraph(animator, config);
                bridge = new CharacterWeaponAnimationBridge(animator, config, graph);
                var runtime = new WeaponRuntime();
                runtime.RequestEquip(new WeaponId("rifle"));
                bridge.Update(runtime.Snapshot, 0f, 0f, 0f);
                bridge.BindRuntime(runtime);
                graph.Context.IsGrounded = true;

                camera.transform.position = new Vector3(2.6f, 1.45f, 3.2f);
                camera.transform.LookAt(new Vector3(0f, 1.05f, 0f));
                camera.fieldOfView = 34f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                camera.targetTexture = target;
                target.Create();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

                yield return Tick(graph, bridge, runtime, 12);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "01-before-reload.png"));

                Assert.IsTrue(runtime.RequestReload(out WeaponActionFact reload));
                yield return Tick(graph, bridge, runtime, 135);
                WeaponModelActionPlayer modelPlayer = bridge.CurrentPresentation.ModelActionPlayer;
                Assert.AreEqual(reload.ActionId, graph.ReloadAction.RequestId);
                Assert.AreEqual(reload.ActionId, modelPlayer.ActionId);
                Assert.Less(Mathf.Abs(graph.ReloadAction.NormalizedTime - modelPlayer.NormalizedTime), 0.08f);
                Assert.Less(graph.Context.LeftHandIkWeight, 0.1f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "02-reload-hand-detached.png"));

                yield return Tick(graph, bridge, runtime, 160);
                Assert.IsFalse(graph.ReloadAction.IsActive);
                Assert.IsFalse(modelPlayer.IsPlaying);
                Assert.AreEqual(animator.isHuman ? 1f : 0f, graph.Context.LeftHandIkWeight, 0.05f);
                Assert.IsTrue(runtime.CompleteAction(reload.ActionId));
                yield return Capture(camera, Path.Combine(evidenceDirectory, "03-reload-recovered.png"));

                Assert.IsTrue(runtime.RequestReload(out WeaponActionFact cancelled));
                yield return Tick(graph, bridge, runtime, 45);
                Assert.IsTrue(runtime.CancelAction(cancelled.ActionId));
                yield return Tick(graph, bridge, runtime, 2);
                Assert.IsFalse(graph.ReloadAction.IsActive);
                Assert.IsFalse(modelPlayer.IsPlaying);
                Assert.AreEqual(animator.isHuman ? 1f : 0f, graph.Context.LeftHandIkWeight, 0.05f);
                yield return Capture(camera, Path.Combine(evidenceDirectory, "04-reload-cancelled.png"));
            }
            finally
            {
                bridge?.Dispose();
                graph?.Dispose();
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
                Object.Destroy(character);
            }
        }

        private static IEnumerator Tick(
            CharacterAnimationGraph graph,
            CharacterWeaponAnimationBridge bridge,
            WeaponRuntime runtime,
            int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                bridge.Update(runtime.Snapshot, 0f, -8f, 1f / 60f);
                graph.Update(1f / 60f);
                yield return null;
            }
        }

        private static IEnumerator Capture(Camera camera, string path)
        {
            yield return new WaitForEndOfFrame();
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = camera.targetTexture;
                texture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(texture);
            }
        }
    }
}
