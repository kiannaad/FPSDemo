using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CGame.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Profiling;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using YooAsset;

namespace CGame.Tests
{
    public class PhysicsInputRuntimeTests
    {
        private readonly PlayerInputTestDriver inputDriver = new PlayerInputTestDriver();
        private object characterTestStep;

        [SetUp]
        public void Setup()
        {
        }

        [UnitySetUp]
        public IEnumerator WaitForCharacterReady()
        {
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            EnsureCharacterStepCreated();
            for (int i = 0; i < 120; i++)
            {
                characterTestStep.GetType().GetMethod("Update").Invoke(characterTestStep, null);
                GameObject character = GameObject.Find("RuntimeCharacter");
                if (character != null)
                {
                    inputDriver.Bind(character);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("CharacterTestStep did not reach CharacterReady within 120 frames.");
        }

        private void EnsureCharacterStepCreated()
        {
            if (characterTestStep != null)
            {
                return;
            }

            Type stepType = Type.GetType("CGame.CharacterTestStep, Assembly-CSharp");
            Assert.IsNotNull(stepType);
            CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            characterTestStep = Activator.CreateInstance(stepType);
            MethodInfo enterMethod = stepType.GetMethod("Enter");
            Assert.IsNotNull(enterMethod);
            enterMethod.Invoke(characterTestStep, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (characterTestStep != null)
            {
                characterTestStep.GetType().GetMethod("Exit").Invoke(characterTestStep, null);
                characterTestStep = null;
            }

            GameObject runtimeRoot = GameObject.Find("[CharacterTestRuntime]");
            if (runtimeRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }

            GameObject gameManager = GameObject.Find("[GameManager]");
            if (gameManager != null)
            {
                UnityEngine.Object.DestroyImmediate(gameManager);
            }

            DestroyImmediateIfPresent("[ObserverAimTestRoot]");
            DestroyImmediateIfPresent("Observer Evidence Camera");

            inputDriver.ReleaseAll();
        }

        [UnityTest]
        public IEnumerator CharacterTestStep_Exit_ReleasesRuntimeCharacter()
        {
            characterTestStep.GetType().GetMethod("Exit").Invoke(characterTestStep, null);
            characterTestStep = null;
            yield return null;

            Assert.IsNull(GameObject.Find("RuntimeCharacter"));
        }

        [UnityTest]
        public IEnumerator RuntimeCharacter_IsOwnedOutsideCharacterTestRoot()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            GameObject testRoot = GameObject.Find("[CharacterTestRuntime]");
            Assert.NotNull(character);
            Assert.NotNull(testRoot);
            Assert.AreEqual("[CharacterRuntimeRoot]", character.transform.parent.name);
            Assert.IsFalse(character.transform.IsChildOf(testRoot.transform));

            UnityEngine.Object.Destroy(testRoot);
            yield return null;

            Assert.NotNull(GameObject.Find("RuntimeCharacter"));
        }

        [UnityTest]
        public IEnumerator HoldingForwardInput_MovesRuntimeCharacter()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.IsNotNull(character);
            Vector3 startingPosition = character.transform.position;

            ScreenCapture.CaptureScreenshot(GetLocomotionCapturePath("idle.png"));
            yield return new WaitForEndOfFrame();

            inputDriver.SetMove(Vector2.up);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
                if (i == 2)
                {
                    ScreenCapture.CaptureScreenshot(GetLocomotionCapturePath("walk.png"));
                    yield return new WaitForEndOfFrame();
                }
            }
            inputDriver.SetSprintHeld(true);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            ScreenCapture.CaptureScreenshot(GetLocomotionCapturePath("sprint.png"));
            yield return new WaitForEndOfFrame();
            inputDriver.ReleaseAll();
            yield return null;

            Assert.Greater(character.transform.position.z, startingPosition.z + 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FullBodyFirstPersonAcceptance_CapturesLocomotionAimFireAndReload()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            Assert.NotNull(inputDriver.Controller);
            Vector3 startingPosition = character.transform.position;
            MethodInfo requestEquip = inputDriver.Controller.GetType().GetMethod("RequestEquipWeapon");
            Assert.NotNull(requestEquip);
            Assert.IsTrue((bool)requestEquip.Invoke(inputDriver.Controller, new object[] { new WeaponId("rifle") }));

            for (int frame = 0; frame < 70; frame++)
            {
                yield return null;
            }

            GameObject firstPersonWeapon = GameObject.Find("RifleAKPresentation[FirstPerson]");
            Assert.NotNull(firstPersonWeapon, "Local player must have a camera-mounted weapon presentation.");
            Assert.AreEqual(Camera.main.transform, firstPersonWeapon.transform.parent);
            WeaponModelActionPlayer firstPersonModelAction =
                firstPersonWeapon.GetComponent<WeaponPresentationInstance>()?.ModelActionPlayer;
            Assert.NotNull(firstPersonModelAction);

            inputDriver.ClearLook();
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(GetFullBodyAcceptanceCapturePath("01-equipped-idle.png"));
            yield return new WaitForEndOfFrame();

            inputDriver.SetMove(Vector2.up);
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            ScreenCapture.CaptureScreenshot(GetFullBodyAcceptanceCapturePath("02-forward-locomotion.png"));
            yield return new WaitForEndOfFrame();

            inputDriver.SetSprintHeld(true);
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            ScreenCapture.CaptureScreenshot(GetFullBodyAcceptanceCapturePath("03-sprint.png"));
            yield return new WaitForEndOfFrame();
            inputDriver.SetSprintHeld(false);

            inputDriver.SetAimHeld(true);
            for (int frame = 0; frame < 120 && Camera.main.fieldOfView > 48.1f; frame++)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(GetFullBodyAcceptanceCapturePath("04-ads.png"));
            yield return new WaitForEndOfFrame();
            WeaponPresentationInstance firstPersonPresentation =
                firstPersonWeapon.GetComponent<WeaponPresentationInstance>();
            Vector3 adsMuzzleViewport = Camera.main.WorldToViewportPoint(
                firstPersonPresentation.Muzzle.position);
            Assert.Greater(adsMuzzleViewport.z, 0f);
            Assert.That(
                adsMuzzleViewport.x,
                Is.EqualTo(0.5f).Within(0.08f),
                $"ADS muzzle must be horizontally centered. viewport={adsMuzzleViewport}");
            Assert.That(
                adsMuzzleViewport.y,
                Is.EqualTo(0.5f).Within(0.08f),
                $"ADS muzzle must be vertically centered. viewport={adsMuzzleViewport}");
            Assert.Greater(
                Vector3.Dot(firstPersonPresentation.Muzzle.forward, Camera.main.transform.forward),
                0.95f,
                "ADS muzzle direction must align with the real Camera.");

            inputDriver.SetFirePressed(true);
            yield return null;
            inputDriver.SetFirePressed(false);
            yield return null;
            ScreenCapture.CaptureScreenshot(GetFullBodyAcceptanceCapturePath("05-fire.png"));
            yield return new WaitForEndOfFrame();

            inputDriver.SetAimHeld(false);
            inputDriver.SetMove(Vector2.zero);
            inputDriver.SetReloadPressed(true);
            yield return null;
            inputDriver.SetReloadPressed(false);
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot(GetFullBodyAcceptanceCapturePath("06-reload.png"));
            yield return new WaitForEndOfFrame();

            Assert.Greater(character.transform.position.z, startingPosition.z + 0.1f);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<Camera>().Length);
            Assert.IsTrue(firstPersonModelAction.IsPlaying, "Reload must animate the first-person weapon model.");
            Assert.That(firstPersonModelAction.NormalizedTime, Is.InRange(0.2f, 0.9f));
            WeaponPresentationInstance presentation = UnityEngine.Object.FindObjectOfType<WeaponPresentationInstance>();
            Assert.NotNull(presentation);
            foreach (Renderer renderer in presentation.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.NotNull(material);
                    Assert.NotNull(material.shader);
                    Assert.IsTrue(material.shader.isSupported, material.name);
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                }
            }

            inputDriver.ReleaseAll();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LookingRightWhileHoldingLeft_MovesAsStrafeAndFacesAimYaw()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.IsNotNull(character);
            Vector3 startingPosition = character.transform.position;
            float groundHeight = startingPosition.y;

            inputDriver.SetLookDelta(new Vector2(90f, 0f));
            yield return null;
            inputDriver.ClearLook();
            inputDriver.SetMove(Vector2.left);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            inputDriver.ReleaseAll();
            yield return null;

            Assert.Greater(Vector3.Dot(character.transform.forward, Vector3.right), 0.9f);
            Assert.Greater(character.transform.position.z, startingPosition.z + 0.05f);
            Assert.AreEqual(groundHeight, character.transform.position.y, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EightDirectionLocomotion_UsesPhysicalLocalVelocityWithoutInputReadsInAnimation()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            Vector2[] directions =
            {
                Vector2.up,
                new Vector2(1f, 1f).normalized,
                Vector2.right,
                new Vector2(1f, -1f).normalized,
                Vector2.down,
                new Vector2(-1f, -1f).normalized,
                Vector2.left,
                new Vector2(-1f, 1f).normalized,
            };

            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Vector3 start = character.transform.position;
                Vector2 input = directions[directionIndex];
                Vector3 expectedWorldDirection = character.transform.TransformDirection(
                    new Vector3(input.x, 0f, input.y)).normalized;
                inputDriver.SetMove(input);
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }

                if (directionIndex % 2 == 0)
                {
                    ScreenCapture.CaptureScreenshot(GetDirectionalLocomotionCapturePath(
                        $"direction-{directionIndex}.png"));
                    yield return new WaitForEndOfFrame();
                }

                inputDriver.ReleaseAll();
                yield return null;
                Vector3 displacement = Vector3.ProjectOnPlane(character.transform.position - start, Vector3.up);
                Assert.Greater(displacement.magnitude, 0.02f, $"Direction {input} did not move the character.");
                Assert.Greater(
                    Vector3.Dot(displacement.normalized, expectedWorldDirection),
                    0.8f,
                    $"Direction {input} disagreed with Character Physics displacement.");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CharacterAnimationRuntime_GameTimeContinuesAndRecoversAfterAnimatorAndControllerChanges()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            Animator animator = character.GetComponentInChildren<Animator>();
            Assert.NotNull(animator);
            CharacterAnimInstance instance = FindCharacterAnimInstance(character);
            Assert.NotNull(instance);

            for (int frame = 0; frame < 3 && animator.playableGraph.GetOutputCount() < 2; frame++)
            {
                yield return null;
            }

            PlayableGraph graph = animator.playableGraph;
            Assert.AreEqual(2, graph.GetOutputCount());
            Playable nativeSource = graph.GetOutput(0).GetSourcePlayable();
            Playable locomotionSource = graph.GetOutput(1).GetSourcePlayable();
            double startTime = locomotionSource.GetTime();
            yield return null;
            yield return null;
            Assert.Greater(locomotionSource.GetTime(), startTime, "GameTime must advance the project-owned locomotion source.");
            Assert.AreEqual(nativeSource, graph.GetOutput(0).GetSourcePlayable());

            Vector3 startLocation = instance.UpdateContext.Location.WorldLocation;
            animator.enabled = false;
            inputDriver.SetMove(Vector2.up);
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
            }

            inputDriver.ReleaseAll();
            Assert.Greater(
                Vector3.Distance(startLocation, instance.UpdateContext.Location.WorldLocation),
                0.01f,
                "Animation data history must keep consuming physical facts while the Animator is disabled.");

            animator.enabled = true;
            for (int frame = 0; frame < 3 && animator.playableGraph.GetOutputCount() < 2; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(2, animator.playableGraph.GetOutputCount());

            RuntimeAnimatorController originalController = animator.runtimeAnimatorController;
            var replacement = new AnimatorOverrideController(originalController);
            animator.runtimeAnimatorController = replacement;
            animator.Rebind();
            for (int frame = 0; frame < 3 && animator.playableGraph.GetOutputCount() < 2; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(2, animator.playableGraph.GetOutputCount());
            Assert.IsTrue(animator.playableGraph.GetOutput(0).GetSourcePlayable().IsValid());
            Assert.IsTrue(animator.playableGraph.GetOutput(1).GetSourcePlayable().IsValid());

            animator.runtimeAnimatorController = originalController;
            animator.Rebind();
            yield return null;
            UnityEngine.Object.Destroy(replacement);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CharacterAnimationRuntime_CapturesIdleDirectionsSprintJumpAndLand()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            string captureDirectory = GetCharacterAnimationRuntimeCapturePath(string.Empty);
            Camera[] existingCameras = Camera.allCameras;
            bool[] existingCameraStates = existingCameras.Select(camera => camera.enabled).ToArray();
            foreach (Camera camera in existingCameras)
            {
                camera.enabled = false;
            }

            var evidenceCameraObject = new GameObject("Character Animation Evidence Camera");
            evidenceCameraObject.transform.SetParent(character.transform, false);
            evidenceCameraObject.transform.localPosition = new Vector3(2.5f, 1.6f, 4f);
            evidenceCameraObject.transform.localRotation = Quaternion.LookRotation(
                new Vector3(0f, 1f, 0f) - evidenceCameraObject.transform.localPosition,
                Vector3.up);
            Camera evidenceCamera = evidenceCameraObject.AddComponent<Camera>();
            evidenceCamera.depth = 100f;
            evidenceCamera.fieldOfView = 60f;
            evidenceCamera.nearClipPlane = 0.01f;
            evidenceCamera.clearFlags = CameraClearFlags.Skybox;

            Animator animator = character.GetComponentInChildren<Animator>(true);
            Assert.NotNull(animator);
            Assert.IsTrue(
                animator.playableGraph.GetOutput(0).GetSourcePlayable().IsValid());
            Assert.IsTrue(
                animator.playableGraph.GetOutput(1).GetSourcePlayable().IsValid());
            Transform leftToes = animator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "Left_Toes");
            Transform rightToes = animator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "Right_Toes");
            Assert.NotNull(leftToes);
            Assert.NotNull(rightToes);
            CapsuleCollider capsule = character.GetComponent<CapsuleCollider>();
            Assert.NotNull(capsule);
            Assert.AreEqual(0f, capsule.center.y - capsule.height * 0.5f, 0.001f,
                "The capsule bottom must define the character ground plane.");
            Transform visualRoot = character.transform.Find("CharacterVisual");
            Assert.NotNull(visualRoot);
            Assert.AreEqual(0f, visualRoot.localPosition.y, 0.001f,
                "The authored visual must not be shifted by imported renderer bounds.");
            float leftToeHeight = character.transform.InverseTransformPoint(leftToes.position).y;
            float rightToeHeight = character.transform.InverseTransformPoint(rightToes.position).y;
            Assert.That(leftToeHeight, Is.InRange(-0.05f, 0.12f));
            Assert.That(rightToeHeight, Is.InRange(-0.05f, 0.12f));
            Component cameraAnchor = character.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().Name == "FirstPersonCameraAnchor");
            Assert.NotNull(cameraAnchor);
            Vector3 cameraPosition = (Vector3)cameraAnchor.GetType()
                .GetProperty("Position", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(cameraAnchor);
            float cameraHeight = character.transform.InverseTransformPoint(cameraPosition).y;
            Assert.That(cameraHeight, Is.InRange(1.45f, 1.75f));
            Assert.Greater(cameraHeight - Mathf.Max(leftToeHeight, rightToeHeight), 1.3f,
                "The first-person eye must remain above the body instead of at the feet.");

            yield return CaptureCharacterAnimationFrame("01-idle.png");

            Vector2[] directions =
            {
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right,
            };
            string[] names =
            {
                "02-forward.png",
                "03-backward.png",
                "04-left.png",
                "05-right.png",
            };
            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                inputDriver.SetMove(directions[directionIndex]);
                for (int frame = 0; frame < 12; frame++)
                {
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }

                CharacterAnimInstance instance = FindCharacterAnimInstance(character);
                Assert.NotNull(instance);
                Vector2 physicalDirection = instance.UpdateContext.Velocity.NormalizedMoveDirection;
                Assert.Greater(physicalDirection.magnitude, 0.9f);
                Assert.Greater(
                    Vector2.Dot(physicalDirection, directions[directionIndex]),
                    0.9f);
                var animatorDirection = new Vector2(
                    animator.GetFloat(Animator.StringToHash("MoveX")),
                    animator.GetFloat(Animator.StringToHash("MoveY")));
                Assert.Greater(animatorDirection.magnitude, 0.65f);
                Assert.Greater(
                    Vector2.Dot(animatorDirection.normalized, physicalDirection.normalized),
                    0.9f);
                Assert.IsTrue(animator.GetBool(Animator.StringToHash("Moving")));
                Quaternion leftToeBeforeSampling = leftToes.localRotation;
                Quaternion rightToeBeforeSampling = rightToes.localRotation;
                for (int frame = 0; frame < 6; frame++)
                {
                    yield return null;
                }

                Assert.Greater(
                    Quaternion.Angle(leftToeBeforeSampling, leftToes.localRotation)
                    + Quaternion.Angle(rightToeBeforeSampling, rightToes.localRotation),
                    1f,
                    $"{names[directionIndex]} locomotion pose did not advance.");
                yield return CaptureCharacterAnimationFrame(names[directionIndex]);
            }

            inputDriver.SetMove(Vector2.up);
            inputDriver.SetSprintHeld(true);
            int sprintHash = Animator.StringToHash("Sprinting");
            for (int frame = 0;
                 frame < 60 && animator.GetFloat(sprintHash) < 0.95f;
                 frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            CharacterAnimInstance sprintInstance = FindCharacterAnimInstance(character);
            Assert.NotNull(sprintInstance);
            Assert.IsTrue(
                sprintInstance.UpdateContext.CharacterState.IsSprinting,
                $"Sprint classification was not reached. speed={sprintInstance.UpdateContext.Velocity.HorizontalSpeed:F3}, "
                + $"grounded={sprintInstance.UpdateContext.CharacterState.IsGrounded}.");
            Assert.Greater(animator.GetFloat(sprintHash), 0.9f);
            yield return CaptureCharacterAnimationFrame("06-sprint.png");
            inputDriver.ReleaseAll();
            for (int frame = 0; frame < 3; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            evidenceCameraObject.transform.SetParent(null, true);
            float groundHeight = character.transform.position.y;
            inputDriver.SetJumpPressed(true);
            yield return null;
            inputDriver.ReleaseAll();
            bool observedJumpClip = false;
            float greatestJumpToePoseDelta = 0f;
            Quaternion leftToeAtJumpStart = leftToes.localRotation;
            Quaternion rightToeAtJumpStart = rightToes.localRotation;
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
                observedJumpClip |= IsPlayingClipWithName(animator, 2, "Jump");
                greatestJumpToePoseDelta = Mathf.Max(
                    greatestJumpToePoseDelta,
                    Quaternion.Angle(leftToeAtJumpStart, leftToes.localRotation)
                    + Quaternion.Angle(rightToeAtJumpStart, rightToes.localRotation));
                if (observedJumpClip
                    && greatestJumpToePoseDelta > 0.25f
                    && character.transform.position.y > groundHeight + 0.05f)
                {
                    break;
                }
            }

            Assert.Greater(character.transform.position.y, groundHeight + 0.05f);
            Assert.IsTrue(animator.GetBool(Animator.StringToHash("InAir")));
            Assert.IsTrue(observedJumpClip,
                "The Generic controller InAir layer never sampled a Jump clip.");
            Assert.Greater(greatestJumpToePoseDelta, 0.25f,
                "The sampled jump clip did not change the rendered foot pose.");
            yield return CaptureCharacterAnimationFrame("07-jump-inair.png");

            for (int frame = 0; frame < 120 && character.transform.position.y > groundHeight + 0.02f; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsFalse(animator.GetBool(Animator.StringToHash("InAir")));
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            float settledLeftToeHeight =
                character.transform.InverseTransformPoint(leftToes.position).y;
            float settledRightToeHeight =
                character.transform.InverseTransformPoint(rightToes.position).y;
            Assert.That(
                Mathf.Min(settledLeftToeHeight, settledRightToeHeight),
                Is.InRange(-0.05f, 0.12f),
                "At least one foot must return to the capsule ground plane after landing.");
            yield return CaptureCharacterAnimationFrame("08-land.png");
            Assert.IsTrue(Directory.Exists(captureDirectory));
            foreach (string fileName in new[]
                     {
                         "01-idle.png",
                         "02-forward.png",
                         "03-backward.png",
                         "04-left.png",
                         "05-right.png",
                         "06-sprint.png",
                         "07-jump-inair.png",
                         "08-land.png",
                     })
            {
                Assert.IsTrue(File.Exists(Path.Combine(captureDirectory, fileName)), fileName);
            }

            for (int cameraIndex = 0; cameraIndex < existingCameras.Length; cameraIndex++)
            {
                if (existingCameras[cameraIndex] != null)
                {
                    existingCameras[cameraIndex].enabled = existingCameraStates[cameraIndex];
                }
            }

            UnityEngine.Object.DestroyImmediate(evidenceCameraObject);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator CaptureCharacterAnimationFrame(string fileName)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(GetCharacterAnimationRuntimeCapturePath(fileName));
            yield return new WaitForEndOfFrame();
        }

        private static bool IsPlayingClipWithName(
            Animator animator,
            int layerIndex,
            string nameFragment)
        {
            return animator.GetCurrentAnimatorClipInfo(layerIndex)
                .Any(clipInfo => clipInfo.clip != null
                    && clipInfo.clip.name.IndexOf(
                        nameFragment,
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [UnityTest]
        public IEnumerator LookingRightWhileHoldingBackward_MovesOppositeAimWithoutTurning()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.IsNotNull(character);
            Vector3 startingPosition = character.transform.position;
            float groundHeight = startingPosition.y;

            inputDriver.SetLookDelta(new Vector2(90f, 0f));
            yield return null;
            inputDriver.ClearLook();
            inputDriver.SetMove(Vector2.down);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            inputDriver.ReleaseAll();
            yield return null;

            Assert.Greater(Vector3.Dot(character.transform.forward, Vector3.right), 0.9f);
            Assert.Less(character.transform.position.x, startingPosition.x - 0.05f);
            Assert.AreEqual(groundHeight, character.transform.position.y, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PressingSpace_JumpsAndReturnsToGround()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.IsNotNull(character);

            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float groundHeight = character.transform.position.y;
            float highestPoint = groundHeight;
            float previousHeight = groundHeight;
            bool capturedJump = false;
            bool capturedFall = false;
            inputDriver.SetJumpPressed(true);
            yield return null;
            inputDriver.ReleaseAll();

            for (int i = 0; i < 80; i++)
            {
                yield return new WaitForFixedUpdate();
                float currentHeight = character.transform.position.y;
                highestPoint = Mathf.Max(highestPoint, currentHeight);
                if (!capturedJump && currentHeight > groundHeight + 0.2f)
                {
                    ScreenCapture.CaptureScreenshot(GetLocomotionCapturePath("jump.png"));
                    capturedJump = true;
                    yield return new WaitForEndOfFrame();
                }

                if (!capturedFall && capturedJump && currentHeight < previousHeight && currentHeight > groundHeight + 0.2f)
                {
                    ScreenCapture.CaptureScreenshot(GetLocomotionCapturePath("fall.png"));
                    capturedFall = true;
                    yield return new WaitForEndOfFrame();
                }

                previousHeight = currentHeight;
            }

            ScreenCapture.CaptureScreenshot(GetLocomotionCapturePath("land.png"));
            yield return new WaitForEndOfFrame();

            Assert.Greater(highestPoint, groundHeight + 0.5f);
            Assert.IsTrue(capturedJump);
            Assert.IsTrue(capturedFall);
            Assert.AreEqual(groundHeight, character.transform.position.y, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator JumpRenderTrajectory_HasNoSingleFrameVerticalPop()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.IsNotNull(character);
            Animator animator = character.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Transform hips = animator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "Pelvis"
                    || transform.name == "Hips");
            Assert.IsNotNull(hips);

            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            float previousCharacterY = character.transform.position.y;
            float previousHipsY = hips.position.y;
            float largestCharacterStep = 0f;
            float largestHipsStep = 0f;
            inputDriver.SetJumpPressed(true);
            yield return null;
            inputDriver.ReleaseAll();

            for (int i = 0; i < 120; i++)
            {
                yield return null;
                float characterStep = character.transform.position.y - previousCharacterY;
                float hipsStep = hips.position.y - previousHipsY;
                largestCharacterStep = Mathf.Max(largestCharacterStep, characterStep);
                largestHipsStep = Mathf.Max(largestHipsStep, hipsStep);
                previousCharacterY = character.transform.position.y;
                previousHipsY = hips.position.y;
            }

            TestContext.WriteLine($"Largest root step: {largestCharacterStep:F4}; largest hips step: {largestHipsStep:F4}");
            Assert.Less(largestCharacterStep, 0.2f);
            Assert.Less(largestHipsStep, 0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstPersonCamera_UsesOneVisibleFullBodyCameraWithOneManualBrain()
        {
            yield return new WaitForEndOfFrame();

            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            Assert.AreEqual(1, cameras.Length);
            Camera worldCamera = Camera.main;
            Assert.NotNull(worldCamera);
            Assert.IsNull(GameObject.Find("ViewModel Overlay Camera"));
            Assert.IsNull(GameObject.Find("First Person ViewModel Prototype"));
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<AudioListener>().Length);

            Type brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            Assert.NotNull(brainType);
            Component brain = worldCamera.GetComponent(brainType);
            Assert.NotNull(brain);
            Assert.AreEqual("ManualUpdate", brainType.GetField("UpdateMethod").GetValue(brain).ToString());
            Assert.IsNull(worldCamera.GetComponent<UnityEngine.InputSystem.PlayerInput>());

            Type additionalCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            Assert.NotNull(additionalCameraDataType);
            Component worldData = worldCamera.GetComponent(additionalCameraDataType);
            Assert.NotNull(worldData);
            Assert.AreEqual("Base", additionalCameraDataType.GetProperty("renderType").GetValue(worldData).ToString());
            var cameraStack = (IList)additionalCameraDataType.GetProperty("cameraStack").GetValue(worldData);
            Assert.AreEqual(0, cameraStack.Count);

            int ownerWorldBodyLayer = LayerMask.NameToLayer("LocalOwnerWorldBody");
            Assert.GreaterOrEqual(ownerWorldBodyLayer, 0);
            Assert.AreNotEqual(0, worldCamera.cullingMask & (1 << ownerWorldBodyLayer));

            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0);
            foreach (Renderer renderer in renderers)
            {
                Assert.AreNotEqual(ownerWorldBodyLayer, renderer.gameObject.layer);
                Assert.AreNotEqual(0, worldCamera.cullingMask & (1 << renderer.gameObject.layer));
                Assert.IsTrue(renderer.enabled);
            }

            foreach (Collider collider in character.GetComponentsInChildren<Collider>(true))
            {
                Assert.AreNotEqual(ownerWorldBodyLayer, collider.gameObject.layer,
                    "Gameplay collider objects must retain their physics layer.");
                Assert.IsTrue(collider.enabled);
            }

            Animator animator = character.GetComponentInChildren<Animator>(true);
            Assert.NotNull(animator);
            Assert.IsTrue(animator.enabled);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstPersonFraming_FollowsTheAnimatedHeadAndKeepsBothHandsVisible()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            Camera worldCamera = Camera.main;
            Assert.NotNull(worldCamera);

            Transform[] bones =
                character.GetComponentsInChildren<Transform>(true);
            Transform leftHand = Array.Find(
                bones,
                transform => transform.name == "Left_Hand");
            Transform rightHand = Array.Find(
                bones,
                transform => transform.name == "Right_Hand");
            Transform head = Array.Find(
                bones,
                transform => transform.name == "Head");
            Assert.NotNull(leftHand);
            Assert.NotNull(rightHand);
            Assert.NotNull(head);

            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.Greater(
                head.localScale.sqrMagnitude,
                0.5f,
                "First-person visibility must never collapse the animated Head bone.");
            SkinnedMeshRenderer ownerRenderer =
                character.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.NotNull(ownerRenderer);
            Assert.IsFalse(
                ownerRenderer.sharedMesh.name.Contains(
                    "(First Person Headless)"),
                "The original full-body Mesh must be restored after the world Camera finishes rendering.");

            Vector3 referenceMountOffset =
                new Vector3(0.012f, 0.059f, 0.024f);
            Vector3 idleMountPosition =
                head.position + head.rotation * referenceMountOffset;
            Assert.Less(
                Vector3.Distance(
                    worldCamera.transform.position,
                    idleMountPosition),
                0.03f,
                "The camera must sample the reference Head/Camera mount.");
            Assert.AreEqual(90f, worldCamera.fieldOfView, 0.1f);

            Vector3 leftViewport =
                worldCamera.WorldToViewportPoint(leftHand.position);
            Vector3 rightViewport =
                worldCamera.WorldToViewportPoint(rightHand.position);
            Assert.That(leftViewport.x, Is.InRange(0f, 1f));
            Assert.That(leftViewport.y, Is.InRange(0f, 1f));
            Assert.Greater(leftViewport.z, worldCamera.nearClipPlane);
            Assert.That(rightViewport.x, Is.InRange(0f, 1f));
            Assert.That(rightViewport.y, Is.InRange(0f, 1f));
            Assert.Greater(rightViewport.z, worldCamera.nearClipPlane);
            Type presentationDriverType =
                FindRuntimeType(
                    "CGame.SingleCameraPresentationDriver");
            Component presentationDriver =
                worldCamera.GetComponent(
                    presentationDriverType);
            Assert.NotNull(presentationDriver);
            Assert.Greater(
                (int)presentationDriverType
                    .GetProperty("OwnerHeadMeshCount")
                    .GetValue(presentationDriver),
                0,
                "The first-person Camera must own at least one headless SkinnedMesh variant.");
            Assert.IsTrue(
                (bool)presentationDriverType
                    .GetProperty(
                        "OwnerHeadWasHiddenForRender")
                    .GetValue(presentationDriver),
                "The owner Head must be hidden before the first-person Camera renders.");

            ScreenCapture.CaptureScreenshot(
                GetFullBodyAcceptanceCapturePath(
                    "00-first-person-framing.png"));
            yield return new WaitForEndOfFrame();

            inputDriver.SetMove(Vector2.up);
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            yield return new WaitForEndOfFrame();
            Vector3 movingMountPosition =
                head.position + head.rotation * referenceMountOffset;
            Assert.Less(
                Vector3.Distance(
                    worldCamera.transform.position,
                    movingMountPosition),
                0.03f,
                "Locomotion must not separate the Camera from the animated Head mount.");
            ScreenCapture.CaptureScreenshot(
                GetFullBodyAcceptanceCapturePath(
                    "00-first-person-moving.png"));
            yield return new WaitForEndOfFrame();
            inputDriver.ReleaseAll();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SingleCamera_RemainsWithoutOverlayDuringRendering()
        {
            for (int frame = 0; frame < 10; frame++)
            {
                yield return new WaitForEndOfFrame();
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            Assert.AreEqual(1, cameras.Length);
            Assert.AreSame(Camera.main, cameras[0]);
            Assert.IsNull(GameObject.Find("ViewModel Overlay Camera"));
            Type additionalCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            Component worldData = Camera.main.GetComponent(additionalCameraDataType);
            var cameraStack = (IList)additionalCameraDataType.GetProperty("cameraStack").GetValue(worldData);
            Assert.AreEqual(0, cameraStack.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstPersonAdsPresentation_UsesOneProgressAndClearsGameplayRejections()
        {
            yield return new WaitForEndOfFrame();

            Type gameManagerType = FindRuntimeType("CGame.GameManager");
            Type cameraManagerType = FindRuntimeType("CGame.CameraManager");
            Assert.NotNull(gameManagerType, "GameManager type missing.");
            Assert.NotNull(cameraManagerType, "CameraManager type missing.");
            MethodInfo getManagerMethod = Array.Find(
                gameManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public),
                method => method.Name == "GetManager" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            Assert.NotNull(getManagerMethod, "GameManager.GetManager<T>() missing.");
            object cameraManager = getManagerMethod.MakeGenericMethod(cameraManagerType).Invoke(null, null);
            Assert.NotNull(cameraManager, "CameraManager instance missing.");
            object adsState = cameraManagerType.GetProperty("AdsPresentationState")?.GetValue(cameraManager);
            object weaponProfile = cameraManagerType.GetProperty("WeaponCameraProfile")?.GetValue(cameraManager);
            Assert.NotNull(adsState, "CameraManager.AdsPresentationState missing.");
            Assert.NotNull(weaponProfile, "CameraManager.WeaponCameraProfile missing.");

            Camera worldCamera = Camera.main;
            Assert.NotNull(worldCamera, "World Camera missing.");
            object cameraOutput = cameraManagerType
                .GetField("output", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(cameraManager);
            Assert.AreSame(
                worldCamera,
                cameraOutput.GetType().GetProperty("WorldCamera").GetValue(cameraOutput),
                "Camera.main must be the CameraManager output Camera.");
            Assert.IsNull(GameObject.Find("ViewModel Overlay Camera"));
            Assert.IsNull(GameObject.Find("First Person ViewModel Prototype"));
            Assert.NotNull(inputDriver.Controller, "Bound PlayerController missing.");

            float hipWorldFov = worldCamera.fieldOfView;
            float adsWorldFov = (float)weaponProfile.GetType().GetProperty("AdsWorldFieldOfView").GetValue(weaponProfile);
            float adsLookMultiplier = (float)weaponProfile.GetType().GetProperty("AdsLookSensitivityMultiplier").GetValue(weaponProfile);

            inputDriver.SetAimHeld(true);
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(0f, ReadFloat(adsState, "AdsProgress"), 0.001f);
            Assert.AreEqual(
                "NoWeapon",
                adsState.GetType()
                    .GetProperty("RejectionReason")
                    .GetValue(adsState)
                    .ToString(),
                "The default knife must reject right-button ADS.");
            inputDriver.SetAimHeld(false);
            yield return null;

            MethodInfo requestEquip =
                inputDriver.Controller.GetType()
                    .GetMethod("RequestEquipWeapon");
            Assert.NotNull(requestEquip);
            Assert.IsTrue((bool)requestEquip.Invoke(
                inputDriver.Controller,
                new object[] { new WeaponId("rifle") }));
            object weaponRuntime =
                inputDriver.Controller.GetType()
                    .GetProperty("WeaponRuntime")
                    .GetValue(inputDriver.Controller);
            object activeSwitch = weaponRuntime.GetType()
                .GetProperty("ActiveSwitch")
                .GetValue(weaponRuntime);
            ulong switchId = (ulong)activeSwitch.GetType()
                .GetProperty("SwitchId")
                .GetValue(activeSwitch);
            Assert.IsTrue((bool)weaponRuntime.GetType()
                .GetMethod("CompleteSwitch")
                .Invoke(
                    weaponRuntime,
                    new object[]
                    {
                        switchId,
                        new WeaponRuntimeCapabilities(
                            true,
                            true,
                            false),
                    }));
            yield return null;

            object finalSnapshot = weaponRuntime.GetType()
                .GetProperty("Snapshot")
                .GetValue(weaponRuntime);
            Assert.AreEqual(
                new WeaponId("rifle"),
                finalSnapshot.GetType()
                    .GetProperty("EquippedWeaponId")
                    .GetValue(finalSnapshot));

            inputDriver.SetAimHeld(true);
            yield return null;
            yield return new WaitForEndOfFrame();
            float midProgress = (float)adsState.GetType().GetProperty("AdsProgress").GetValue(adsState);
            Assert.That(midProgress, Is.InRange(0.001f, 0.999f));
            AssertAdsConsumersMatch(
                midProgress,
                worldCamera,
                inputDriver.Controller,
                hipWorldFov,
                adsWorldFov,
                adsLookMultiplier);

            for (int i = 0; i < 120 && ReadFloat(adsState, "AdsProgress") < 0.999f; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            object fullAdsSnapshot = cameraManagerType.GetProperty("DebugSnapshot").GetValue(cameraManager);
            Assert.IsTrue(
                (bool)fullAdsSnapshot.GetType().GetProperty("HasTarget").GetValue(fullAdsSnapshot),
                "Settled ADS requires the local full-body camera target to remain bound.");
            Assert.AreEqual(
                adsWorldFov,
                ReadFloat(fullAdsSnapshot, "FieldOfView"),
                0.01f,
                "CameraManager snapshot must carry the settled ADS field of view.");
            object presentationDriver = worldCamera.GetComponent(FindRuntimeType("CGame.SingleCameraPresentationDriver"));
            Assert.NotNull(presentationDriver, "Single-camera presentation driver missing.");
            Assert.AreEqual(
                adsWorldFov,
                (float)presentationDriver.GetType()
                    .GetField("fieldOfView", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(presentationDriver),
                0.01f,
                "Presentation driver must retain the settled ADS field of view.");
            AssertAdsConsumersMatch(
                1f,
                worldCamera,
                inputDriver.Controller,
                hipWorldFov,
                adsWorldFov,
                adsLookMultiplier);

            string[] rejectionReasons = { "Reloading", "Sprinting", "Dead", "WeaponSwitching" };
            foreach (string rejectionReason in rejectionReasons)
            {
                SetAimRejectionOverride(rejectionReason);
                for (int i = 0; i < 120 && ReadFloat(adsState, "AdsProgress") > 0.001f; i++)
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }

                Assert.AreEqual(rejectionReason, adsState.GetType().GetProperty("RejectionReason").GetValue(adsState).ToString());
                AssertAdsConsumersMatch(
                    0f,
                    worldCamera,
                    inputDriver.Controller,
                    hipWorldFov,
                    adsWorldFov,
                    adsLookMultiplier);

                characterTestStep.GetType().GetMethod("ClearingAimRejectionOverride").Invoke(characterTestStep, null);
                for (int i = 0; i < 120 && ReadFloat(adsState, "AdsProgress") < 0.999f; i++)
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }
            }

            inputDriver.ReleaseAll();
            for (int i = 0; i < 120 && ReadFloat(adsState, "AdsProgress") > 0.001f; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            AssertAdsConsumersMatch(
                0f,
                worldCamera,
                inputDriver.Controller,
                hipWorldFov,
                adsWorldFov,
                adsLookMultiplier);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstPersonCamera_SamplesPresentedAnchorAndControlRotationInTheSameFrame()
        {
            GameObject character = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(character);
            Type anchorType = Type.GetType("CGame.FirstPersonCameraAnchor, Assembly-CSharp");
            Assert.NotNull(anchorType);
            Component anchor = character.GetComponentInChildren(anchorType, true);
            Assert.NotNull(anchor);
            PropertyInfo anchorPosition = anchorType.GetProperty("Position");
            PropertyInfo usesAnimatedHeadMount = anchorType.GetProperty("UsesAnimatedHeadMount");
            Assert.IsTrue((bool)usesAnimatedHeadMount.GetValue(anchor),
                "The local camera must sample the animated Head mount.");
            Type gameManagerType = FindRuntimeType("CGame.GameManager");
            Type cameraManagerType = FindRuntimeType("CGame.CameraManager");
            MethodInfo getManagerMethod = Array.Find(
                gameManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public),
                method => method.Name == "GetManager" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            object cameraManager = getManagerMethod.MakeGenericMethod(cameraManagerType).Invoke(null, null);
            PropertyInfo debugSnapshotProperty = cameraManagerType.GetProperty("DebugSnapshot");
            float maximumBobWeight = 0f;

            inputDriver.SetLookDelta(new Vector2(90f, -20f));
            yield return null;
            inputDriver.ClearLook();
            yield return new WaitForEndOfFrame();
            inputDriver.SetMove(Vector2.up);
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();

                Camera camera = Camera.main;
                Assert.NotNull(camera);
                Vector3 presentedPosition = (Vector3)anchorPosition.GetValue(anchor);
                object snapshot = debugSnapshotProperty.GetValue(cameraManager);
                object basePose = snapshot.GetType().GetProperty("BasePose").GetValue(snapshot);
                Vector3 basePosition = (Vector3)basePose.GetType().GetProperty("Position").GetValue(basePose);
                Vector3 finalPosition = (Vector3)snapshot.GetType().GetProperty("Position").GetValue(snapshot);
                Assert.That(Vector3.Distance(basePosition, presentedPosition), Is.LessThan(0.001f),
                    "Locomotion effects must preserve the presented Anchor as Base Pose.");
                Assert.That(Vector3.Distance(camera.transform.position, finalPosition), Is.LessThan(0.001f),
                    "Cinemachine output must consume the composed final Pose in the same frame.");

                IEnumerable contributions = (IEnumerable)snapshot.GetType().GetProperty("Contributions").GetValue(snapshot);
                foreach (object contribution in contributions)
                {
                    if (contribution.GetType().GetProperty("Layer").GetValue(contribution).ToString() != "Bob")
                    {
                        continue;
                    }

                    object poseDelta = contribution.GetType().GetProperty("PoseDelta").GetValue(contribution);
                    maximumBobWeight = Mathf.Max(
                        maximumBobWeight,
                        (float)poseDelta.GetType().GetProperty("Weight").GetValue(poseDelta));
                }
            }

            inputDriver.ReleaseAll();
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
            }

            object settledSnapshot = debugSnapshotProperty.GetValue(cameraManager);
            float settledBobWeight = 0f;
            foreach (object contribution in (IEnumerable)settledSnapshot.GetType().GetProperty("Contributions").GetValue(settledSnapshot))
            {
                if (contribution.GetType().GetProperty("Layer").GetValue(contribution).ToString() == "Bob")
                {
                    object poseDelta = contribution.GetType().GetProperty("PoseDelta").GetValue(contribution);
                    settledBobWeight = (float)poseDelta.GetType().GetProperty("Weight").GetValue(poseDelta);
                }
            }

            Assert.Less(maximumBobWeight, 0.001f,
                "An animated Head mount already supplies locomotion motion and must not receive a second synthetic Bob.");
            Assert.Less(settledBobWeight, 0.01f, "Bob must settle after movement stops.");
            Assert.Greater(Vector3.Dot(Camera.main.transform.forward, Vector3.right), 0.9f);
            Assert.Less(Camera.main.transform.forward.y, -0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WeaponRecoil_SeparatesAimCameraAndViewModelAndClearsWithoutResidue()
        {
            yield return new WaitForEndOfFrame();

            Type gameManagerType = FindRuntimeType("CGame.GameManager");
            Type cameraManagerType = FindRuntimeType("CGame.CameraManager");
            Type requestType = FindRuntimeType("CGame.WeaponRecoilRequest");
            MethodInfo getManagerMethod = Array.Find(
                gameManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public),
                method => method.Name == "GetManager" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            object cameraManager = getManagerMethod.MakeGenericMethod(cameraManagerType).Invoke(null, null);
            MethodInfo applyRecoil = cameraManagerType.GetMethod("ApplyingWeaponRecoil");
            MethodInfo clearRecoil = cameraManagerType.GetMethod("ClearingWeaponRecoil");
            PropertyInfo debugSnapshot = cameraManagerType.GetProperty("DebugSnapshot");
            Assert.NotNull(cameraManager);
            Assert.NotNull(applyRecoil);
            Assert.NotNull(clearRecoil);
            Assert.NotNull(inputDriver.Controller);

            Quaternion startingAim = (Quaternion)inputDriver.Controller.GetType()
                .GetProperty("ControlRotation").GetValue(inputDriver.Controller);
            object request = Activator.CreateInstance(
                requestType,
                new Vector2(-2f, 0.4f),
                12f,
                new Vector3(0.01f, 0f, -0.025f),
                new Vector3(-2.5f, 0.4f, 0f),
                new Vector3(0f, -0.02f, -0.08f),
                new Vector3(-5f, 0.8f, 0.3f),
                0.7f,
                24f);

            applyRecoil.Invoke(cameraManager, new[] { request });
            Quaternion kickedAim = (Quaternion)inputDriver.Controller.GetType()
                .GetProperty("ControlRotation").GetValue(inputDriver.Controller);
            Assert.Greater(Quaternion.Angle(startingAim, kickedAim), 0.1f,
                "Gameplay recoil must change the authoritative Controller aim immediately.");

            yield return new WaitForEndOfFrame();
            object snapshot = debugSnapshot.GetValue(cameraManager);
            object basePose = snapshot.GetType().GetProperty("BasePose").GetValue(snapshot);
            Quaternion baseRotation = (Quaternion)basePose.GetType().GetProperty("Rotation").GetValue(basePose);
            Quaternion currentAim = (Quaternion)inputDriver.Controller.GetType()
                .GetProperty("ControlRotation").GetValue(inputDriver.Controller);
            Assert.Less(Quaternion.Angle(baseRotation, currentAim), 0.01f,
                "Base camera pose must sample authoritative aim before presentation recoil.");
            Assert.Greater(Quaternion.Angle(baseRotation, (Quaternion)snapshot.GetType().GetProperty("Rotation").GetValue(snapshot)), 0.1f,
                "Visual recoil must alter only the composed camera pose.");

            float visualWeight = FindContributionWeight(snapshot, "VisualRecoil");
            Assert.Greater(visualWeight, 0f);
            Assert.IsNull(GameObject.Find("First Person ViewModel Prototype"));
            ScreenCapture.CaptureScreenshot(GetRecoilCapturePath("hip-recoil.png"));
            yield return new WaitForEndOfFrame();

            inputDriver.SetAimHeld(true);
            for (int index = 0; index < 30; index++)
            {
                yield return null;
            }

            applyRecoil.Invoke(cameraManager, new[] { request });
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(GetRecoilCapturePath("ads-recoil.png"));
            yield return new WaitForEndOfFrame();

            clearRecoil.Invoke(cameraManager, null);
            yield return new WaitForEndOfFrame();
            object clearedSnapshot = debugSnapshot.GetValue(cameraManager);
            Assert.AreEqual(0f, FindContributionWeight(clearedSnapshot, "VisualRecoil"));
            Assert.Less(
                Quaternion.Angle(
                    startingAim,
                    (Quaternion)inputDriver.Controller.GetType().GetProperty("ControlRotation").GetValue(inputDriver.Controller)),
                0.01f,
                "Reload, switch and unbind paths can use the same explicit clear contract without residue.");
            inputDriver.ReleaseAll();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CameraImpulse_IsGeometryConstrainedAndReturnsToStableBasePose()
        {
            for (int index = 0; index < 4; index++)
            {
                yield return new WaitForEndOfFrame();
            }

            Type gameManagerType = FindRuntimeType("CGame.GameManager");
            Type cameraManagerType = FindRuntimeType("CGame.CameraManager");
            Type requestType = FindRuntimeType("CGame.CameraImpulseRequest");
            MethodInfo getManagerMethod = Array.Find(
                gameManagerType.GetMethods(BindingFlags.Static | BindingFlags.Public),
                method => method.Name == "GetManager" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            object cameraManager = getManagerMethod.MakeGenericMethod(cameraManagerType).Invoke(null, null);
            MethodInfo applyImpulse = cameraManagerType.GetMethod("ApplyingCameraImpulse");
            MethodInfo clearImpulse = cameraManagerType.GetMethod("ClearingCameraImpulse");
            PropertyInfo debugSnapshot = cameraManagerType.GetProperty("DebugSnapshot");
            Assert.NotNull(cameraManager);
            Assert.NotNull(applyImpulse);
            Assert.NotNull(clearImpulse);
            Assert.NotNull(inputDriver.Controller);

            object initialSnapshot = debugSnapshot.GetValue(cameraManager);
            object initialBasePose = initialSnapshot.GetType().GetProperty("BasePose").GetValue(initialSnapshot);
            Vector3 basePosition = (Vector3)initialBasePose.GetType().GetProperty("Position").GetValue(initialBasePose);
            Quaternion baseRotation = (Quaternion)initialBasePose.GetType().GetProperty("Rotation").GetValue(initialBasePose);
            Quaternion startingAim = (Quaternion)inputDriver.Controller.GetType()
                .GetProperty("ControlRotation").GetValue(inputDriver.Controller);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Impulse Validation Wall";
            wall.layer = 2;
            wall.transform.SetParent(GameObject.Find("[GameManager]").transform, true);
            wall.transform.SetPositionAndRotation(
                basePosition + baseRotation * new Vector3(0.032f, 0f, 0.35f),
                baseRotation);
            wall.transform.localScale = new Vector3(0.012f, 3f, 1.2f);
            Collider wallCollider = wall.GetComponent<Collider>();
            Component ownerMotor = GameObject.Find("RuntimeCharacter")
                .GetComponent(FindRuntimeType("CGame.CharacterPhysicsMotor"));
            Assert.NotNull(ownerMotor);
            FieldInfo collidableLayersField = ownerMotor.GetType().GetField("CollidableLayers");
            LayerMask collidableLayers = (LayerMask)collidableLayersField.GetValue(ownerMotor);
            collidableLayersField.SetValue(ownerMotor, (LayerMask)(collidableLayers.value & ~(1 << wall.layer)));

            Physics.SyncTransforms();
            RaycastHit[] validationHits = Physics.SphereCastAll(
                basePosition,
                0.012f,
                baseRotation * Vector3.right,
                0.05f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            Assert.IsTrue(
                Array.Exists(validationHits, hit => hit.collider == wallCollider),
                "Validation wall must be reachable by the same sphere-cast geometry used by the runtime constraint.");
            Type anchorType = FindRuntimeType("CGame.FirstPersonCameraAnchor");
            Component anchor = GameObject.Find("RuntimeCharacter").GetComponentInChildren(anchorType, true);
            Assert.AreNotSame(anchor.transform.root, wall.transform.root,
                "Validation geometry must not be filtered as part of the local owner root.");

            yield return new WaitForEndOfFrame();

            object preImpulseSnapshot = debugSnapshot.GetValue(cameraManager);
            object preImpulseBasePose = preImpulseSnapshot.GetType().GetProperty("BasePose").GetValue(preImpulseSnapshot);
            Vector3 preImpulseBasePosition = (Vector3)preImpulseBasePose.GetType().GetProperty("Position").GetValue(preImpulseBasePose);
            Quaternion preImpulseBaseRotation = (Quaternion)preImpulseBasePose.GetType().GetProperty("Rotation").GetValue(preImpulseBasePose);
            RaycastHit[] preImpulseHits = Physics.SphereCastAll(
                preImpulseBasePosition,
                0.012f,
                preImpulseBaseRotation * Vector3.right,
                0.05f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            Assert.IsTrue(
                Array.Exists(preImpulseHits, hit => hit.collider == wallCollider),
                $"Wall moved out of the current probe: baseDelta={preImpulseBasePosition - basePosition}, " +
                $"rotationDelta={Quaternion.Angle(preImpulseBaseRotation, baseRotation):F3}, wall={wall.transform.position}.");
            Assert.IsFalse(wall.transform.IsChildOf(ownerMotor.transform));
            ScreenCapture.CaptureScreenshot(GetImpulseCapturePath("wall-no-impulse.png"));
            yield return new WaitForEndOfFrame();

            object request = Activator.CreateInstance(
                requestType,
                new Vector3(0.05f, 0f, 0f),
                new Vector3(-2.4f, 0.5f, 0.4f),
                0.4f,
                20f);
            applyImpulse.Invoke(cameraManager, new[] { request });
            yield return new WaitForEndOfFrame();

            object impulseSnapshot = debugSnapshot.GetValue(cameraManager);
            object impulseDelta = FindContributionDelta(impulseSnapshot, "Impulse");
            Vector3 constrainedPosition = (Vector3)impulseDelta.GetType().GetProperty("LocalPosition").GetValue(impulseDelta);
            Assert.GreaterOrEqual(constrainedPosition.magnitude, 0f,
                "A fully blocked impulse may be constrained to zero translation.");
            Assert.Less(constrainedPosition.magnitude, 0.025f,
                "The nearby wall must compress only the requested Impulse translation.");
            Assert.Greater((float)impulseDelta.GetType().GetProperty("Weight").GetValue(impulseDelta), 0f);
            Assert.That(
                (Quaternion)inputDriver.Controller.GetType().GetProperty("ControlRotation").GetValue(inputDriver.Controller),
                Is.EqualTo(startingAim).Using(QuaternionEqualityComparer.Instance),
                "Environmental Camera Impulse must not change authoritative aim.");
            ScreenCapture.CaptureScreenshot(GetImpulseCapturePath("wall-constrained-impulse.png"));
            yield return new WaitForEndOfFrame();

            for (int index = 0; index < 90; index++)
            {
                yield return new WaitForEndOfFrame();
            }

            object settledSnapshot = debugSnapshot.GetValue(cameraManager);
            Assert.AreEqual(0f, FindContributionWeight(settledSnapshot, "Impulse"));
            object settledBasePose = settledSnapshot.GetType().GetProperty("BasePose").GetValue(settledSnapshot);
            Vector3 settledBasePosition = (Vector3)settledBasePose.GetType().GetProperty("Position").GetValue(settledBasePose);
            Vector3 settledFinalPosition = (Vector3)settledSnapshot.GetType().GetProperty("Position").GetValue(settledSnapshot);
            Assert.Less(Vector3.Distance(settledBasePosition, settledFinalPosition), 0.02f,
                "A nearby wall alone must not trigger third-person-style Camera pull-in.");

            applyImpulse.Invoke(cameraManager, new[] { request });
            applyImpulse.Invoke(cameraManager, new[] { request });
            clearImpulse.Invoke(cameraManager, null);
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(0f, FindContributionWeight(debugSnapshot.GetValue(cameraManager), "Impulse"));
            inputDriver.ReleaseAll();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CameraModes_UsePriorityAndCinemachineBlendWithoutChangingGameplayAuthority()
        {
            Type cameraManagerType = FindRuntimeType("CGame.CameraManager");
            Type gameManagerType = FindRuntimeType("CGame.GameManager");
            Type modeType = FindRuntimeType("CGame.CameraMode");
            Type transitionType = FindRuntimeType("CGame.CameraModeTransition");
            Type requestType = FindRuntimeType("CGame.CameraModeRequest");
            Type targetType = FindRuntimeType("CGame.CameraModeTargetState");
            Type poseType = FindRuntimeType("CGame.CameraPose");
            Assert.NotNull(cameraManagerType);
            Assert.NotNull(gameManagerType);
            Assert.NotNull(modeType);
            Assert.NotNull(transitionType);
            Assert.NotNull(requestType);
            Assert.NotNull(targetType);
            Assert.NotNull(poseType);

            MethodInfo getManager = gameManagerType.GetMethod("GetManager", BindingFlags.Public | BindingFlags.Static);
            object cameraManager = getManager.MakeGenericMethod(cameraManagerType).Invoke(null, null);
            MethodInfo requestMode = cameraManagerType.GetMethod("RequestingCameraMode");
            PropertyInfo activeMode = cameraManagerType.GetProperty("ActiveCameraMode");
            object output = cameraManagerType
                .GetField("output", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(cameraManager);
            object brain = output.GetType().GetProperty("Brain").GetValue(output);
            PropertyInfo isBlending = brain.GetType().GetProperty("IsBlending");
            Assert.NotNull(requestMode);
            Assert.NotNull(activeMode);
            Assert.NotNull(brain);
            Assert.NotNull(isBlending);
            Assert.IsNull(output.GetType().GetProperty("ViewModelCamera").GetValue(output));

            yield return null;
            yield return new WaitForEndOfFrame();
            GameObject character = GameObject.Find("RuntimeCharacter");
            object controller = inputDriver.Controller;
            int characterInstanceId = character.GetInstanceID();
            Camera worldCamera = Camera.main;
            Vector3 gameplayPosition = worldCamera.transform.position;
            Quaternion gameplayRotation = worldCamera.transform.rotation;
            float gameplayFieldOfView = worldCamera.fieldOfView;
            ScreenCapture.CaptureScreenshot(GetCameraModeCapturePath("gameplay-start.png"));
            yield return new WaitForEndOfFrame();

            object respawnTarget = CreateCameraModeTarget(
                targetType,
                poseType,
                gameplayPosition + worldCamera.transform.up * 0.2f,
                gameplayRotation,
                58f);
            IDisposable respawn = RequestCameraMode(
                requestMode, cameraManager, requestType, modeType, transitionType,
                "Respawn", respawnTarget, "Cut", 0f);
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Respawn", activeMode.GetValue(cameraManager).ToString());
            Assert.IsFalse((bool)isBlending.GetValue(brain));

            Vector3 deathPosition = gameplayPosition + worldCamera.transform.right * 0.8f + Vector3.up * 0.35f;
            object deathTarget = CreateCameraModeTarget(
                targetType,
                poseType,
                deathPosition,
                gameplayRotation,
                50f);
            IDisposable death = RequestCameraMode(
                requestMode, cameraManager, requestType, modeType, transitionType,
                "Death", deathTarget, "EaseInOut", 0.45f);
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Death", activeMode.GetValue(cameraManager).ToString());
            Assert.IsTrue((bool)isBlending.GetValue(brain), "Death must be selected through a Cinemachine Blend.");
            Assert.IsNull(GameObject.Find("ViewModel Overlay Camera"));
            ScreenCapture.CaptureScreenshot(GetCameraModeCapturePath("blend-start.png"));
            yield return new WaitForEndOfFrame();

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.That(worldCamera.fieldOfView, Is.InRange(50f, Mathf.Max(58f, gameplayFieldOfView) + 0.1f));
            Assert.Greater(Vector3.Dot(worldCamera.transform.up, gameplayRotation * Vector3.up), 0.995f,
                "Mode Blend must not introduce a sudden Roll.");
            ScreenCapture.CaptureScreenshot(GetCameraModeCapturePath("blend-mid.png"));
            yield return new WaitForEndOfFrame();

            yield return new WaitForSeconds(0.55f);
            yield return null;

            Assert.IsFalse((bool)isBlending.GetValue(brain));
            Assert.That(Vector3.Distance(worldCamera.transform.position, deathPosition), Is.LessThan(0.03f));
            Assert.AreEqual(50f, worldCamera.fieldOfView, 0.1f);
            ScreenCapture.CaptureScreenshot(GetCameraModeCapturePath("death-end.png"));
            yield return new WaitForEndOfFrame();

            IDisposable spectator = RequestCameraMode(
                requestMode, cameraManager, requestType, modeType, transitionType,
                "Spectator",
                CreateCameraModeTarget(
                    targetType, poseType,
                    gameplayPosition - worldCamera.transform.right * 0.65f + Vector3.up * 0.55f,
                    gameplayRotation, 62f),
                "EaseInOut", 0.25f);
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Spectator", activeMode.GetValue(cameraManager).ToString());

            IDisposable cinematic = RequestCameraMode(
                requestMode, cameraManager, requestType, modeType, transitionType,
                "Cinematic",
                CreateCameraModeTarget(
                    targetType, poseType,
                    gameplayPosition + Vector3.up * 0.75f,
                    gameplayRotation, 65f),
                "EaseInOut", 0.3f);
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Cinematic", activeMode.GetValue(cameraManager).ToString());

            cinematic.Dispose();
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Spectator", activeMode.GetValue(cameraManager).ToString());
            spectator.Dispose();
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Death", activeMode.GetValue(cameraManager).ToString());
            death.Dispose();
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("Respawn", activeMode.GetValue(cameraManager).ToString());
            respawn.Dispose();
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("GameplayFirstPerson", activeMode.GetValue(cameraManager).ToString());

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            for (int frame = 0; frame < 60 && (bool)isBlending.GetValue(brain); frame++)
            {
                yield return null;
            }

            Assert.IsFalse((bool)isBlending.GetValue(brain));
            Assert.IsNull(GameObject.Find("ViewModel Overlay Camera"));
            Assert.AreEqual(characterInstanceId, GameObject.Find("RuntimeCharacter").GetInstanceID());
            Assert.AreSame(controller, inputDriver.Controller);
            object currentGameplaySnapshot = cameraManagerType.GetProperty("DebugSnapshot").GetValue(cameraManager);
            Vector3 currentGameplayPosition = (Vector3)currentGameplaySnapshot.GetType()
                .GetProperty("Position").GetValue(currentGameplaySnapshot);
            float currentGameplayFieldOfView = (float)currentGameplaySnapshot.GetType()
                .GetProperty("FieldOfView").GetValue(currentGameplaySnapshot);
            Assert.That(Vector3.Distance(worldCamera.transform.position, currentGameplayPosition), Is.LessThan(0.15f));
            Assert.AreEqual(currentGameplayFieldOfView, worldCamera.fieldOfView, 0.2f);
            ScreenCapture.CaptureScreenshot(GetCameraModeCapturePath("gameplay-return.png"));
            yield return new WaitForEndOfFrame();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ObserverAimPresentation_DrivesRemoteWorldBodyWithoutOwnerCameraFacts()
        {
            Type aimFrameType = FindRuntimeType("CGame.Animation.ObserverAimFrame");
            Type weaponStateType = FindRuntimeType("CGame.Animation.ObserverWeaponState");
            Type presentationType = FindRuntimeType("CGame.ObserverCharacterPresentation");
            Assert.NotNull(aimFrameType, "ObserverAimFrame type missing.");
            Assert.NotNull(weaponStateType, "ObserverWeaponState type missing.");
            Assert.NotNull(presentationType, "ObserverCharacterPresentation type missing.");

            GameObject owner = GameObject.Find("RuntimeCharacter");
            Assert.NotNull(owner, "Runtime owner character missing.");
            Animator ownerAnimator = owner.GetComponentInChildren<Animator>();
            Assert.NotNull(ownerAnimator, "Owner Animator missing.");
            int ownerInstanceId = owner.GetInstanceID();
            int ownerWorldBodyLayer = LayerMask.NameToLayer("LocalOwnerWorldBody");
            const int observerEvidenceLayer = 31;
            Assert.GreaterOrEqual(ownerWorldBodyLayer, 0);
            Assert.IsTrue(owner.GetComponentsInChildren<Renderer>(true)
                .All(renderer => renderer.gameObject.layer != ownerWorldBodyLayer),
                "The accepted single-camera full-body path must keep the local complete body visible.");

            var observerRoot = new GameObject("[ObserverAimTestRoot]");
            observerRoot.transform.position = owner.transform.position + Vector3.right * 3f;
            GameObject observerVisual = UnityEngine.Object.Instantiate(
                ownerAnimator.transform.gameObject,
                observerRoot.transform);
            observerVisual.name = "ObserverCharacterVisual";
            observerVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Collider collider in observerVisual.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            foreach (Renderer renderer in observerVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = observerEvidenceLayer;
                renderer.enabled = true;
            }

            Animator observerAnimator = observerVisual.GetComponentInChildren<Animator>();
            Assert.NotNull(observerAnimator, "Cloned observer Animator missing.");
            observerAnimator.applyRootMotion = false;
            Transform rightHand = observerAnimator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "Right_Hand");
            Transform aimBone = observerAnimator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "UpperChest")
                ?? observerAnimator.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "Chest")
                ?? observerAnimator.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "Neck")
                ?? observerAnimator.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "Spine");
            Assert.NotNull(rightHand, "Observer right-hand bone missing.");
            Assert.NotNull(aimBone, "Observer upper-body bone missing.");

            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weapon.name = "ObserverWeaponPrototype";
            UnityEngine.Object.DestroyImmediate(weapon.GetComponent<Collider>());
            weapon.transform.SetParent(rightHand, false);
            weapon.transform.localPosition = new Vector3(0f, 0.08f, 0.28f);
            weapon.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            weapon.transform.localScale = new Vector3(0.08f, 0.08f, 0.55f);
            weapon.layer = observerEvidenceLayer;

            object presentation = Activator.CreateInstance(
                presentationType,
                observerRoot.transform,
                weapon);

            GameObject observerCameraObject = new GameObject("Observer Evidence Camera");
            Camera observerCamera = observerCameraObject.AddComponent<Camera>();
            Camera[] suppressedCameras = Camera.allCameras.Where(camera => camera != observerCamera).ToArray();
            bool[] suppressedCameraStates = suppressedCameras.Select(camera => camera.enabled).ToArray();
            foreach (Camera camera in suppressedCameras)
            {
                camera.enabled = false;
            }

            observerCamera.depth = 100f;
            observerCamera.clearFlags = CameraClearFlags.SolidColor;
            observerCamera.backgroundColor = new Color(0.36f, 0.52f, 0.72f, 1f);
            observerCamera.cullingMask = 1 << observerEvidenceLayer;
            observerCamera.rect = new Rect(0f, 0f, 1f, 1f);
            Camera.CameraCallback observerCameraViewportLock = camera =>
            {
                if (camera != observerCamera)
                {
                    return;
                }

                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
                camera.aspect = (float)Screen.width / Screen.height;
            };
            Camera.onPreCull += observerCameraViewportLock;
            observerCameraObject.transform.position = observerRoot.transform.position + new Vector3(2.4f, 1.45f, 3.4f);
            observerCameraObject.transform.rotation = Quaternion.LookRotation(
                observerRoot.transform.position + Vector3.up - observerCameraObject.transform.position,
                Vector3.up);

            MethodInfo applyFrame = presentationType.GetMethod("ApplyFrame");
            MethodInfo advance = presentationType.GetMethod("Advance");
            MethodInfo clear = presentationType.GetMethod("Clear");
            object hipFrame = Activator.CreateInstance(
                aimFrameType,
                180f,
                180f,
                0f,
                Enum.Parse(weaponStateType, "HipFire"));
            applyFrame.Invoke(presentation, new[] { hipFrame });
            for (int frame = 0; frame < 20; frame++)
            {
                advance.Invoke(presentation, new object[] { 0.016f });
                yield return null;
            }

            Quaternion neutralAimBoneRotation = aimBone.rotation;
            Assert.IsTrue(weapon.activeSelf);
            PrepareObserverEvidenceCamera(observerCamera);
            yield return new WaitForEndOfFrame();
            CaptureScreen(GetObserverAimCapturePath("observer-hipfire-neutral.png"));

            object adsFrame = Activator.CreateInstance(
                aimFrameType,
                180f,
                225f,
                35f,
                Enum.Parse(weaponStateType, "Ads"));
            applyFrame.Invoke(presentation, new[] { adsFrame });
            for (int frame = 0; frame < 24; frame++)
            {
                advance.Invoke(presentation, new object[] { 0.016f });
                yield return null;
            }

            object snapshot = presentationType.GetProperty("Snapshot").GetValue(presentation);
            Assert.AreEqual("Ads", snapshot.GetType().GetProperty("WeaponState").GetValue(snapshot).ToString());
            Assert.AreEqual(1f, (float)snapshot.GetType().GetProperty("AimWeight").GetValue(snapshot), 0.001f);
            Assert.AreEqual(1f, (float)snapshot.GetType().GetProperty("AdsWeight").GetValue(snapshot), 0.001f);
            Assert.AreEqual(1f, (float)snapshot.GetType().GetProperty("LeftHandIkWeight").GetValue(snapshot), 0.001f);
            Assert.AreEqual(180f, observerRoot.transform.eulerAngles.y, 0.1f);
            float observerAimBoneAngle = Quaternion.Angle(neutralAimBoneRotation, aimBone.rotation);
            Assert.Less(
                observerAimBoneAngle,
                15f,
                "V1 must not inject the requested 35-degree pitch/45-degree yaw into the animation runtime; small controller sampling drift is allowed.");
            Assert.IsTrue(observerAnimator.enabled);
            Assert.IsTrue(weapon.activeSelf);
            Assert.IsTrue(observerVisual.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.enabled));
            Assert.IsTrue(observerVisual.GetComponentsInChildren<Renderer>(true)
                .All(renderer => renderer.gameObject.layer != ownerWorldBodyLayer));
            Assert.AreEqual(ownerInstanceId, GameObject.Find("RuntimeCharacter").GetInstanceID());
            PrepareObserverEvidenceCamera(observerCamera);
            yield return new WaitForEndOfFrame();
            CaptureScreen(GetObserverAimCapturePath("observer-ads-aim-up-right.png"));

            clear.Invoke(presentation, null);
            for (int frame = 0; frame < 12; frame++)
            {
                advance.Invoke(presentation, new object[] { 0.016f });
                yield return null;
            }

            object cleared = presentationType.GetProperty("Snapshot").GetValue(presentation);
            Assert.IsFalse((bool)cleared.GetType().GetProperty("IsActive").GetValue(cleared));
            Assert.AreEqual(0f, (float)cleared.GetType().GetProperty("AimWeight").GetValue(cleared), 0.001f);
            Assert.AreEqual(0f, (float)cleared.GetType().GetProperty("LeftHandIkWeight").GetValue(cleared), 0.001f);
            Assert.IsFalse(weapon.activeSelf);
            PrepareObserverEvidenceCamera(observerCamera);
            yield return new WaitForEndOfFrame();
            CaptureScreen(GetObserverAimCapturePath("observer-cleared.png"));

            Camera.onPreCull -= observerCameraViewportLock;
            for (int cameraIndex = 0; cameraIndex < suppressedCameras.Length; cameraIndex++)
            {
                if (suppressedCameras[cameraIndex] != null)
                {
                    suppressedCameras[cameraIndex].enabled = suppressedCameraStates[cameraIndex];
                }
            }

            UnityEngine.Object.DestroyImmediate(observerCameraObject);
            UnityEngine.Object.DestroyImmediate(observerRoot);
            LogAssert.NoUnexpectedReceived();
        }

        private void SetAimRejectionOverride(string rejectionReason)
        {
            Type rejectionType = FindRuntimeType("CGame.AimRejectionReason");
            Assert.NotNull(rejectionType);
            object reason = Enum.Parse(rejectionType, rejectionReason);
            MethodInfo method = characterTestStep.GetType().GetMethod("SettingAimRejectionOverride");
            Assert.NotNull(method);
            method.Invoke(characterTestStep, new[] { reason });
        }

        private static float ReadFloat(object target, string propertyName)
        {
            return (float)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static Type FindRuntimeType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static CharacterAnimInstance FindCharacterAnimInstance(GameObject character)
        {
            Component pawnHost = character.GetComponent("PawnHost");
            Assert.NotNull(pawnHost);
            object pawn = pawnHost.GetType().GetProperty("Pawn").GetValue(pawnHost);
            Assert.NotNull(pawn);
            FieldInfo componentsField = pawn.GetType().BaseType.GetField(
                "components",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(componentsField);
            var components = (System.Collections.IEnumerable)componentsField.GetValue(pawn);
            foreach (object component in components)
            {
                if (component?.GetType().Name != "CharacterAnimationComponent")
                {
                    continue;
                }

                FieldInfo instanceField = component.GetType().GetField(
                    "animInstance",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                return (CharacterAnimInstance)instanceField?.GetValue(component);
            }

            return null;
        }

        private static string GetLocomotionCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures010"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string GetCharacterAnimationRuntimeCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "HarnessCaptures017"));
            Directory.CreateDirectory(directory);
            return string.IsNullOrEmpty(fileName) ? directory : Path.Combine(directory, fileName);
        }

        private static string GetRecoilCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures011"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string GetDirectionalLocomotionCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures015"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string GetImpulseCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures012"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string GetCameraModeCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures013"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string GetObserverAimCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures014"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string GetFullBodyAcceptanceCapturePath(string fileName)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "HarnessCaptures016"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static void DestroyImmediateIfPresent(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void CaptureScreen(string path)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void PrepareObserverEvidenceCamera(Camera camera)
        {
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
            camera.aspect = (float)Screen.width / Screen.height;
            camera.ResetProjectionMatrix();
            Assert.AreEqual(Screen.width, camera.pixelWidth);
            Assert.AreEqual(Screen.height, camera.pixelHeight);
        }

        private static object CreateCameraModeTarget(
            Type targetType,
            Type poseType,
            Vector3 position,
            Quaternion rotation,
            float fieldOfView)
        {
            object target = Activator.CreateInstance(targetType);
            object pose = Activator.CreateInstance(poseType, position, rotation);
            targetType.GetMethod("Updating").Invoke(target, new[] { pose, (object)fieldOfView });
            return target;
        }

        private static IDisposable RequestCameraMode(
            MethodInfo requestMode,
            object cameraManager,
            Type requestType,
            Type modeType,
            Type transitionType,
            string mode,
            object target,
            string transition,
            float duration)
        {
            object request = Activator.CreateInstance(
                requestType,
                Enum.Parse(modeType, mode),
                target,
                Enum.Parse(transitionType, transition),
                duration);
            return (IDisposable)requestMode.Invoke(cameraManager, new[] { request });
        }

        private static object FindContributionDelta(object snapshot, string layerName)
        {
            foreach (object contribution in (IEnumerable)snapshot.GetType().GetProperty("Contributions").GetValue(snapshot))
            {
                if (contribution.GetType().GetProperty("Layer").GetValue(contribution).ToString() == layerName)
                {
                    return contribution.GetType().GetProperty("PoseDelta").GetValue(contribution);
                }
            }

            Assert.Fail($"Camera contribution was not found: {layerName}");
            return null;
        }

        private static float FindContributionWeight(object snapshot, string layerName)
        {
            foreach (object contribution in (IEnumerable)snapshot.GetType().GetProperty("Contributions").GetValue(snapshot))
            {
                if (contribution.GetType().GetProperty("Layer").GetValue(contribution).ToString() != layerName)
                {
                    continue;
                }

                object poseDelta = contribution.GetType().GetProperty("PoseDelta").GetValue(contribution);
                return (float)poseDelta.GetType().GetProperty("Weight").GetValue(poseDelta);
            }

            Assert.Fail($"Camera contribution was not found: {layerName}");
            return 0f;
        }

        private static void AssertAdsConsumersMatch(
            float expectedProgress,
            Camera worldCamera,
            object controller,
            float hipWorldFov,
            float adsWorldFov,
            float adsLookMultiplier)
        {
            Assert.AreEqual(Mathf.Lerp(hipWorldFov, adsWorldFov, expectedProgress), worldCamera.fieldOfView, 0.5f);
            Assert.AreEqual(
                Mathf.Lerp(1f, adsLookMultiplier, expectedProgress),
                ReadFloat(controller, "LookSensitivityMultiplier"),
                0.001f);
        }

    }

    public sealed class CharacterTestStepLifecycleTests
    {
        private object characterTestStep;

        [TearDown]
        public void TearDown()
        {
            characterTestStep?.GetType().GetMethod("Exit").Invoke(characterTestStep, null);
            characterTestStep = null;
            DestroyIfPresent("[CharacterTestRuntime]");
            DestroyIfPresent("[GameManager]");
        }

        [UnityTest]
        public IEnumerator ExitBeforeObservingReady_ReleasesRuntimeCharacter()
        {
            yield return CharacterSpawnTestConfiguration
                .EnsureResourcesReady();
            Type stepType = Type.GetType("CGame.CharacterTestStep, Assembly-CSharp");
            Assert.NotNull(stepType);
            CharacterSpawnTestConfiguration.CreateManagerWithYooAssetDefinitions();
            characterTestStep = Activator.CreateInstance(stepType);
            stepType.GetMethod("Enter").Invoke(characterTestStep, null);
            for (int i = 0; i < 120 && GameObject.Find("RuntimeCharacter") == null; i++)
            {
                yield return null;
            }

            Assert.NotNull(GameObject.Find("RuntimeCharacter"));

            characterTestStep.GetType().GetMethod("Exit").Invoke(characterTestStep, null);
            characterTestStep = null;
            Assert.NotNull(GameObject.Find("RuntimeCharacter"));

            yield return null;

            Assert.IsNull(GameObject.Find("RuntimeCharacter"));
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject gameObject = GameObject.Find(objectName);
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }

    public class LaunchCharacterMovementRuntimeTests
    {
        private readonly PlayerInputTestDriver inputDriver = new PlayerInputTestDriver();
        private GameObject launcherObject;

        [SetUp]
        public void Setup()
        {
            FieldInfo settingField = typeof(YooAssetSettingsData).GetField("_setting", BindingFlags.Static | BindingFlags.NonPublic);
            if (settingField?.GetValue(null) == null)
            {
                LogAssert.Expect(LogType.Log, new Regex("YooAsset use (default|user) settings\\."));
                YooAssetSettingsData.GetDefaultYooFolderName();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (launcherObject != null)
            {
                UnityEngine.Object.DestroyImmediate(launcherObject);
                launcherObject = null;
            }

            ReturnLauncherToEmptyState();
            DestroyIfPresent("[CharacterTestRuntime]");
            DestroyIfPresent("[GameManager]");
            inputDriver.ReleaseAll();
        }

        [UnityTearDown]
        public IEnumerator TearDownResourcePackage()
        {
            ResourcePackage package = YooAssets.TryGetPackage("DefaultPackage");
            if (package == null)
            {
                yield break;
            }

            yield return package.DestroyAsync();
            YooAssets.RemovePackage("DefaultPackage");
            YooAssets.Destroy();
        }

        [UnityTest]
        public IEnumerator SampleSceneLaunch_HoldingForwardInput_MovesRuntimeCharacter()
        {
            ReturnLauncherToEmptyState();
            Type launcherMgrType = Type.GetType("CGame.GameLauncherMgr, Assembly-CSharp");
            Assert.IsNotNull(launcherMgrType);

            LogAssert.Expect(LogType.Log, new Regex("进入CGame\\.PreSourceStep时间: \\d+"));
            LogAssert.Expect(LogType.Log, "YooAssets initialize !");
            LogAssert.Expect(LogType.Log, "Create resource package : DefaultPackage");
            LogAssert.Expect(LogType.Log, "The package DefaultPackage create file system : YooAsset.DefaultEditorFileSystem");
            LogAssert.Expect(LogType.Log, "<color=green>ResourceManager: DefaultPackage initialized successfully!</color>");
            LogAssert.Expect(LogType.Log, new Regex("退出CGame\\.PreSourceStep时间: \\d+"));
            LogAssert.Expect(LogType.Log, new Regex("进入CGame\\.EnterStep时间: \\d+"));
            LogAssert.Expect(LogType.Log, new Regex("退出CGame\\.EnterStep时间: \\d+"));
            LogAssert.Expect(LogType.Log, new Regex("进入CGame\\.CharacterTestStep时间: \\d+"));
            LogAssert.Expect(LogType.Log, "[CharacterTest] Runtime ready. Use WASD to move and Space to jump.");

            launcherObject = new GameObject("LaunchRuntimeTest");
            launcherObject.AddComponent(launcherMgrType);

            GameObject character = null;
            for (int i = 0; i < 120; i++)
            {
                character = GameObject.Find("RuntimeCharacter");
                if (character != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(character);
            inputDriver.Bind(character);
            Vector3 startingPosition = character.transform.position;

            inputDriver.SetMove(Vector2.up);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            inputDriver.ReleaseAll();
            yield return null;

            Assert.Greater(character.transform.position.z, startingPosition.z + 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SampleSceneLaunch_ArtCharacterMovesJumpsAndLands()
        {
            ReturnLauncherToEmptyState();
            Type launcherMgrType = Type.GetType("CGame.GameLauncherMgr, Assembly-CSharp");
            Assert.IsNotNull(launcherMgrType);

            LogAssert.Expect(LogType.Log, new Regex(".*CGame\\.PreSourceStep.*\\d+"));
            LogAssert.Expect(LogType.Log, "YooAssets initialize !");
            LogAssert.Expect(LogType.Log, "Create resource package : DefaultPackage");
            LogAssert.Expect(LogType.Log, "The package DefaultPackage create file system : YooAsset.DefaultEditorFileSystem");
            LogAssert.Expect(LogType.Log, "<color=green>ResourceManager: DefaultPackage initialized successfully!</color>");
            LogAssert.Expect(LogType.Log, new Regex(".*CGame\\.PreSourceStep.*\\d+"));
            LogAssert.Expect(LogType.Log, new Regex(".*CGame\\.EnterStep.*\\d+"));
            LogAssert.Expect(LogType.Log, new Regex(".*CGame\\.EnterStep.*\\d+"));
            LogAssert.Expect(LogType.Log, new Regex(".*CGame\\.CharacterTestStep.*\\d+"));
            LogAssert.Expect(LogType.Log, "[CharacterTest] Runtime ready. Use WASD to move and Space to jump.");

            launcherObject = new GameObject("LaunchArtCharacterTest");
            launcherObject.AddComponent(launcherMgrType);

            GameObject character = null;
            for (int i = 0; i < 120 && character == null; i++)
            {
                character = GameObject.Find("RuntimeCharacter");
                yield return null;
            }

            Assert.IsNotNull(character);
            inputDriver.Bind(character);
            Transform visual = character.transform.Find("CharacterVisual");
            Assert.IsNotNull(visual);
            SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.Greater(renderers.Length, 0);
            Assert.IsTrue(System.Array.Exists(renderers, renderer => renderer.enabled && renderer.gameObject.activeInHierarchy));
            Assert.IsNull(character.transform.Find("Visual"));
            Animator animator = visual.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsTrue(animator.hasBoundPlayables);

            Vector3 startingPosition = character.transform.position;
            inputDriver.SetMove(Vector2.up);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            inputDriver.ReleaseAll();
            Assert.Greater(character.transform.position.z, startingPosition.z + 0.05f);

            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            float groundHeight = character.transform.position.y;
            float highestPoint = groundHeight;
            inputDriver.SetJumpPressed(true);
            yield return null;
            inputDriver.ReleaseAll();
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
                highestPoint = Mathf.Max(highestPoint, character.transform.position.y);
            }

            Assert.Greater(highestPoint, groundHeight + 0.5f);
            Assert.AreEqual(groundHeight, character.transform.position.y, 0.05f);
            Assert.IsTrue(visual.gameObject.activeInHierarchy);
            LogAssert.NoUnexpectedReceived();
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject gameObject = GameObject.Find(objectName);
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void ReturnLauncherToEmptyState()
        {
            Type launcherType = Type.GetType("CGame.GameLauncher, Assembly-CSharp");
            if (launcherType == null)
            {
                return;
            }

            object launcher = GetStaticInstance(launcherType);
            if (launcher == null)
            {
                return;
            }

            FieldInfo currentStepField = launcherType.GetField(
                "currentStep",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object currentStep = currentStepField?.GetValue(launcher);
            if (currentStep != null)
            {
                LogAssert.Expect(
                    LogType.Log,
                    new Regex(
                        $"退出{Regex.Escape(currentStep.GetType().FullName)}"
                        + "时间: \\d+"));
            }

            launcherType.GetMethod("ReturnLoginPanel")?.Invoke(launcher, null);
        }

        private static object GetStaticInstance(Type type)
        {
            while (type != null)
            {
                var property = type.GetProperty("Instance");
                if (property != null)
                {
                    return property.GetValue(null);
                }

                type = type.BaseType;
            }

            return null;
        }
    }

    internal sealed class PlayerInputTestDriver
    {
        private PlayerInputState state;

        public object Controller { get; private set; }

        public void Bind(GameObject character)
        {
            Type pawnHostType = Type.GetType("CGame.PawnHost, Assembly-CSharp");
            Assert.IsNotNull(pawnHostType);
            Component pawnHost = character.GetComponent(pawnHostType);
            Assert.IsNotNull(pawnHost);

            object pawn = pawnHostType.GetProperty("Pawn")?.GetValue(pawnHost);
            Assert.IsNotNull(pawn);
            object controller = pawn.GetType().GetProperty("Controller")?.GetValue(pawn);
            Assert.IsNotNull(controller);
            Controller = controller;

            MethodInfo setProviderMethod = controller.GetType().GetMethod("SettingInputStateProvider");
            Assert.IsNotNull(setProviderMethod);
            setProviderMethod.Invoke(controller, new object[] { new Func<PlayerInputState>(() => state) });
        }

        public void SetMove(Vector2 moveInput)
        {
            state.MoveInput = moveInput;
        }

        public void SetLookDelta(Vector2 lookDelta)
        {
            state.LookInput = new LookInputValue(lookDelta, LookInputTimeMode.Delta);
        }

        public void ClearLook()
        {
            state.LookInput = default;
        }

        public void SetJumpPressed(bool jumpPressed)
        {
            state.JumpPressed = jumpPressed;
        }

        public void SetAimHeld(bool aimHeld)
        {
            state.AimHeld = aimHeld;
        }

        public void SetSprintHeld(bool sprintHeld)
        {
            state.SprintHeld = sprintHeld;
        }

        public void SetFirePressed(bool firePressed)
        {
            state.FirePressed = firePressed;
        }

        public void SetReloadPressed(bool reloadPressed)
        {
            state.ReloadPressed = reloadPressed;
        }

        public void ReleaseAll()
        {
            state = default;
        }
    }
}
