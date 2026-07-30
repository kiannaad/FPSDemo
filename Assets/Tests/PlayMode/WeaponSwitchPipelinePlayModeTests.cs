using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class WeaponSwitchPipelinePlayModeTests
    {
        private GameObject ground;

        [SetUp]
        public void SetUp()
        {
            ground = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            ground.name = "WeaponSwitchPipelineGround";
            ground.transform.position =
                new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale =
                new Vector3(20f, 1f, 20f);
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
        public IEnumerator KnifeToRifle_SwitchesTransactionallyWhileMovingAndCapturesStages()
        {
            object spawnManager =
                CharacterSpawnTestConfiguration
                    .CreateManagerWithInMemoryDefinition();
            object operation = Invoke(
                spawnManager,
                "BeginSpawn",
                CreateRequest(
                    "weapon-switch-success",
                    "WeaponSwitchSuccessCharacter"));
            AdvanceSpawn(spawnManager);
            Assert.AreEqual(
                "CharacterReady",
                GetProperty<object>(
                    operation,
                    "State").ToString());

            GameObject character =
                GameObject.Find(
                    "WeaponSwitchSuccessCharacter");
            Assert.NotNull(character);
            Animator animator =
                character.GetComponentInChildren<Animator>();
            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer renderer
                     in character.GetComponentsInChildren<
                         SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen = true;
            }

            object playerController = GetPlayerController();
            var inputState = new PlayerInputState
            {
                MoveInput = new Vector2(0f, 0.2f),
            };
            Func<PlayerInputState> inputProvider =
                () => inputState;
            Invoke(
                playerController,
                "SettingInputStateProvider",
                inputProvider);
            WeaponRuntime runtime =
                GetProperty<WeaponRuntime>(
                    playerController,
                    "WeaponRuntime");
            var switchFacts = new List<WeaponSwitchFact>();
            int equipmentChanges = 0;
            runtime.SwitchChanged += switchFacts.Add;
            runtime.EquipmentChanged += _ => equipmentChanges++;
            Assert.AreEqual(
                new WeaponId("knife"),
                runtime.Snapshot.EquippedWeaponId,
                "The character must enter play in the configured default knife state.");
            Assert.IsTrue(runtime.Capabilities.SupportsMeleeAttack);
            Assert.IsFalse(runtime.Capabilities.SupportsFire);
            WeaponAnimationDefinition knife =
                Resources.Load<WeaponAnimationDefinition>(
                    "FistsWeaponAnimationDefinition");
            Vector3 startPosition =
                character.transform.position;

            using (var evidence =
                   new EvidenceCapture(
                       character,
                       "WeaponSwitchPipelineEvidence"))
            {
                yield return evidence.Capture(
                    "01-knife-before-switch.png");

                Assert.AreEqual(
                    WeaponSwitchRequestResult.Started,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("rifle"),
                        out WeaponSwitchFact started));
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.IsFalse(
                    runtime.RequestPrimaryAction(out _));
                Assert.IsFalse(runtime.RequestReload(out _));
                Assert.AreEqual(
                    WeaponSwitchRequestResult
                        .AlreadySwitching,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("pistol"),
                        out _));

                yield return null;
                float sampleDuration =
                    knife.Unequip.AnimationClip.length
                    * 0.4f
                    / knife.Unequip.Speed;
                float elapsed = 0f;
                while (elapsed < sampleDuration
                       && runtime.IsSwitching)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }

                Assert.IsTrue(runtime.IsSwitching);
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);
                yield return evidence.Capture(
                    "02-knife-unequip.png");

                float timeout = 15f;
                while (runtime.IsSwitching && timeout > 0f)
                {
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    timeout -= Time.deltaTime;
                }

                Assert.Greater(
                    timeout,
                    0f,
                    "Successful weapon switch timed out.");
                Assert.AreEqual(
                    new WeaponId("rifle"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.IsTrue(runtime.Capabilities.SupportsFire);
                Assert.IsTrue(
                    runtime.Capabilities.SupportsReload);
                Assert.IsFalse(
                    runtime.Capabilities
                        .SupportsMeleeAttack);
                Assert.AreEqual(1, equipmentChanges);
                Assert.AreEqual(
                    WeaponSwitchPhase.Completed,
                    switchFacts[switchFacts.Count - 1]
                        .Phase);
                Assert.AreEqual(
                    started.SwitchId,
                    switchFacts[switchFacts.Count - 1]
                        .SwitchId);
                Assert.Greater(
                    character.transform.position.z,
                    startPosition.z + 0.05f);
                Assert.AreEqual(
                    2,
                    animator.playableGraph.GetOutputCount());
                yield return evidence.Capture(
                    "03-rifle-after-switch.png");

                Assert.AreEqual(
                    WeaponSwitchRequestResult.Started,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("missing"),
                        out _));
                timeout = 5f;
                while (runtime.IsSwitching && timeout > 0f)
                {
                    yield return null;
                    timeout -= Time.deltaTime;
                }

                Assert.Greater(timeout, 0f);
                Assert.AreEqual(
                    new WeaponId("rifle"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.AreEqual(
                    WeaponSwitchEndReason.TargetLoadFailed,
                    switchFacts[switchFacts.Count - 1]
                        .EndReason);
                Assert.AreEqual(1, equipmentChanges);
                Assert.AreEqual(3, evidence.FileCount);
            }

            inputState = default;
            Invoke(
                playerController,
                "SettingInputStateProvider",
                new object[] { null });
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TargetPlaybackFailure_RestoresKnifeBeforeReopeningRequests()
        {
            WeaponAnimationDefinition knife =
                Resources.Load<WeaponAnimationDefinition>(
                    "FistsWeaponAnimationDefinition");
            WeaponAnimationDefinition rifle =
                LoadEditorAsset<WeaponAnimationDefinition>(
                    "Assets/Art/Animation/Weapon/KINEMATION/AK/"
                    + "RifleAKAnimationDefinition.asset");
            Assert.NotNull(rifle);
            WeaponAnimationDefinition faultedRifle =
                UnityEngine.Object.Instantiate(rifle);
            object spawnManager =
                CharacterSpawnTestConfiguration
                    .CreateManagerWithInMemoryDefinition(
                        knife,
                        faultedRifle);
            try
            {
                object operation = Invoke(
                    spawnManager,
                    "BeginSpawn",
                    CreateRequest(
                        "weapon-switch-recovery",
                        "WeaponSwitchRecoveryCharacter"));
                AdvanceSpawn(spawnManager);
                Assert.AreEqual(
                    "CharacterReady",
                    GetProperty<object>(
                        operation,
                        "State").ToString());

                GameObject character =
                    GameObject.Find(
                        "WeaponSwitchRecoveryCharacter");
                object playerController =
                    GetPlayerController();
                WeaponRuntime runtime =
                    GetProperty<WeaponRuntime>(
                        playerController,
                        "WeaponRuntime");
                var facts = new List<WeaponSwitchFact>();
                runtime.SwitchChanged += facts.Add;
                using (var evidence =
                       new EvidenceCapture(
                           character,
                           "WeaponSwitchPipelineRecoveryEvidence"))
                {
                    yield return evidence.Capture(
                        "01-knife-before-failure.png");
                    Assert.AreEqual(
                        WeaponSwitchRequestResult.Started,
                        runtime.RequestSwitchWeapon(
                            new WeaponId("rifle"),
                            out _));

                    yield return null;
                    SetPrivateField(
                        faultedRifle,
                        "equip",
                        null);
                    LogAssert.Expect(
                        LogType.Error,
                        "An AnimationClipAsset is required.");
                    float timeout = 15f;
                    while (runtime.IsSwitching
                           && timeout > 0f)
                    {
                        yield return null;
                        timeout -= Time.deltaTime;
                    }

                    Assert.Greater(
                        timeout,
                        0f,
                        "Failure recovery timed out.");
                    Assert.AreEqual(
                        new WeaponId("knife"),
                        runtime.Snapshot.EquippedWeaponId);
                    Assert.AreEqual(
                        WeaponSwitchEndReason
                            .TargetPlaybackFailed,
                        facts[facts.Count - 1].EndReason);
                    yield return evidence.Capture(
                        "02-knife-restored.png");
                    Assert.AreEqual(2, evidence.FileCount);
                    Assert.IsTrue(
                        runtime.RequestPrimaryAction(
                            out WeaponActionFact melee));
                    Assert.AreEqual(
                        WeaponActionKind.MeleeAttack,
                        melee.Kind);
                    Assert.AreEqual(
                        2,
                        character.GetComponentInChildren<
                                Animator>()
                            .playableGraph.GetOutputCount());

                    SetPrivateField(
                        faultedRifle,
                        "equip",
                        rifle.Equip);
                    Assert.AreEqual(
                        WeaponSwitchRequestResult.Started,
                        runtime.RequestSwitchWeapon(
                            new WeaponId("rifle"),
                            out _));
                    timeout = 15f;
                    while (runtime.IsSwitching
                           && timeout > 0f)
                    {
                        yield return null;
                        timeout -= Time.deltaTime;
                    }

                    Assert.Greater(
                        timeout,
                        0f,
                        "Retry after playback recovery timed out.");
                    Assert.AreEqual(
                        new WeaponId("rifle"),
                        runtime.Snapshot.EquippedWeaponId);
                    Assert.IsTrue(runtime.Capabilities.SupportsFire);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    faultedRifle);
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
                RequireRuntimeType(
                    "CGame.CharacterSpawnRequest");
            object requestId = Activator.CreateInstance(
                RequireRuntimeType(
                    "CGame.CharacterSpawnRequestId"),
                requestIdValue);
            object placement = Activator.CreateInstance(
                RequireRuntimeType(
                    "CGame.CharacterSpawnPlacement"),
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
                RequireRuntimeType(
                    "CGame.ControllerManager");
            object controllerManager =
                RequireRuntimeType("CGame.GameManager")
                    .GetMethods(
                        BindingFlags.Public
                        | BindingFlags.Static)
                    .Single(method =>
                        method.Name == "GetManager"
                        && method.IsGenericMethodDefinition)
                    .MakeGenericMethod(
                        controllerManagerType)
                    .Invoke(null, null);
            return controllerManagerType
                .GetMethods(
                    BindingFlags.Public
                    | BindingFlags.Instance)
                .Single(method =>
                    method.Name == "GettingController"
                    && method.IsGenericMethodDefinition)
                .MakeGenericMethod(
                    RequireRuntimeType(
                        "CGame.PlayerController"))
                .Invoke(controllerManager, null);
        }

        private static T LoadEditorAsset<T>(string path)
            where T : UnityEngine.Object
        {
            Type assetDatabaseType =
                Type.GetType(
                    "UnityEditor.AssetDatabase, "
                    + "UnityEditor.CoreModule");
            MethodInfo loadMethod =
                assetDatabaseType?.GetMethod(
                    "LoadAssetAtPath",
                    BindingFlags.Public
                    | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(Type),
                    },
                    null);
            return loadMethod?.Invoke(
                       null,
                       new object[] { path, typeof(T) })
                   as T;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic)
                ?.SetValue(target, value);
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

        private static Type RequireRuntimeType(
            string fullName)
        {
            Type type = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType(fullName))
                .FirstOrDefault(candidate =>
                    candidate != null);
            Assert.NotNull(
                type,
                $"Runtime type was not found: {fullName}");
            return type;
        }

        private static void DestroyIfPresent(
            string objectName)
        {
            GameObject target =
                GameObject.Find(objectName);
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class EvidenceCapture : IDisposable
        {
            private readonly Camera camera;
            private readonly Light light;
            private readonly RenderTexture target;
            private readonly Camera[] sceneCameras;
            private readonly bool[] cameraStates;
            private readonly string directory;
            private readonly Transform characterTransform;
            private readonly Vector3 cameraOffset =
                new Vector3(2.6f, 1.45f, 3.2f);
            private readonly Vector3 lookOffset =
                new Vector3(0f, 0.9f, 0f);

            public EvidenceCapture(
                GameObject character,
                string evidenceDirectoryName)
            {
                characterTransform =
                    character.transform;
                var cameraObject =
                    new GameObject(
                        "Weapon Switch Evidence Camera");
                var lightObject =
                    new GameObject(
                        "Weapon Switch Evidence Light");
                camera = cameraObject.AddComponent<Camera>();
                light = lightObject.AddComponent<Light>();
                target = new RenderTexture(
                    960,
                    720,
                    24,
                    RenderTextureFormat.ARGB32);
                sceneCameras =
                    UnityEngine.Object.FindObjectsOfType<
                        Camera>();
                cameraStates =
                    sceneCameras.Select(item =>
                        item.enabled).ToArray();
                foreach (Camera sceneCamera in sceneCameras)
                {
                    if (sceneCamera != camera)
                    {
                        sceneCamera.enabled = false;
                    }
                }

                AlignCamera();
                camera.fieldOfView = 34f;
                camera.clearFlags =
                    CameraClearFlags.SolidColor;
                camera.backgroundColor =
                    new Color(0.12f, 0.14f, 0.18f);
                camera.targetTexture = target;
                target.Create();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation =
                    Quaternion.Euler(35f, -30f, 0f);
                directory = Path.Combine(
                    Application.temporaryCachePath,
                    evidenceDirectoryName);
                Directory.CreateDirectory(directory);
                foreach (string path
                         in Directory.GetFiles(
                             directory,
                             "*.png"))
                {
                    File.Delete(path);
                }
            }

            public int FileCount =>
                Directory.GetFiles(
                    directory,
                    "*.png").Length;

            public IEnumerator Capture(string fileName)
            {
                var texture = new Texture2D(
                    target.width,
                    target.height,
                    TextureFormat.RGB24,
                    false);
                RenderTexture previous =
                    RenderTexture.active;
                try
                {
                    AlignCamera();
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
                        Path.Combine(directory, fileName),
                        texture.EncodeToPNG());
                    TestContext.Out.WriteLine(
                        Path.Combine(directory, fileName));
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.Destroy(texture);
                }
            }

            public void Dispose()
            {
                for (int i = 0;
                     i < sceneCameras.Length;
                     i++)
                {
                    if (sceneCameras[i] != null
                        && sceneCameras[i] != camera)
                    {
                        sceneCameras[i].enabled =
                            cameraStates[i];
                    }
                }

                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(
                    camera.gameObject);
                UnityEngine.Object.Destroy(light.gameObject);
            }

            private void AlignCamera()
            {
                camera.transform.position =
                    characterTransform.position
                    + cameraOffset;
                camera.transform.LookAt(
                    characterTransform.position
                    + lookOffset);
            }
        }
    }
}
