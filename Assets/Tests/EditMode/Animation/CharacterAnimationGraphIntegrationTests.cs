using System;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class CharacterAnimationGraphIntegrationTests
    {
        [Test]
        public void Context_FirstFrameInitializesAllGroupsWithoutDerivativeSpikes()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Source.Root.transform.SetPositionAndRotation(
                    new Vector3(10f, 2f, -3f),
                    Quaternion.Euler(0f, 45f, 0f));
                fixture.Source.Velocity = new Vector3(2f, 3f, 4f);
                fixture.Source.IsGrounded = false;

                fixture.Instance.UpdateAnimation(0.016f);

                AnimationUpdateContext context = fixture.Instance.UpdateContext;
                Assert.AreEqual(context.Location.WorldLocation, context.Location.PreviousWorldLocation);
                Assert.AreEqual(Vector3.zero, context.Location.DisplacementDelta);
                Assert.AreEqual(0f, context.Location.DisplacementSpeed);
                Assert.AreEqual(0f, context.Rotation.YawDeltaSpeed);
                Assert.AreEqual(Vector3.zero, context.Acceleration.WorldAcceleration);
                Assert.AreEqual(fixture.Source.Velocity, context.Velocity.WorldVelocity);
                Assert.IsTrue(context.CharacterState.IsJumping);
                Assert.IsFalse(context.CharacterState.IsFalling);
            }
        }

        [Test]
        public void Context_UpdatesLocationRotationVelocityAccelerationAndStateInDependencyOrder()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Instance.UpdateAnimation(0.1f);
                fixture.Source.Root.transform.SetPositionAndRotation(
                    new Vector3(0f, 0f, 1f),
                    Quaternion.Euler(0f, 90f, 0f));
                fixture.Source.Velocity = new Vector3(0f, 0f, 5f);
                fixture.Source.IsGrounded = true;

                fixture.Instance.UpdateAnimation(0.1f);

                AnimationUpdateContext context = fixture.Instance.UpdateContext;
                Assert.AreEqual(new Vector3(0f, 0f, 1f), context.Location.DisplacementDelta);
                Assert.AreEqual(10f, context.Location.DisplacementSpeed, 0.001f);
                Assert.AreEqual(90f, context.Rotation.YawDelta, 0.001f);
                Assert.AreEqual(900f, context.Rotation.YawDeltaSpeed, 0.001f);
                Assert.AreEqual(fixture.Source.Velocity, context.Velocity.WorldVelocity);
                Assert.AreEqual(5f, context.Velocity.HorizontalSpeed, 0.001f);
                Assert.AreEqual(50f, context.Acceleration.WorldAcceleration.z, 0.001f);
                Assert.IsTrue(context.CharacterState.IsMoving);
                Assert.IsTrue(context.CharacterState.IsSprinting);
            }
        }

        [Test]
        public void Context_MarkDiscontinuityResetsHistoryOnNextFrame()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Instance.UpdateAnimation(0.1f);
                fixture.Source.Root.transform.position = Vector3.forward;
                fixture.Source.Velocity = Vector3.forward;
                fixture.Instance.UpdateAnimation(0.1f);

                fixture.Source.Root.transform.SetPositionAndRotation(
                    new Vector3(100f, 20f, -50f),
                    Quaternion.Euler(0f, 170f, 0f));
                fixture.Source.Velocity = new Vector3(10f, 0f, 0f);
                fixture.Instance.MarkDiscontinuity();
                fixture.Instance.UpdateAnimation(0.1f);

                AnimationUpdateContext context = fixture.Instance.UpdateContext;
                Assert.AreEqual(Vector3.zero, context.Location.DisplacementDelta);
                Assert.AreEqual(0f, context.Location.DisplacementSpeed);
                Assert.AreEqual(0f, context.Rotation.YawDeltaSpeed);
                Assert.AreEqual(Vector3.zero, context.Acceleration.WorldAcceleration);
            }
        }

        [Test]
        public void DisabledAnimator_DoesNotFreezeContextHistoryAndRecoversOutput()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Instance.UpdateAnimation(0.1f);
                Assert.AreEqual(2, fixture.Animator.playableGraph.GetOutputCount());

                fixture.Animator.enabled = false;
                fixture.Source.Root.transform.position = Vector3.forward;
                fixture.Source.Velocity = Vector3.forward * 2f;
                fixture.Instance.UpdateAnimation(0.1f);
                Assert.AreEqual(Vector3.forward, fixture.Instance.UpdateContext.Location.WorldLocation);
                Assert.Greater(fixture.Animator.GetFloat("MoveY"), 0.5f);
                Assert.IsTrue(fixture.Animator.GetBool("Moving"));

                fixture.Source.Root.transform.position = Vector3.forward * 2f;
                fixture.Source.Velocity = Vector3.forward * 3f;
                fixture.Instance.UpdateAnimation(0.1f);
                Assert.AreEqual(Vector3.forward, fixture.Instance.UpdateContext.Location.DisplacementDelta);

                fixture.Animator.enabled = true;
                fixture.Instance.UpdateAnimation(0.1f);
                Assert.AreEqual(2, fixture.Animator.playableGraph.GetOutputCount());
                Assert.IsTrue(fixture.Instance.PlayablesController.IsValid());
            }
        }

        [Test]
        public void ContextData_PublicPropertiesAreReadOnly()
        {
            Type[] dataTypes =
            {
                typeof(AnimationLocationData),
                typeof(AnimationRotationData),
                typeof(AnimationVelocityData),
                typeof(AnimationAccelerationData),
                typeof(AnimationCharacterStateData),
            };

            foreach (Type dataType in dataTypes)
            {
                Assert.IsTrue(dataType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any());
                Assert.IsTrue(dataType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .All(property => property.SetMethod == null || !property.SetMethod.IsPublic));
                Assert.IsFalse(dataType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Any(method => method.Name == "Update" || method.Name == "Reset"));
            }
        }

        [Test]
        public void VisualPrefab_UsesRequestedGenericAnimatorAtTheSkeletonBindingBoundary()
        {
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab");
            Assert.NotNull(visualPrefab);

            Animator[] animators = visualPrefab.GetComponentsInChildren<Animator>(true);
            Assert.AreEqual(1, animators.Length);
            Animator animator = animators[0];
            SkinnedMeshRenderer renderer = visualPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.NotNull(renderer);
            Assert.NotNull(renderer.rootBone);
            Assert.IsTrue(renderer.rootBone.IsChildOf(animator.transform));
            Assert.IsTrue(animator.avatar.isValid);
            Assert.IsFalse(animator.avatar.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode);
            Assert.AreEqual(
                "Assets/KINEMATION/ScriptableAnimationSystemDemo/Animations/Locomotion/FPSAnimator_Generic.controller",
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            Assert.AreEqual(
                "Assets/KINEMATION/ScriptableAnimationSystemDemo/Meshes/Character/UnityRobot/Skeleton/IKRig.fbx",
                AssetDatabase.GetAssetPath(animator.avatar));
        }

        [Test]
        public void Runtime_PreservesNativeOutputZeroAndOwnsOnlyOutputOne()
        {
            using (var fixture = new RuntimeFixture())
            {
                PlayableGraph graph = fixture.Animator.playableGraph;
                PlayableOutput nativeOutput = graph.GetOutput(0);
                Playable nativeSource = nativeOutput.GetSourcePlayable();

                fixture.Instance.UpdateAnimation(0.016f);

                Assert.AreEqual(2, graph.GetOutputCount());
                Assert.AreEqual(nativeOutput, graph.GetOutput(0));
                Assert.AreEqual(nativeSource, graph.GetOutput(0).GetSourcePlayable());
                Assert.AreEqual(0f, graph.GetOutput(0).GetWeight());
                Assert.IsTrue(graph.GetOutput(1).IsOutputValid());
                Assert.AreEqual(1f, graph.GetOutput(1).GetWeight());

                fixture.Instance.Dispose();
                fixture.Instance = null;
                Assert.IsTrue(graph.IsValid());
                Assert.AreEqual(1, graph.GetOutputCount());
                Assert.AreEqual(nativeOutput, graph.GetOutput(0));
                Assert.AreEqual(nativeSource, graph.GetOutput(0).GetSourcePlayable());
                Assert.AreEqual(1f, graph.GetOutput(0).GetWeight());
            }
        }

        [Test]
        public void Runtime_MapsAllSixAnimatorParametersFromPhysicalFacts()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Instance.UpdateAnimation(0.016f);
                fixture.Source.Velocity = new Vector3(3f, 0f, 4f);
                fixture.Source.IsGrounded = true;
                fixture.Instance.UpdateAnimation(0.016f);
                Assert.That(fixture.Animator.GetFloat("MoveX"), Is.InRange(0.01f, 0.59f));
                Assert.That(fixture.Animator.GetFloat("MoveY"), Is.InRange(0.01f, 0.79f));
                Assert.That(fixture.Animator.GetFloat("Sprinting"), Is.InRange(0.01f, 0.99f));
                for (int frame = 1; frame < 60; frame++)
                {
                    fixture.Instance.UpdateAnimation(0.016f);
                }

                Assert.AreEqual(0.6f, fixture.Animator.GetFloat("MoveX"), 0.001f);
                Assert.AreEqual(0.8f, fixture.Animator.GetFloat("MoveY"), 0.001f);
                Assert.AreEqual(1f, fixture.Animator.GetFloat("Velocity"), 0.001f);
                Assert.IsTrue(fixture.Animator.GetBool("Moving"));
                Assert.IsFalse(fixture.Animator.GetBool("InAir"));
                Assert.AreEqual(1f, fixture.Animator.GetFloat("Sprinting"), 0.001f);

                fixture.Source.IsGrounded = false;
                fixture.Source.Velocity = Vector3.down;
                for (int frame = 0; frame < 60; frame++)
                {
                    fixture.Instance.UpdateAnimation(0.016f);
                }
                Assert.IsFalse(fixture.Animator.GetBool("Moving"));
                Assert.IsTrue(fixture.Animator.GetBool("InAir"));
                Assert.AreEqual(0f, fixture.Animator.GetFloat("Sprinting"), 0.001f);
            }
        }

        [Test]
        public void Runtime_ControllerSourceReplacementRebuildsOwnedOutputInSameUpdate()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Instance.UpdateAnimation(0.016f);
                RuntimeAnimatorController controller = fixture.Animator.runtimeAnimatorController;
                var replacement = new AnimatorOverrideController(controller);
                try
                {
                    fixture.Animator.runtimeAnimatorController = replacement;
                    fixture.Animator.Rebind();
                    fixture.Instance.UpdateAnimation(0.016f);

                    Assert.AreEqual(2, fixture.Animator.playableGraph.GetOutputCount());
                    Assert.IsTrue(fixture.Instance.PlayablesController.IsValid());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(replacement);
                }
            }
        }

        [Test]
        public void Runtime_MissingRequiredParameterKeepsNativeFallbackWithoutPartialOutput()
        {
            using (var fixture = new RuntimeFixture())
            {
                RuntimeAnimatorController incompatibleSource = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Art/Animation/KINEMATION/FPSAnimationPack/AnimatorControllers/AC_FPS_Character.controller");
                Assert.NotNull(incompatibleSource);
                var incompatible = new AnimatorOverrideController(incompatibleSource);
                try
                {
                    fixture.Animator.runtimeAnimatorController = incompatible;
                    fixture.Animator.Rebind();

                    LogAssert.Expect(
                        LogType.Error,
                        $"Animator Controller '{incompatible.name}' requires MoveX (Float) for character locomotion.");
                    fixture.Instance.UpdateAnimation(0.016f);

                    Assert.AreEqual(1, fixture.Animator.playableGraph.GetOutputCount());
                    Assert.IsFalse(fixture.Instance.PlayablesController.IsValid());
                    Assert.AreEqual(1f, fixture.Animator.playableGraph.GetOutput(0).GetWeight());
                    Assert.AreEqual(fixture.Source.Velocity, fixture.Instance.UpdateContext.Velocity.WorldVelocity);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(incompatible);
                }
            }
        }

        [Test]
        public void Runtime_DisposeIsIdempotentAndDoesNotMutateSourceState()
        {
            using (var fixture = new RuntimeFixture())
            {
                fixture.Source.Velocity = new Vector3(1f, 2f, 3f);
                fixture.Source.IsGrounded = false;
                fixture.Instance.UpdateAnimation(0.016f);

                fixture.Instance.Dispose();
                fixture.Instance.Dispose();

                Assert.AreEqual(new Vector3(1f, 2f, 3f), fixture.Source.Velocity);
                Assert.IsFalse(fixture.Source.IsGrounded);
                Assert.AreEqual(1, fixture.Animator.playableGraph.GetOutputCount());
            }
        }

        private sealed class RuntimeFixture : IDisposable
        {
            private readonly GameObject visual;

            public RuntimeFixture()
            {
                Source = new FakeSource();
                GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab");
                Assert.NotNull(visualPrefab);
                visual = UnityEngine.Object.Instantiate(visualPrefab);
                Animator = visual.GetComponentInChildren<Animator>();
                Assert.NotNull(Animator);
                Instance = new CharacterAnimInstance(Source, Animator);
            }

            public FakeSource Source { get; }
            public Animator Animator { get; }
            public CharacterAnimInstance Instance { get; set; }

            public void Dispose()
            {
                Instance?.Dispose();
                UnityEngine.Object.DestroyImmediate(visual);
                Source.Dispose();
            }
        }

        private sealed class FakeSource : IAnimationCharacterSource, IDisposable
        {
            public FakeSource()
            {
                Root = new GameObject("[AnimationSource]");
            }

            public GameObject Root { get; }
            public Transform Transform => Root.transform;
            public Vector3 Velocity { get; set; }
            public bool IsGrounded { get; set; } = true;

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
