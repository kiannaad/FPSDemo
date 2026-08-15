using System;
using System.Collections;
using System.IO;
using System.Reflection;
using CGame;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CGame.WeaponGrip.Tests
{
    public sealed class LocomotionRecoveryProductionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_FormalInputDrivesLocomotionAndIntegratedBonePipeline()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return WaitForWorldPlaying();

            GameInstance gameInstance = UnityEngine.Object.FindObjectOfType<GameInstance>();
            PlayerController controller = gameInstance.RuntimeWorld.GameMode.PlayerController as PlayerController;
            Assert.That(controller, Is.Not.Null);

            Pawn pawn = controller.ControlledPawn;
            Assert.That(pawn, Is.Not.Null);
            CharacterPhysicsMotor motor = pawn.Root.GetComponent<CharacterPhysicsMotor>();
            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            Animator animator = pawn.Transform.GetComponentInChildren<Animator>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(animation?.AnimInstance, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);

            for (int frame = 0; frame < 120 && !motor.GroundingStatus.IsStableOnGround; frame++) yield return null;
            Assert.That(
                motor.GroundingStatus.IsStableOnGround,
                Is.True,
                "The production motor must settle on the SampleScene ground before input. "
                + "Position=" + pawn.Transform.position + ", Velocity=" + motor.Velocity);
            CharacterBoneController boneController = animation.AnimInstance.BoneController;
            Assert.That(boneController, Is.Not.Null);
            Assert.That(boneController.IsValid(), Is.True);
            AssertIntegratedOutputs(animator.playableGraph, 0);
            for (int frame = 0; frame < 60; frame++) yield return null;
            Assert.That(
                animator.GetBool("InAir"),
                Is.False,
                "The settled locomotion state must be grounded. MotorGrounded="
                + motor.GroundingStatus.IsStableOnGround
                + ", Position=" + pawn.Transform.position);
            CaptureExternal(pawn, "bone-controller-004-before.png");
            float lowestRenderedPoint = GetLowestRenderedPoint(animator);
            Assert.That(
                lowestRenderedPoint - pawn.Transform.position.y,
                Is.InRange(-0.12f, 0.12f),
                "The animated body must visually meet the grounded physics root.");
            Vector3 startPosition = pawn.Transform.position;
            float startRootHeight = startPosition.y;
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(
                "Assets/Settings/Animation/Weapons/AK12/AK12BoneProfile.asset");
            Assert.That(profile, Is.Not.Null);
            WeaponBoneProfileValidator.ValidateAk12(profile);
            boneController.LinkProfile(profile);
            yield return WaitForActiveProfile(boneController, profile, 120);
            AnimationBlendingJob blendIn = GetBlendingJob(boneController);
            Assert.That(blendIn.BlendDuration, Is.EqualTo(profile.BlendIn));
            Assert.That(blendIn.IsBlending, Is.EqualTo(profile.BlendIn > 0f));
            for (int frame = 0; frame < 30; frame++) yield return null;
            Assert.That(GetBlendingJob(boneController).IsBlending, Is.False);

            PlayableOutput destroyedBoneOutput = GetBoneOutput(animator.playableGraph);
            animator.playableGraph.DestroyOutput(destroyedBoneOutput);
            for (int frame = 0; frame < 30 && !boneController.IsValid(); frame++) yield return null;
            Assert.That(boneController.IsValid(), Is.True, "The production update must recover a destroyed bone output.");
            Assert.That(boneController.ActiveProfile, Is.SameAs(profile));
            Assert.That(GetBoneOutput(animator.playableGraph).GetHandle(), Is.Not.EqualTo(destroyedBoneOutput.GetHandle()));
            Assert.That(animation.AnimInstance.TryRebuildGraph(), Is.True);
            Assert.That(boneController.ActiveProfile, Is.SameAs(profile));
            Assert.That(GetBlendingJob(boneController).IsBlending, Is.False);
            AssertIntegratedOutputs(animator.playableGraph, profile.Layers.Count);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("LocomotionRecoveryKeyboard");
            keyboard.MakeCurrent();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                yield return WaitForAnimatorBool(animator, "Moving", true, 60);
                AnimatorControllerPlayable controllerPlayable = GetControllerPlayable(animator.playableGraph);
                Assert.That(controllerPlayable.GetBool("Moving"), Is.True);
                AnimatorStateInfo initialState = controllerPlayable.GetCurrentAnimatorStateInfo(0);
                Transform leftUpperLeg = FindExactTransform(pawn.Transform, "Left_UpperLeg");
                Quaternion initialLegRotation = leftUpperLeg.localRotation;
                float maximumLegRotationDelta = 0f;
                float maximumStateTimeDelta = 0f;
                bool changedState = false;
                for (int frame = 0; frame < 30; frame++)
                {
                    yield return null;
                    maximumLegRotationDelta = Mathf.Max(
                        maximumLegRotationDelta,
                        Quaternion.Angle(initialLegRotation, leftUpperLeg.localRotation));
                    AnimatorStateInfo observedState = controllerPlayable.GetCurrentAnimatorStateInfo(0);
                    changedState |= observedState.fullPathHash != initialState.fullPathHash;
                    maximumStateTimeDelta = Mathf.Max(
                        maximumStateTimeDelta,
                        Mathf.Abs(observedState.normalizedTime - initialState.normalizedTime));
                }
                Assert.That(animator.GetFloat("MoveY"), Is.GreaterThan(0.1f));
                Assert.That(motor.Velocity.magnitude, Is.GreaterThan(0.1f));
                Assert.That(
                    maximumLegRotationDelta,
                    Is.GreaterThan(3f),
                    "The PlayablesController output must evaluate changing locomotion bone poses.");
                Assert.That(
                    changedState || maximumStateTimeDelta > 0.1f,
                    Is.True,
                    "The owned AnimatorControllerPlayable state must advance while locomotion is active.");
                CaptureExternal(pawn, "bone-controller-004-after.png");
                Vector3 afterForward = pawn.Transform.position;
                Assert.That(HorizontalDistance(startPosition, afterForward), Is.GreaterThan(0.05f));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                InputSystem.Update();
                for (int frame = 0; frame < 20; frame++) yield return null;
                Vector3 afterStrafe = pawn.Transform.position;
                Assert.That(HorizontalDistance(afterForward, afterStrafe), Is.GreaterThan(0.05f));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return WaitForAnimatorBool(animator, "Moving", false, 120);
                Assert.That(motor.GroundingStatus.IsStableOnGround, Is.True, "The motor must remain grounded after horizontal input stops.");
                Assert.That(Mathf.Abs(pawn.Transform.position.y - startRootHeight), Is.LessThan(0.05f));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                yield return WaitForAirborne(motor, animator, 60);
                Assert.That(motor.Velocity.y, Is.GreaterThan(0f));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return WaitForLanding(motor, animator, 300);
                Assert.That(Mathf.Abs(pawn.Transform.position.y - startRootHeight), Is.LessThan(0.05f));
                boneController.UnlinkProfile();
                yield return WaitForActiveProfile(boneController, null, 120);
                AnimationBlendingJob blendOut = GetBlendingJob(boneController);
                Assert.That(blendOut.BlendDuration, Is.EqualTo(profile.BlendOut));
                Assert.That(blendOut.IsBlending, Is.EqualTo(profile.BlendOut > 0f));
                for (int frame = 0; frame < 30; frame++) yield return null;
                Assert.That(GetBlendingJob(boneController).IsBlending, Is.False);
                AssertIntegratedOutputs(animator.playableGraph, 0);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static IEnumerator WaitForWorldPlaying()
        {
            for (int frame = 0; frame < 300; frame++)
            {
                GameInstance gameInstance = UnityEngine.Object.FindObjectOfType<GameInstance>();
                if (gameInstance != null && gameInstance.RuntimeWorld?.State == WorldState.Playing) yield break;
                yield return null;
            }

            throw new TimeoutException("SampleScene did not create a playing World.");
        }

        private static IEnumerator WaitForAnimatorBool(Animator animator, string parameter, bool expected, int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (animator.GetBool(parameter) == expected) yield break;
                yield return null;
            }

            Assert.Fail(parameter + " did not become " + expected + ".");
        }

        private static IEnumerator WaitForAirborne(CharacterPhysicsMotor motor, Animator animator, int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (!motor.GroundingStatus.IsStableOnGround && animator.GetBool("InAir")) yield break;
                yield return null;
            }

            Assert.Fail("The production pawn did not enter the airborne state.");
        }

        private static IEnumerator WaitForLanding(CharacterPhysicsMotor motor, Animator animator, int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (motor.GroundingStatus.IsStableOnGround && !animator.GetBool("InAir")) yield break;
                yield return null;
            }

            Assert.Fail("The production pawn did not return to the grounded state.");
        }

        private static IEnumerator WaitForActiveProfile(
            CharacterBoneController controller,
            BoneProfile expected,
            int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (controller.ActiveProfile == expected) yield break;
                yield return null;
            }

            Assert.Fail("Bone profile did not become " + (expected != null ? expected.name : "null") + ".");
        }

        private static void AssertIntegratedOutputs(PlayableGraph graph, int expectedProfileLayerCount)
        {
            Assert.That(graph.IsValid(), Is.True, "The production Animator playable graph must remain valid.");
            Assert.That(graph.GetOutputCount(), Is.EqualTo(3));
            Assert.That(graph.GetOutput(0).GetWeight(), Is.EqualTo(0f));
            Assert.That(graph.GetOutput(0).GetSourcePlayable().GetPlayableType(), Is.EqualTo(typeof(AnimatorControllerPlayable)));
            Assert.That(graph.GetOutput(1).GetWeight(), Is.EqualTo(1f));
            Assert.That(graph.GetOutput(1).GetSourcePlayable().GetPlayableType(), Is.EqualTo(typeof(AnimationLayerMixerPlayable)));
            AnimationPlayableOutput boneOutput = GetBoneOutput(graph);
            Assert.That(
                boneOutput.GetAnimationStreamSource(),
                Is.EqualTo(UnityEngine.Experimental.Animations.AnimationStreamSource.PreviousInputs));
            AnimationScriptPlayable blending = (AnimationScriptPlayable)boneOutput.GetSourcePlayable();
            Assert.That(blending.GetJobData<AnimationBlendingJob>().Poses.IsCreated, Is.True);
            Playable profileOrVirtualElement = blending.GetInput(0);
            int profileLayerCount = 0;
            while (profileOrVirtualElement.GetInputCount() > 0)
            {
                profileLayerCount++;
                profileOrVirtualElement = profileOrVirtualElement.GetInput(0);
            }

            Assert.That(profileLayerCount, Is.EqualTo(expectedProfileLayerCount));
            AnimationScriptPlayable virtualElements = (AnimationScriptPlayable)profileOrVirtualElement;
            Assert.That(virtualElements.GetJobData<VirtualElementJob>().Handles.IsCreated, Is.True);
        }

        private static AnimationPlayableOutput GetBoneOutput(PlayableGraph graph)
        {
            for (int index = 1; index < graph.GetOutputCount(); index++)
            {
                AnimationPlayableOutput output = (AnimationPlayableOutput)graph.GetOutput(index);
                if (output.GetAnimationStreamSource()
                    == UnityEngine.Experimental.Animations.AnimationStreamSource.PreviousInputs)
                {
                    return output;
                }
            }

            Assert.Fail("Missing CharacterBoneController PreviousInputs output.");
            return AnimationPlayableOutput.Null;
        }

        private static AnimationBlendingJob GetBlendingJob(CharacterBoneController controller)
        {
            FieldInfo field = typeof(CharacterBoneController).GetField(
                "blendingPlayable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return ((AnimationScriptPlayable)field.GetValue(controller)).GetJobData<AnimationBlendingJob>();
        }

        private static AnimatorControllerPlayable GetControllerPlayable(PlayableGraph graph)
        {
            AnimationLayerMixerPlayable masterMixer =
                (AnimationLayerMixerPlayable)graph.GetOutput(1).GetSourcePlayable();
            return (AnimatorControllerPlayable)masterMixer.GetInput(0);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private static Transform FindExactTransform(Transform root, string name)
        {
            Transform result = null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != name) continue;
                Assert.That(result, Is.Null, "Expected exactly one transform named " + name);
                result = candidate;
            }

            Assert.That(result, Is.Not.Null, "Missing transform " + name);
            return result;
        }

        private static float GetLowestRenderedPoint(Animator animator)
        {
            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            float minimum = float.PositiveInfinity;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer.enabled)
                {
                    minimum = Mathf.Min(minimum, renderer.bounds.min.y);
                }
            }

            return minimum;
        }

        private static void CaptureExternal(Pawn pawn, string fileName)
        {
            GameObject cameraObject = new GameObject("LocomotionRecoveryEvidenceCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 45f;
            camera.cullingMask = ~0;
            camera.transform.position = pawn.Transform.position + new Vector3(-3.2f, 1.45f, -3.2f);
            camera.transform.LookAt(pawn.Transform.position + new Vector3(0f, 1f, 0f));

            RenderTexture renderTexture = new RenderTexture(1280, 720, 24);
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(Application.temporaryCachePath, fileName), image.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(renderTexture);
            UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(cameraObject);
        }
    }
}
