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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using YooAsset;

namespace CGame.Tests
{
    public sealed class KnifeToAkFullChainValidationPlayModeTests
    {
        private const string EvidenceDirectoryName =
            "KnifeToAkFullChainValidationEvidence";

        private GameObject launcherObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ReturnLauncherToEmptyState();
            DestroyIfPresent("[GameManager]");
            DestroyIfPresent("[CharacterRuntimeRoot]");
            DestroyIfPresent("[CharacterTestRuntime]");
            DestroyIfPresent("[WeaponAnimationTestInput]");
            ResourcePackage package =
                YooAssets.Initialized
                    ? YooAssets.TryGetPackage(
                        "DefaultPackage")
                    : null;
            if (package != null)
            {
                yield return package.DestroyAsync();
                YooAssets.RemovePackage("DefaultPackage");
                YooAssets.Destroy();
            }

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ReturnLauncherToEmptyState();
            if (launcherObject != null)
            {
                UnityEngine.Object.DestroyImmediate(launcherObject);
                launcherObject = null;
            }

            DestroyIfPresent("[GameManager]");
            DestroyIfPresent("[CharacterRuntimeRoot]");
            DestroyIfPresent("[CharacterTestRuntime]");
            DestroyIfPresent("[WeaponAnimationTestInput]");
            ResourcePackage package =
                YooAssets.Initialized
                    ? YooAssets.TryGetPackage(
                        "DefaultPackage")
                    : null;
            if (package != null)
            {
                yield return package.DestroyAsync();
                YooAssets.RemovePackage("DefaultPackage");
                YooAssets.Destroy();
            }

        }

        [UnityTest]
        public IEnumerator SampleSceneTKey_SwitchesKnifeToRifleAndBack()
        {
            launcherObject =
                new GameObject("KnifeToAkKeyboardLauncher");
            Type launcherManagerType =
                Type.GetType(
                    "CGame.GameLauncherMgr, Assembly-CSharp");
            Assert.NotNull(launcherManagerType);
            launcherObject.AddComponent(launcherManagerType);

            GameObject character = null;
            float startupTimeout = 20f;
            while (character == null && startupTimeout > 0f)
            {
                character = GameObject.Find("RuntimeCharacter");
                yield return null;
                startupTimeout -= Time.deltaTime;
            }

            Assert.NotNull(character);
            Type pawnHostType =
                Type.GetType(
                    "CGame.PawnHost, Assembly-CSharp");
            Assert.NotNull(pawnHostType);
            Component pawnHost =
                character.GetComponent(pawnHostType);
            Assert.NotNull(pawnHost);
            object pawn =
                GetProperty<object>(pawnHost, "Pawn");
            object controller =
                GetProperty<object>(pawn, "Controller");
            Assert.NotNull(controller);
            WeaponRuntime runtime =
                GetProperty<WeaponRuntime>(
                    controller,
                    "WeaponRuntime");
            Assert.AreEqual(
                new WeaponId("knife"),
                runtime.Snapshot.EquippedWeaponId);

            Type toggleInputType =
                Type.GetType(
                    "CGame.WeaponAnimationTestInput, Assembly-CSharp");
            Assert.NotNull(toggleInputType);
            Component toggleInput =
                (Component)UnityEngine.Object
                    .FindObjectOfType(toggleInputType);
            if (toggleInput == null)
            {
                var toggleObject =
                    new GameObject("[WeaponAnimationTestInput]");
                toggleInput =
                    toggleObject.AddComponent(toggleInputType);
            }

            MethodInfo updateMethod =
                toggleInputType.GetMethod(
                    "Update",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic);
            Assert.NotNull(updateMethod);
            Keyboard virtualKeyboard =
                InputSystem.AddDevice<Keyboard>(
                    "WeaponSwitchTestKeyboard");
            try
            {
                PressToggleKey(
                    virtualKeyboard,
                    toggleInput,
                    updateMethod);
                yield return WaitForEquippedWeapon(
                    runtime,
                    new WeaponId("rifle"),
                    10f);
                Assert.AreEqual(
                    new WeaponId("rifle"),
                    runtime.Snapshot.EquippedWeaponId);

                PressToggleKey(
                    virtualKeyboard,
                    toggleInput,
                    updateMethod);
                yield return WaitForEquippedWeapon(
                    runtime,
                    new WeaponId("knife"),
                    10f);
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);
            }
            finally
            {
                InputSystem.RemoveDevice(virtualKeyboard);
            }
        }

        [UnityTest]
        public IEnumerator ActualYooAssetSpawn_KnifeToAkCoversLocomotionActionsSwitchAndRetry()
        {
            launcherObject =
                new GameObject("KnifeToAkFullChainLauncher");
            Type launcherManagerType =
                Type.GetType(
                    "CGame.GameLauncherMgr, Assembly-CSharp");
            Assert.NotNull(launcherManagerType);
            launcherObject.AddComponent(launcherManagerType);

            GameObject character = null;
            float startupTimeout = 20f;
            while (character == null && startupTimeout > 0f)
            {
                character = GameObject.Find("RuntimeCharacter");
                yield return null;
                startupTimeout -= Time.deltaTime;
            }

            Assert.Greater(
                startupTimeout,
                0f,
                "The actual YooAsset launch did not publish RuntimeCharacter.");
            Assert.IsTrue(ResourceManager.Instance.IsReady);
            Assert.IsFalse(
                AssetManager.Instance.CheckLocation(
                    "WeaponAnimationCatalog"));
            Assert.IsTrue(
                AssetManager.Instance.CheckLocation(
                    "KnifeWeaponAnimationDefinition"));
            Assert.IsTrue(
                AssetManager.Instance.CheckLocation(
                    "RifleAKAnimationDefinition"));

            yield return VerifyActualYooAssetDefinitions();

            Animator animator =
                character.GetComponentInChildren<Animator>();
            Assert.NotNull(animator);
            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer renderer
                     in character.GetComponentsInChildren<
                         SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen = true;
            }

            var input = new PlayerInputTestDriver();
            input.Bind(character);
            WeaponRuntime runtime =
                GetProperty<WeaponRuntime>(
                    input.Controller,
                    "WeaponRuntime");
            Assert.NotNull(runtime);
            Assert.AreEqual(
                new WeaponId("knife"),
                runtime.Snapshot.EquippedWeaponId);
            Assert.AreEqual(
                new WeaponRuntimeCapabilities(
                    false,
                    false,
                    true),
                runtime.Capabilities);

            var actionFacts =
                new List<WeaponActionFact>();
            var switchFacts =
                new List<WeaponSwitchFact>();
            runtime.ActionChanged += actionFacts.Add;
            runtime.SwitchChanged += switchFacts.Add;

            using (var evidence =
                   new FullChainEvidenceCapture(
                       character,
                       animator,
                       runtime,
                       EvidenceDirectoryName))
            {
                yield return CaptureState(
                    evidence,
                    animator,
                    runtime,
                    "01-knife-idle");
                AssertUpperBodyGraph(
                    animator,
                    evidence.DescribeState(
                        "initial-debug"));

                yield return DriveDirection(
                    input,
                    evidence,
                    animator,
                    runtime,
                    new Vector2(0f, 0.7f),
                    "02-knife-forward");
                Assert.IsTrue(animator.GetBool("Moving"));
                Assert.Greater(animator.GetFloat("MoveY"), 0.15f);

                yield return DriveDirection(
                    input,
                    evidence,
                    animator,
                    runtime,
                    new Vector2(0.7f, 0f),
                    "03-knife-right");
                Assert.Greater(animator.GetFloat("MoveX"), 0.15f);

                input.SetMove(Vector2.up);
                input.SetSprintHeld(true);
                yield return WaitForSprinting(
                    evidence,
                    animator,
                    "04-knife-sprint");
                Assert.Greater(
                    animator.GetFloat("Sprinting"),
                    0.5f);

                yield return JumpFallAndLand(
                    input,
                    evidence,
                    animator,
                    runtime,
                    "knife");

                input.ReleaseAll();
                Assert.IsTrue(
                    runtime.RequestPrimaryAction(
                        out WeaponActionFact melee));
                Assert.AreEqual(
                    WeaponActionKind.MeleeAttack,
                    melee.Kind);
                yield return RecordFrames(
                    evidence,
                    8,
                    "08-knife-melee");
                yield return WaitForActionEnd(
                    runtime,
                    evidence,
                    10f);
                Assert.IsTrue(
                    actionFacts.Any(fact =>
                        fact.ActionId == melee.ActionId
                        && fact.Phase
                        == WeaponActionPhase.Completed));

                Assert.AreEqual(
                    WeaponSwitchRequestResult.Started,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("missing"),
                        out WeaponSwitchFact missing));
                yield return WaitForSwitchEnd(
                    runtime,
                    evidence,
                    10f);
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.IsTrue(
                    switchFacts.Any(fact =>
                        fact.SwitchId == missing.SwitchId
                        && fact.Phase
                        == WeaponSwitchPhase.Failed
                        && fact.EndReason
                        == WeaponSwitchEndReason
                            .TargetLoadFailed));
                yield return CaptureState(
                    evidence,
                    animator,
                    runtime,
                    "09-load-failure-keeps-knife");

                input.SetMove(new Vector2(0f, 0.35f));
                Assert.AreEqual(
                    WeaponSwitchRequestResult.Started,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("rifle"),
                        out WeaponSwitchFact rifleSwitch));
                Assert.IsFalse(
                    runtime.RequestPrimaryAction(out _));
                Assert.IsFalse(runtime.RequestReload(out _));
                Assert.AreEqual(
                    WeaponSwitchRequestResult
                        .AlreadySwitching,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("knife"),
                        out _));
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);

                yield return RecordFrames(
                    evidence,
                    8,
                    "10-knife-unequip");
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);
                yield return WaitForSwitchEnd(
                    runtime,
                    evidence,
                    15f,
                    "11-ak-overlay-equip");

                Assert.AreEqual(
                    new WeaponId("rifle"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.AreEqual(
                    new WeaponRuntimeCapabilities(
                        true,
                        true,
                        false),
                    runtime.Capabilities);
                Assert.IsTrue(
                    switchFacts.Any(fact =>
                        fact.SwitchId
                        == rifleSwitch.SwitchId
                        && fact.Phase
                        == WeaponSwitchPhase.Completed));
                input.ReleaseAll();
                yield return WaitForIdle(
                    evidence,
                    animator);
                yield return CaptureState(
                    evidence,
                    animator,
                    runtime,
                    "12-ak-idle");

                yield return DriveDirection(
                    input,
                    evidence,
                    animator,
                    runtime,
                    new Vector2(0f, -0.7f),
                    "13-ak-backward");
                Assert.Less(animator.GetFloat("MoveY"), -0.15f);

                yield return DriveDirection(
                    input,
                    evidence,
                    animator,
                    runtime,
                    new Vector2(-0.7f, 0f),
                    "14-ak-left");
                Assert.Less(animator.GetFloat("MoveX"), -0.15f);

                input.SetMove(Vector2.up);
                input.SetSprintHeld(true);
                yield return WaitForSprinting(
                    evidence,
                    animator,
                    "15-ak-sprint");
                Assert.Greater(
                    animator.GetFloat("Sprinting"),
                    0.5f);
                yield return JumpFallAndLand(
                    input,
                    evidence,
                    animator,
                    runtime,
                    "ak");

                input.ReleaseAll();
                Assert.IsTrue(
                    runtime.RequestPrimaryAction(
                        out WeaponActionFact fire));
                Assert.AreEqual(
                    WeaponActionKind.Fire,
                    fire.Kind);
                yield return RecordFrames(
                    evidence,
                    6,
                    "19-ak-fire");
                yield return WaitForActionEnd(
                    runtime,
                    evidence,
                    5f);
                Assert.IsTrue(
                    actionFacts.Any(fact =>
                        fact.ActionId == fire.ActionId
                        && fact.Phase
                        == WeaponActionPhase.Completed));

                Assert.IsTrue(
                    runtime.RequestReload(
                        out WeaponActionFact reload));
                Assert.AreEqual(
                    WeaponActionKind.Reload,
                    reload.Kind);
                yield return RecordFrames(
                    evidence,
                    10,
                    "20-ak-reload");
                yield return WaitForActionEnd(
                    runtime,
                    evidence,
                    15f);
                Assert.IsTrue(
                    actionFacts.Any(fact =>
                        fact.ActionId == reload.ActionId
                        && fact.Phase
                        == WeaponActionPhase.Completed));

                input.ReleaseAll();
                yield return CaptureState(
                    evidence,
                    animator,
                    runtime,
                    "21-ak-restored-idle");
                evidence.WriteTrace(
                    actionFacts,
                    switchFacts);

                Assert.GreaterOrEqual(evidence.FrameCount, 100);
                Assert.AreEqual(
                    2,
                    animator.playableGraph.GetOutputCount());
                AssertUpperBodyGraph(animator);
            }

            input.ReleaseAll();
            Assert.IsFalse(runtime.IsSwitching);
            Assert.IsFalse(runtime.ActiveAction.IsValid);
        }

        private static IEnumerator VerifyActualYooAssetDefinitions()
        {
            IWeaponAnimationDefinitionLocationResolver resolver =
                new WeaponAnimationDefinitionLocationResolver();
            foreach (WeaponId weaponId in new[]
                     {
                         new WeaponId("knife"),
                         new WeaponId("rifle"),
                     })
            {
                Assert.IsTrue(resolver.TryResolveLocation(
                    weaponId,
                    out string location));
                AssetHandle handle =
                    AssetManager.Instance
                        .LoadAsset<WeaponAnimationDefinition>(
                            location);
                try
                {
                    float timeout = 10f;
                    while (!handle.IsDone && timeout > 0f)
                    {
                        yield return null;
                        timeout -= Time.deltaTime;
                    }

                    Assert.Greater(timeout, 0f);
                    Assert.AreEqual(
                        EOperationStatus.Succeed,
                        handle.Status);
                    WeaponAnimationDefinition definition =
                        handle.GetAssetObject<
                            WeaponAnimationDefinition>();
                    Assert.NotNull(definition);
                    Assert.AreEqual(
                        WeaponAnimationDefinitionError.None,
                        definition.Validate(weaponId));
                    if (weaponId == new WeaponId("rifle"))
                    {
                        Assert.AreEqual(
                            "A_FP_AKX_Fire",
                            definition.Fire.AnimationClip.name);
                    }
                }
                finally
                {
                    handle.Release();
                }
            }
        }

        private static IEnumerator DriveDirection(
            PlayerInputTestDriver input,
            FullChainEvidenceCapture evidence,
            Animator animator,
            WeaponRuntime runtime,
            Vector2 direction,
            string label)
        {
            input.SetSprintHeld(false);
            input.SetMove(direction);
            yield return RecordFrames(
                evidence,
                10,
                label);
            Assert.IsTrue(animator.GetBool("Moving"));
            Assert.IsFalse(runtime.IsSwitching);
        }

        private static IEnumerator JumpFallAndLand(
            PlayerInputTestDriver input,
            FullChainEvidenceCapture evidence,
            Animator animator,
            WeaponRuntime runtime,
            string prefix)
        {
            input.SetSprintHeld(false);
            input.SetMove(new Vector2(0f, 0.25f));
            yield return new WaitForFixedUpdate();
            float groundY =
                evidence.Character.position.y;
            input.SetJumpPressed(true);
            yield return null;
            input.SetJumpPressed(false);

            float timeout = 5f;
            int waitTicks = 0;
            while (!animator.GetBool("InAir")
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (waitTicks++ % 4 == 0)
                {
                    yield return evidence.Capture();
                }

                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            while (evidence.Character.position.y
                       <= groundY + 0.1f
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (waitTicks++ % 4 == 0)
                {
                    yield return evidence.Capture();
                }

                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            Assert.Greater(
                evidence.Character.position.y,
                groundY + 0.1f);
            yield return evidence.Capture(
                prefix == "knife"
                    ? "05-knife-jump"
                    : "16-ak-jump");

            float highestY =
                evidence.Character.position.y;
            bool fallingObserved = false;
            Vector3 previous =
                evidence.Character.position;
            waitTicks = 0;
            timeout = 6f;
            while (animator.GetBool("InAir")
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (waitTicks++ % 4 == 0)
                {
                    yield return evidence.Capture();
                }

                highestY = Mathf.Max(
                    highestY,
                    evidence.Character.position.y);
                if (!fallingObserved
                    && evidence.Character.position.y
                    < previous.y - 0.002f)
                {
                    fallingObserved = true;
                    yield return evidence.Capture(
                        prefix == "knife"
                            ? "06-knife-in-air"
                            : "17-ak-in-air");
                }

                previous =
                    evidence.Character.position;
                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            Assert.IsTrue(fallingObserved);
            Assert.Greater(highestY, groundY + 0.5f);
            Assert.AreEqual(
                groundY,
                evidence.Character.position.y,
                0.08f);
            yield return evidence.Capture(
                prefix == "knife"
                    ? "07-knife-land"
                    : "18-ak-land");
            Assert.IsFalse(animator.GetBool("InAir"));
            Assert.IsFalse(runtime.IsSwitching);
        }

        private static IEnumerator WaitForActionEnd(
            WeaponRuntime runtime,
            FullChainEvidenceCapture evidence,
            float timeout)
        {
            int waitTicks = 0;
            while (runtime.ActiveAction.IsValid
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (waitTicks++ % 6 == 0)
                {
                    yield return evidence.Capture();
                }

                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            Assert.IsFalse(runtime.ActiveAction.IsValid);
        }

        private static IEnumerator WaitForSwitchEnd(
            WeaponRuntime runtime,
            FullChainEvidenceCapture evidence,
            float timeout,
            string label = null)
        {
            bool labelled = false;
            int waitTicks = 0;
            while (runtime.IsSwitching
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (!labelled
                    && label != null
                    && evidence.SwitchStage
                    == WeaponSwitchAnimationStage
                        .WaitingForTarget)
                {
                    yield return evidence.Capture(label);
                    labelled = true;
                }
                else if (waitTicks++ % 4 == 0)
                {
                    yield return evidence.Capture();
                }

                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            Assert.IsFalse(runtime.IsSwitching);
            if (label != null)
            {
                Assert.IsTrue(
                    labelled,
                    "The target Overlay+Equip stage was not observed.");
            }
        }

        private static IEnumerator RecordFrames(
            FullChainEvidenceCapture evidence,
            int count,
            string label)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return evidence.Capture(
                    i == count / 2
                        ? label
                        : null);
            }
        }

        private static IEnumerator WaitForSprinting(
            FullChainEvidenceCapture evidence,
            Animator animator,
            string label)
        {
            float timeout = 5f;
            int waitTicks = 0;
            while (animator.GetFloat("Sprinting") <= 0.6f
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (waitTicks++ % 3 == 0)
                {
                    yield return evidence.Capture();
                }

                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            yield return evidence.Capture(label);
        }

        private static IEnumerator WaitForIdle(
            FullChainEvidenceCapture evidence,
            Animator animator)
        {
            float timeout = 3f;
            int waitTicks = 0;
            while ((animator.GetBool("Moving")
                    || animator.GetFloat("Velocity")
                    > 0.05f)
                   && timeout > 0f)
            {
                yield return new WaitForFixedUpdate();
                if (waitTicks++ % 4 == 0)
                {
                    yield return evidence.Capture();
                }

                timeout -= Time.deltaTime;
            }

            Assert.Greater(timeout, 0f);
            Assert.IsFalse(animator.GetBool("Moving"));
        }

        private static IEnumerator CaptureState(
            FullChainEvidenceCapture evidence,
            Animator animator,
            WeaponRuntime runtime,
            string label)
        {
            yield return evidence.Capture(label);
            Assert.AreEqual(
                2,
                animator.playableGraph.GetOutputCount());
            Assert.IsTrue(runtime.IsInitialized);
        }

        private static void AssertUpperBodyGraph(
            Animator animator,
            string message = null)
        {
            PlayableGraph graph = animator.playableGraph;
            var master =
                (AnimationLayerMixerPlayable)graph
                    .GetOutput(1)
                    .GetSourcePlayable();
            var overrideMixer =
                (AnimationLayerMixerPlayable)
                    master.GetInput(1);
            var slotMixer =
                (AnimationLayerMixerPlayable)
                    overrideMixer.GetInput(0);
            var overlayMixer =
                (AnimationLayerMixerPlayable)
                    slotMixer.GetInput(0);
            Assert.AreEqual(
                1f,
                master.GetInputWeight(1),
                message);
            Assert.Greater(
                overlayMixer.GetInputWeight(0),
                0.99f,
                message);
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

        private static void PressToggleKey(
            Keyboard keyboard,
            Component toggleInput,
            MethodInfo updateMethod)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.T));
            InputSystem.Update();
            updateMethod.Invoke(toggleInput, null);
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState());
            InputSystem.Update();
        }

        private static IEnumerator WaitForEquippedWeapon(
            WeaponRuntime runtime,
            WeaponId expectedWeaponId,
            float timeout)
        {
            while (runtime.Snapshot.EquippedWeaponId
                       != expectedWeaponId
                   && timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;
            }

            Assert.Greater(
                timeout,
                0f,
                $"Timed out switching to {expectedWeaponId}.");
        }

        private static void ReturnLauncherToEmptyState()
        {
            try
            {
                Type launcherType =
                    Type.GetType(
                        "CGame.GameLauncher, Assembly-CSharp");
                object launcher =
                    launcherType?
                        .GetProperty(
                            "Instance",
                            BindingFlags.Public
                            | BindingFlags.Static)
                        ?.GetValue(null);
                launcherType?
                    .GetMethod(
                        "ReturnLoginPanel",
                        BindingFlags.Instance
                        | BindingFlags.Public)
                    ?.Invoke(launcher, null);
            }
            catch
            {
            }
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

        private sealed class FullChainEvidenceCapture :
            IDisposable
        {
            private readonly Camera camera;
            private readonly Light light;
            private readonly RenderTexture target;
            private readonly Camera[] sceneCameras;
            private readonly bool[] cameraStates;
            private readonly string directory;
            private readonly string frameDirectory;
            private readonly string keyDirectory;
            private readonly Animator animator;
            private readonly WeaponRuntime runtime;
            private readonly WeaponAnimationSequencer sequencer;
            private readonly List<string> stateSamples =
                new List<string>();
            private readonly Vector3 cameraOffset =
                new Vector3(2.6f, 1.45f, 3.2f);
            private readonly Vector3 lookOffset =
                new Vector3(0f, 0.9f, 0f);

            public FullChainEvidenceCapture(
                GameObject character,
                Animator animator,
                WeaponRuntime runtime,
                string evidenceDirectoryName)
            {
                Character = character.transform;
                this.animator = animator;
                this.runtime = runtime;
                sequencer = ResolveSequencer(character);
                var cameraObject =
                    new GameObject(
                        "Knife To AK Evidence Camera");
                var lightObject =
                    new GameObject(
                        "Knife To AK Evidence Light");
                camera =
                    cameraObject.AddComponent<Camera>();
                light =
                    lightObject.AddComponent<Light>();
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
                foreach (Camera sceneCamera
                         in sceneCameras)
                {
                    if (sceneCamera != camera)
                    {
                        sceneCamera.enabled = false;
                    }
                }

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
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                frameDirectory =
                    Path.Combine(directory, "frames");
                keyDirectory =
                    Path.Combine(directory, "keyframes");
                Directory.CreateDirectory(frameDirectory);
                Directory.CreateDirectory(keyDirectory);
            }

            public Transform Character { get; }
            public int FrameCount { get; private set; }
            public WeaponSwitchAnimationStage
                SwitchStage =>
                sequencer?.SwitchStage
                ?? WeaponSwitchAnimationStage.None;
            public string DescribeState(string label) =>
                BuildStateSample(label);

            public IEnumerator Capture(
                string keyLabel = null)
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
                    byte[] bytes =
                        texture.EncodeToPNG();
                    string frameName =
                        $"frame-{FrameCount:0000}.png";
                    File.WriteAllBytes(
                        Path.Combine(
                            frameDirectory,
                            frameName),
                        bytes);
                    if (!string.IsNullOrWhiteSpace(
                            keyLabel))
                    {
                        File.WriteAllBytes(
                            Path.Combine(
                                keyDirectory,
                                keyLabel + ".png"),
                            bytes);
                        stateSamples.Add(
                            BuildStateSample(
                                keyLabel));
                    }

                    FrameCount++;
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.Destroy(texture);
                }
            }

            public void WriteTrace(
                IReadOnlyList<WeaponActionFact> actions,
                IReadOnlyList<WeaponSwitchFact> switches)
            {
                var lines = new List<string>
                {
                    "{",
                    "  \"finalWeaponId\": \"rifle\",",
                    $"  \"frameCount\": {FrameCount},",
                    "  \"actions\": [",
                };
                for (int i = 0; i < actions.Count; i++)
                {
                    WeaponActionFact fact = actions[i];
                    lines.Add(
                        "    {"
                        + $"\"actionId\":{fact.ActionId},"
                        + $"\"generation\":{fact.Generation},"
                        + $"\"weaponId\":\"{fact.WeaponId.Value}\","
                        + $"\"kind\":\"{fact.Kind}\","
                        + $"\"phase\":\"{fact.Phase}\","
                        + $"\"endReason\":\"{fact.EndReason}\""
                        + "}"
                        + (i + 1 < actions.Count
                            ? ","
                            : string.Empty));
                }

                lines.Add("  ],");
                lines.Add("  \"switches\": [");
                for (int i = 0; i < switches.Count; i++)
                {
                    WeaponSwitchFact fact = switches[i];
                    lines.Add(
                        "    {"
                        + $"\"switchId\":{fact.SwitchId},"
                        + $"\"from\":\"{fact.FromWeaponId.Value}\","
                        + $"\"to\":\"{fact.ToWeaponId.Value}\","
                        + $"\"phase\":\"{fact.Phase}\","
                        + $"\"endReason\":\"{fact.EndReason}\""
                        + "}"
                        + (i + 1 < switches.Count
                            ? ","
                            : string.Empty));
                }

                lines.Add("  ],");
                lines.Add("  \"samples\": [");
                for (int i = 0;
                     i < stateSamples.Count;
                     i++)
                {
                    lines.Add(
                        "    "
                        + stateSamples[i]
                        + (i + 1
                           < stateSamples.Count
                            ? ","
                            : string.Empty));
                }

                lines.Add("  ]");
                lines.Add("}");
                string tracePath =
                    Path.Combine(
                        directory,
                        "runtime-trace.json");
                File.WriteAllLines(tracePath, lines);
                TestContext.Out.WriteLine(directory);
                TestContext.Out.WriteLine(tracePath);
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
                UnityEngine.Object.Destroy(
                    light.gameObject);
            }

            private void AlignCamera()
            {
                camera.transform.position =
                    Character.position
                    + cameraOffset;
                camera.transform.LookAt(
                    Character.position
                    + lookOffset);
            }

            private string BuildStateSample(
                string label)
            {
                PlayableGraph graph =
                    animator.playableGraph;
                var master =
                    (AnimationLayerMixerPlayable)
                        graph.GetOutput(1)
                            .GetSourcePlayable();
                var overrideMixer =
                    (AnimationLayerMixerPlayable)
                        master.GetInput(1);
                var slotMixer =
                    (AnimationLayerMixerPlayable)
                        overrideMixer.GetInput(0);
                var overlayMixer =
                    (AnimationLayerMixerPlayable)
                        slotMixer.GetInput(0);
                int slotConnections = 0;
                for (int i = 1;
                     i < slotMixer.GetInputCount();
                     i++)
                {
                    if (slotMixer.GetInput(i)
                        .IsValid())
                    {
                        slotConnections++;
                    }
                }

                WeaponAnimationDefinition targetDefinition =
                    sequencer == null
                        ? null
                        : sequencer.GetType()
                            .GetField(
                                "targetDefinition",
                                BindingFlags.Instance
                                | BindingFlags.NonPublic)
                            ?.GetValue(sequencer)
                            as WeaponAnimationDefinition;
                return "{"
                    + $"\"frame\":{FrameCount},"
                    + $"\"label\":\"{label}\","
                    + $"\"weaponId\":\"{runtime.Snapshot.EquippedWeaponId.Value}\","
                    + $"\"generation\":{runtime.Snapshot.Generation},"
                    + $"\"actionId\":{runtime.ActiveAction.ActionId},"
                    + $"\"switchId\":{runtime.ActiveSwitch.SwitchId},"
                    + $"\"moveX\":{animator.GetFloat("MoveX").ToString("R", System.Globalization.CultureInfo.InvariantCulture)},"
                    + $"\"moveY\":{animator.GetFloat("MoveY").ToString("R", System.Globalization.CultureInfo.InvariantCulture)},"
                    + $"\"moving\":{animator.GetBool("Moving").ToString().ToLowerInvariant()},"
                    + $"\"inAir\":{animator.GetBool("InAir").ToString().ToLowerInvariant()},"
                    + $"\"sprinting\":{animator.GetFloat("Sprinting").ToString("R", System.Globalization.CultureInfo.InvariantCulture)},"
                    + $"\"masterWeight\":{master.GetInputWeight(1).ToString("R", System.Globalization.CultureInfo.InvariantCulture)},"
                    + $"\"overlayWeight\":{overlayMixer.GetInputWeight(0).ToString("R", System.Globalization.CultureInfo.InvariantCulture)},"
                    + $"\"slotConnections\":{slotConnections},"
                    + $"\"switchStage\":\"{sequencer?.SwitchStage.ToString() ?? "Unavailable"}\","
                    + $"\"currentDefinition\":\"{sequencer?.CurrentDefinition?.WeaponId.Value ?? string.Empty}\","
                    + $"\"targetDefinition\":\"{targetDefinition?.WeaponId.Value ?? string.Empty}\","
                    + $"\"actionHandle\":\"{DescribeHandle(sequencer?.CurrentHandle)}\","
                    + $"\"overlayHandle\":\"{DescribeHandle(sequencer?.CurrentOverlayHandle)}\","
                    + $"\"unequipHandle\":\"{DescribeHandle(GetHandle("UnequipHandle"))}\","
                    + $"\"targetOverlayHandle\":\"{DescribeHandle(GetHandle("TargetOverlayHandle"))}\","
                    + $"\"targetEquipHandle\":\"{DescribeHandle(GetHandle("TargetEquipHandle"))}\""
                    + "}";
            }

            private AnimationPlaybackHandle GetHandle(
                string propertyName)
            {
                return sequencer?.GetType()
                    .GetProperty(
                        propertyName,
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic)
                    ?.GetValue(sequencer)
                    as AnimationPlaybackHandle;
            }

            private static string DescribeHandle(
                AnimationPlaybackHandle handle)
            {
                return handle == null
                    ? string.Empty
                    : handle.PlaybackId
                      + ":"
                      + handle.RequestId
                      + ":"
                      + handle.State;
            }

            private static WeaponAnimationSequencer
                ResolveSequencer(
                    GameObject character)
            {
                Type pawnHostType =
                    Type.GetType(
                        "CGame.PawnHost, Assembly-CSharp");
                Component pawnHost =
                    character.GetComponent(pawnHostType);
                object pawn = pawnHostType
                    ?.GetProperty(
                        "Pawn",
                        BindingFlags.Instance
                        | BindingFlags.Public)
                        ?.GetValue(pawnHost);
                FieldInfo componentsField = null;
                for (Type type = pawn?.GetType();
                     type != null
                     && componentsField == null;
                     type = type.BaseType)
                {
                    componentsField = type.GetField(
                        "components",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly);
                }

                var components = componentsField?
                    .GetValue(pawn)
                    as System.Collections.IEnumerable;
                if (components == null)
                {
                    return null;
                }

                foreach (object component
                         in components)
                {
                    if (component?.GetType().Name
                        != "CharacterAnimationComponent")
                    {
                        continue;
                    }

                    object animInstance = component
                        .GetType()
                        .GetField(
                            "animInstance",
                            BindingFlags.Instance
                            | BindingFlags.NonPublic)
                        ?.GetValue(component);
                    return animInstance?
                        .GetType()
                        .GetField(
                            "weaponSequencer",
                            BindingFlags.Instance
                            | BindingFlags.NonPublic)
                        ?.GetValue(animInstance)
                        as WeaponAnimationSequencer;
                }

                return null;
            }
        }
    }
}
