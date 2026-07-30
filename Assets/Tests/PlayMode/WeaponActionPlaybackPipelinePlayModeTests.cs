using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class WeaponActionPlaybackPipelinePlayModeTests
    {
        private GameObject ground;

        [SetUp]
        public void SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "WeaponActionPipelineGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyIfPresent("[GameManager]");
            DestroyIfPresent("[CharacterRuntimeRoot]");
            if (ground != null)
            {
                UnityEngine.Object.DestroyImmediate(ground);
                ground = null;
            }
        }

        [UnityTest]
        public IEnumerator DefaultKnife_PrimaryActionPlaysDuringLocomotionAndCompletesBusiness()
        {
            object spawnManager =
                CharacterSpawnTestConfiguration
                    .CreateManagerWithInMemoryDefinition();
            object operation = Invoke(
                spawnManager,
                "BeginSpawn",
                CreateRequest(
                    "weapon-action-runtime",
                    "WeaponActionRuntimeCharacter"));
            AdvanceSpawn(spawnManager);
            Assert.AreEqual(
                "CharacterReady",
                GetProperty<object>(operation, "State").ToString());

            GameObject character =
                GameObject.Find("WeaponActionRuntimeCharacter");
            Assert.NotNull(character);
            object playerController = GetPlayerController();
            var inputState = new PlayerInputState
            {
                MoveInput = new Vector2(0f, 0.25f),
            };
            Func<PlayerInputState> inputProvider = () => inputState;
            Invoke(
                playerController,
                "SettingInputStateProvider",
                inputProvider);
            WeaponRuntime runtime =
                GetProperty<WeaponRuntime>(
                    playerController,
                    "WeaponRuntime");
            var facts = new List<WeaponActionFact>();
            int fireCommittedCount = 0;
            runtime.ActionChanged += facts.Add;
            runtime.FireCommitted += _ => fireCommittedCount++;
            Vector3 startPosition = character.transform.position;
            Animator animator =
                character.GetComponentInChildren<Animator>();

            Assert.IsTrue(
                runtime.RequestPrimaryAction(
                    out WeaponActionFact action));
            Assert.AreEqual(
                WeaponActionKind.MeleeAttack,
                action.Kind);
            Assert.AreEqual(
                0,
                CountConnectedInputs(
                    GetSlotMixer(animator),
                    1));

            yield return null;
            Assert.AreEqual(
                1,
                CountConnectedInputs(
                    GetSlotMixer(animator),
                    1));
            for (int click = 0; click < 5; click++)
            {
                Assert.IsFalse(
                    runtime.RequestPrimaryAction(out _),
                    "Rapid melee clicks must not restart the active attack.");
                Assert.AreEqual(
                    action.ActionId,
                    runtime.ActiveAction.ActionId);
                Assert.AreEqual(
                    1,
                    facts.Count,
                    "Rapid melee clicks must not publish cancellation/restart facts.");
                yield return null;
            }

            float timeout = 10f;
            while (runtime.ActiveAction.IsValid && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                timeout -= Time.deltaTime;
            }

            inputState = default;
            Invoke(
                playerController,
                "SettingInputStateProvider",
                new object[] { null });
            Assert.Greater(timeout, 0f, "Melee action did not finish.");
            Assert.IsFalse(runtime.ActiveAction.IsValid);
            Assert.AreEqual(
                WeaponActionPhase.Completed,
                facts[facts.Count - 1].Phase);
            Assert.AreEqual(
                WeaponActionEndReason.Completed,
                facts[facts.Count - 1].EndReason);
            Assert.AreEqual(0, fireCommittedCount);
            Assert.Greater(
                character.transform.position.z,
                startPosition.z + 0.05f);
            Assert.AreEqual(
                2,
                character.GetComponentInChildren<Animator>()
                    .playableGraph.GetOutputCount());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FixedCamera_CapturesRuntimeMeleeAndOverlayRestoration()
        {
            object spawnManager =
                CharacterSpawnTestConfiguration
                    .CreateManagerWithInMemoryDefinition();
            object operation = Invoke(
                spawnManager,
                "BeginSpawn",
                CreateRequest(
                    "weapon-action-visual",
                    "WeaponActionVisualCharacter"));
            AdvanceSpawn(spawnManager);
            Assert.AreEqual(
                "CharacterReady",
                GetProperty<object>(operation, "State").ToString());

            GameObject character =
                GameObject.Find("WeaponActionVisualCharacter");
            Animator animator =
                character.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer renderer
                     in character.GetComponentsInChildren<
                         SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen = true;
            }

            object playerController = GetPlayerController();
            WeaponRuntime runtime =
                GetProperty<WeaponRuntime>(
                    playerController,
                    "WeaponRuntime");
            WeaponAnimationDefinition definition =
                Resources.Load<WeaponAnimationDefinition>(
                    "FistsWeaponAnimationDefinition");
            var cameraObject =
                new GameObject("Weapon Action Evidence Camera");
            var lightObject =
                new GameObject("Weapon Action Evidence Light");
            Camera camera = cameraObject.AddComponent<Camera>();
            Light light = lightObject.AddComponent<Light>();
            var target =
                new RenderTexture(
                    960,
                    720,
                    24,
                    RenderTextureFormat.ARGB32);
            Camera[] sceneCameras =
                UnityEngine.Object.FindObjectsOfType<Camera>();
            bool[] cameraStates =
                sceneCameras.Select(item => item.enabled).ToArray();
            string evidenceDirectory = Path.Combine(
                Application.temporaryCachePath,
                "WeaponActionPlaybackPipelineEvidence");
            Directory.CreateDirectory(evidenceDirectory);
            foreach (string evidenceName in new[]
                     {
                         "01-overlay-before-action.png",
                         "02-melee-action.png",
                         "03-overlay-restored.png",
                     })
            {
                string evidencePath = Path.Combine(
                    evidenceDirectory,
                    evidenceName);
                if (File.Exists(evidencePath))
                {
                    File.Delete(evidencePath);
                }
            }

            try
            {
                foreach (Camera sceneCamera in sceneCameras)
                {
                    if (sceneCamera != camera)
                    {
                        sceneCamera.enabled = false;
                    }
                }

                camera.transform.position =
                    new Vector3(2.6f, 1.45f, 3.2f);
                camera.transform.LookAt(
                    new Vector3(0f, 0.9f, 0f));
                camera.fieldOfView = 34f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor =
                    new Color(0.12f, 0.14f, 0.18f);
                camera.targetTexture = target;
                target.Create();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation =
                    Quaternion.Euler(35f, -30f, 0f);

                yield return Capture(
                    camera,
                    Path.Combine(
                        evidenceDirectory,
                        "01-overlay-before-action.png"));

                Assert.IsTrue(
                    runtime.RequestPrimaryAction(
                        out WeaponActionFact melee));
                Assert.AreEqual(
                    WeaponActionKind.MeleeAttack,
                    melee.Kind);
                yield return null;
                float sampleDuration =
                    definition.MeleeAttack.AnimationClip.length
                    * 0.4f
                    / definition.MeleeAttack.Speed;
                float elapsed = 0f;
                while (elapsed < sampleDuration)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }

                Assert.IsTrue(runtime.ActiveAction.IsValid);
                yield return Capture(
                    camera,
                    Path.Combine(
                        evidenceDirectory,
                        "02-melee-action.png"));

                float timeout = 10f;
                while (runtime.ActiveAction.IsValid && timeout > 0f)
                {
                    yield return null;
                    timeout -= Time.deltaTime;
                }

                Assert.Greater(timeout, 0f);
                yield return Capture(
                    camera,
                    Path.Combine(
                        evidenceDirectory,
                        "03-overlay-restored.png"));
                Assert.AreEqual(
                    3,
                    Directory.GetFiles(
                        evidenceDirectory,
                        "*.png").Length);
                TestContext.Out.WriteLine(evidenceDirectory);
            }
            finally
            {
                for (int i = 0; i < sceneCameras.Length; i++)
                {
                    if (sceneCameras[i] != null
                        && sceneCameras[i] != camera)
                    {
                        sceneCameras[i].enabled = cameraStates[i];
                    }
                }

                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(lightObject);
            }
        }

        private static object CreateRequest(
            string requestIdValue,
            string displayName)
        {
            CharacterDefinition definition =
                Resources.Load<CharacterDefinition>(
                    "CharacterDefinition");
            Type requestType =
                RequireRuntimeType("CGame.CharacterSpawnRequest");
            object requestId = Activator.CreateInstance(
                RequireRuntimeType("CGame.CharacterSpawnRequestId"),
                requestIdValue);
            object placement = Activator.CreateInstance(
                RequireRuntimeType("CGame.CharacterSpawnPlacement"),
                Vector3.zero,
                Quaternion.identity);
            return Activator.CreateInstance(
                requestType,
                requestId,
                definition.DefinitionId,
                CharacterControlKind.LocalPlayer,
                placement,
                InputType.Player,
                displayName);
        }

        private static void AdvanceSpawn(object spawnManager)
        {
            for (int i = 0; i < 6; i++)
            {
                Invoke(spawnManager, "Update", 0f);
            }
        }

        private static object GetPlayerController()
        {
            Type controllerManagerType =
                RequireRuntimeType("CGame.ControllerManager");
            object controllerManager = RequireRuntimeType(
                    "CGame.GameManager")
                .GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "GetManager"
                    && method.IsGenericMethodDefinition)
                .MakeGenericMethod(controllerManagerType)
                .Invoke(null, null);
            return controllerManagerType
                .GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .Single(method =>
                    method.Name == "GettingController"
                    && method.IsGenericMethodDefinition)
                .MakeGenericMethod(
                    RequireRuntimeType("CGame.PlayerController"))
                .Invoke(controllerManager, null);
        }

        private static Playable GetSlotMixer(Animator animator)
        {
            Playable master =
                animator.playableGraph.GetOutput(1)
                    .GetSourcePlayable();
            Playable overrideMixer = master.GetInput(1);
            return overrideMixer.GetInput(0);
        }

        private static int CountConnectedInputs(
            Playable playable,
            int firstInput)
        {
            int count = 0;
            for (int i = firstInput;
                 i < playable.GetInputCount();
                 i++)
            {
                if (playable.GetInput(i).IsValid())
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerator Capture(
            Camera camera,
            string path)
        {
            RenderTexture target = camera.targetTexture;
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
                    new Rect(
                        0f,
                        0f,
                        target.width,
                        target.height),
                    0,
                    0);
                texture.Apply();
                File.WriteAllBytes(
                    path,
                    texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            return target.GetType()
                .GetMethod(
                    methodName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        private static T GetProperty<T>(
            object target,
            string propertyName)
        {
            return (T)target.GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(
                type,
                $"Runtime type was not found: {fullName}");
            return type;
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
