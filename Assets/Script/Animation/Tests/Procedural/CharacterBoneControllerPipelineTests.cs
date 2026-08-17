using System;
using System.Collections.Generic;
using System.Reflection;
using CGame.Animation.Rig;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    public sealed class CharacterBoneControllerPipelineTests
    {
        [Test]
        public void TryRebuild_CreatesAnIndependentPreviousInputsOutput()
        {
            using (TestRig testRig = TestRig.Create(false))
            {
                int outputCount = testRig.Graph.GetOutputCount();

                Assert.That(testRig.Controller.TryRebuild(), Is.True);

                Assert.That(testRig.Graph.GetOutputCount(), Is.EqualTo(outputCount + 1));
                AnimationPlayableOutput output = GetOutput(testRig.Controller);
                AnimationScriptPlayable blending = GetPlayable(testRig.Controller, "blendingPlayable");
                AnimationScriptPlayable virtualElements = GetPlayable(testRig.Controller, "virtualElementPlayable");
                Assert.That(output.GetAnimationStreamSource(), Is.EqualTo(AnimationStreamSource.PreviousInputs));
                Assert.That(output.GetSourcePlayable().GetHandle(), Is.EqualTo(blending.GetHandle()));
                Assert.That(blending.GetInput(0).GetHandle(), Is.EqualTo(virtualElements.GetHandle()));
                Assert.That(virtualElements.GetInputCount(), Is.Zero);
                Assert.That(testRig.Output.GetSourcePlayable().GetHandle(), Is.EqualTo(testRig.Source.GetHandle()));
                Assert.That(blending.GetJobData<AnimationBlendingJob>().Poses.IsCreated, Is.True);
                Assert.That(blending.GetJobData<AnimationBlendingJob>().Poses.Length, Is.EqualTo(testRig.RigCount));
                Assert.That(virtualElements.GetJobData<VirtualElementJob>().Handles.IsCreated, Is.True);
            }
        }

        [Test]
        public void VirtualElementJob_CopiesTheTargetInsideTheSameEvaluation()
        {
            using (TestRig testRig = TestRig.Create(true))
            {
                Assert.That(testRig.Controller.TryRebuild(), Is.True);
                testRig.Target.localPosition = new Vector3(0.35f, -0.2f, 0.75f);
                testRig.Target.localRotation = Quaternion.Euler(17f, 29f, -11f);
                testRig.Virtual.localPosition = Vector3.zero;
                testRig.Virtual.localRotation = Quaternion.identity;

                testRig.Graph.Evaluate();

                Assert.That(Vector3.Distance(testRig.Virtual.position, testRig.Target.position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(testRig.Virtual.rotation, testRig.Target.rotation), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void TryRebuild_RejectsDuplicateVirtualElementObjects()
        {
            using (TestRig testRig = TestRig.Create(true, true))
            {
                Assert.That(
                    () => testRig.Controller.TryRebuild(),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(testRig.Output.GetSourcePlayable().GetHandle(), Is.EqualTo(testRig.Source.GetHandle()));
                Assert.That(testRig.Graph.GetOutputCount(), Is.EqualTo(1));
            }
        }

        [Test]
        public void Dispose_RepeatedlyRestoresTheOriginalOutputWithoutLeakingPlayables()
        {
            using (TestRig testRig = TestRig.Create(false, false, false))
            {
                int stablePlayableCount = -1;
                for (int iteration = 0; iteration < 10; iteration++)
                {
                    CharacterBoneController controller = testRig.CreateController();
                    Assert.That(controller.TryRebuild(), Is.True);
                    controller.Dispose();
                    Assert.That(testRig.Output.GetSourcePlayable().GetHandle(), Is.EqualTo(testRig.Source.GetHandle()));
                    Assert.That(testRig.Graph.GetOutputCount(), Is.EqualTo(1));
                    if (iteration == 0)
                    {
                        stablePlayableCount = testRig.Graph.GetPlayableCount();
                    }
                    else
                    {
                        Assert.That(testRig.Graph.GetPlayableCount(), Is.EqualTo(stablePlayableCount));
                    }
                }
            }
        }

        [Test]
        public void TryRebuild_ReplacesADestroyedOutputWithTheLastRequestedProfileWithoutBlend()
        {
            using (TestRig testRig = TestRig.Create(false))
            {
                Assert.That(testRig.Controller.TryRebuild(), Is.True);
                BoneProfile firstProfile = testRig.CreateProfile();
                BoneProfile lastProfile = testRig.CreateProfile();
                SetFloat(lastProfile, "blendIn", 1f);
                try
                {
                    testRig.Controller.LinkProfile(firstProfile);
                    testRig.Graph.Evaluate();
                    testRig.Controller.PostAnimationUpdate();
                    AnimationPlayableOutput destroyedOutput = GetOutput(testRig.Controller);
                    testRig.Graph.DestroyOutput(destroyedOutput);

                    testRig.Controller.LinkProfile(lastProfile);
                    Assert.That(testRig.Controller.TryRebuild(), Is.True);

                    AnimationPlayableOutput rebuiltOutput = GetOutput(testRig.Controller);
                    Assert.That(rebuiltOutput.GetHandle(), Is.Not.EqualTo(destroyedOutput.GetHandle()));
                    Assert.That(rebuiltOutput.GetAnimationStreamSource(),
                        Is.EqualTo(AnimationStreamSource.PreviousInputs));
                    Assert.That(testRig.Controller.ActiveProfile, Is.SameAs(lastProfile));
                    Assert.That(
                        GetPlayable(testRig.Controller, "blendingPlayable")
                            .GetJobData<AnimationBlendingJob>().IsBlending,
                        Is.False);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(lastProfile);
                    UnityEngine.Object.DestroyImmediate(firstProfile);
                }
            }
        }

        [Test]
        public void Dispose_RepeatedCallsReleaseTheOutputAndPersistentArrays()
        {
            using (TestRig testRig = TestRig.Create(true, false, false))
            {
                CharacterBoneController controller = testRig.CreateController();
                Assert.That(controller.TryRebuild(), Is.True);

                Assert.That(() => controller.Dispose(), Throws.Nothing);
                Assert.That(() => controller.Dispose(), Throws.Nothing);

                Assert.That(GetOutput(controller).IsOutputValid(), Is.False);
                Assert.That(GetVirtualElementHandles(controller).IsCreated, Is.False);
                Assert.That(GetBlendingPoses(controller).IsCreated, Is.False);
                Assert.That(testRig.Graph.GetOutputCount(), Is.EqualTo(1));
            }
        }

        [Test]
        public void ProfileSwitch_BlendsTheCachedFullRigPoseBeforeCompleting()
        {
            using (TestRig testRig = TestRig.Create(true))
            {
                Assert.That(testRig.Controller.TryRebuild(), Is.True);
                BoneProfile firstProfile = testRig.CreateProfile();
                BoneProfile secondProfile = testRig.CreateProfile();
                SetLocalPositionLayerSettings layer = ScriptableObject.CreateInstance<SetLocalPositionLayerSettings>();
                layer.Configure(testRig.Rig);
                layer.TargetName = "TargetBone";
                layer.LocalPosition = new Vector3(1f, 0f, 0f);
                SetLayers(secondProfile, layer);
                SetFloat(secondProfile, "blendIn", 0.5f);
                try
                {
                    testRig.Controller.LinkProfile(firstProfile);
                    testRig.Graph.Evaluate();
                    testRig.Controller.PostAnimationUpdate();
                    Assert.That(testRig.Controller.ActiveProfile, Is.SameAs(firstProfile));
                    testRig.Target.localPosition = Vector3.zero;

                    testRig.Controller.LinkProfile(secondProfile);
                    testRig.Graph.Evaluate(0.01f);
                    testRig.Controller.PostAnimationUpdate();
                    Assert.That(testRig.Controller.ActiveProfile, Is.SameAs(secondProfile));

                    testRig.Graph.Evaluate(0.25f);
                    testRig.Controller.PostAnimationUpdate();
                    Assert.That(testRig.Target.localPosition.x, Is.GreaterThan(0f).And.LessThan(1f));

                    testRig.Graph.Evaluate(0.5f);
                    testRig.Controller.PostAnimationUpdate();
                    Assert.That(testRig.Target.localPosition.x, Is.EqualTo(1f).Within(0.001f));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(layer);
                    UnityEngine.Object.DestroyImmediate(secondProfile);
                    UnityEngine.Object.DestroyImmediate(firstProfile);
                }
            }
        }

        private static void SetFloat(BoneProfile profile, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayers(BoneProfile profile, params AnimationLayerSettings[] layers)
        {
            SerializedObject serialized = new SerializedObject(profile);
            SerializedProperty property = serialized.FindProperty("layers");
            property.arraySize = layers.Length;
            for (int index = 0; index < layers.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = layers[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationScriptPlayable GetPlayable(CharacterBoneController controller, string fieldName)
        {
            FieldInfo field = typeof(CharacterBoneController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (AnimationScriptPlayable)field.GetValue(controller);
        }

        private static AnimationPlayableOutput GetOutput(CharacterBoneController controller)
        {
            FieldInfo field = typeof(CharacterBoneController).GetField(
                "output",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (AnimationPlayableOutput)field.GetValue(controller);
        }

        private static NativeArray<VirtualElementHandle> GetVirtualElementHandles(
            CharacterBoneController controller)
        {
            FieldInfo field = typeof(CharacterBoneController).GetField(
                "virtualElementHandles",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (NativeArray<VirtualElementHandle>)field.GetValue(controller);
        }

        private static NativeArray<TransformStreamPose> GetBlendingPoses(
            CharacterBoneController controller)
        {
            FieldInfo field = typeof(CharacterBoneController).GetField(
                "blendingPoses",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (NativeArray<TransformStreamPose>)field.GetValue(controller);
        }

        private sealed class TestRig : IDisposable
        {
            private readonly GameObject root;
            private readonly KRig rig;
            private readonly Animator animator;
            private readonly KRigComponent rigComponent;
            private readonly List<CharacterAnimInstance> owners = new List<CharacterAnimInstance>();
            private readonly string animatorControllerPath;

            private TestRig(
                GameObject root,
                KRig rig,
                Animator animator,
                KRigComponent rigComponent,
                string animatorControllerPath,
                PlayableGraph graph,
                AnimationPlayableOutput output,
                Playable source,
                Transform target,
                Transform virtualTransform)
            {
                this.root = root;
                this.rig = rig;
                this.animator = animator;
                this.rigComponent = rigComponent;
                this.animatorControllerPath = animatorControllerPath;
                Graph = graph;
                Output = output;
                Source = source;
                Target = target;
                Virtual = virtualTransform;
                Controller = CreateController();
            }

            public CharacterBoneController Controller { get; private set; }
            public PlayableGraph Graph { get; }
            public AnimationPlayableOutput Output { get; }
            public Playable Source { get; }
            public Transform Target { get; }
            public Transform Virtual { get; }
            public KRig Rig => rig;
            public int RigCount => rig.Hierarchy.Count;

            public static TestRig Create(
                bool includeVirtualElement,
                bool duplicateVirtualElement = false,
                bool createController = true)
            {
                GameObject root = new GameObject("CharacterBoneControllerPipelineTests");
                Animator animator = root.AddComponent<Animator>();
                KRigComponent rigComponent = root.AddComponent<KRigComponent>();
                Transform target = null;
                Transform virtualTransform = null;
                if (includeVirtualElement)
                {
                    target = new GameObject("TargetBone").transform;
                    target.SetParent(root.transform, false);
                    virtualTransform = new GameObject("VirtualBone").transform;
                    virtualTransform.SetParent(root.transform, false);
                    ConfigureVirtualElement(virtualTransform.gameObject.AddComponent<KVirtualElement>(), target);
                    if (duplicateVirtualElement)
                    {
                        ConfigureVirtualElement(virtualTransform.gameObject.AddComponent<KVirtualElement>(), target);
                    }
                }

                KRig rig = ScriptableObject.CreateInstance<KRig>();
                rig.Import(rigComponent);
                rigComponent.Initialize(rig);
                string animatorControllerPath = AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/CharacterBoneControllerPipelineTests.controller");
                animator.runtimeAnimatorController =
                    AnimatorController.CreateAnimatorControllerAtPath(animatorControllerPath);
                animator.Rebind();
                animator.Update(0f);
                PlayableGraph graph = animator.playableGraph;
                AnimationPlayableOutput output = (AnimationPlayableOutput)graph.GetOutput(0);
                Playable source = output.GetSourcePlayable();
                TestRig result = new TestRig(
                    root,
                    rig,
                    animator,
                    rigComponent,
                    animatorControllerPath,
                    graph,
                    output,
                    source,
                    target,
                    virtualTransform);
                if (!createController)
                {
                    result.Controller.Dispose();
                    result.Controller = null;
                }

                return result;
            }

            public CharacterBoneController CreateController()
            {
                var owner = new CharacterAnimInstance(
                    new Pawn(root),
                    new TestAnimationSource(root.transform),
                    animator,
                    rigComponent);
                owners.Add(owner);
                return owner.BoneController;
            }

            public BoneProfile CreateProfile()
            {
                BoneProfile profile = ScriptableObject.CreateInstance<BoneProfile>();
                FieldInfo rigField = typeof(BoneProfile).GetField("rig", BindingFlags.Instance | BindingFlags.NonPublic);
                rigField.SetValue(profile, rig);
                return profile;
            }

            public void Dispose()
            {
                Controller?.Dispose();
                for (int index = owners.Count - 1; index >= 0; index--)
                {
                    owners[index].Dispose();
                }
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(animatorControllerPath);
            }

            private static void ConfigureVirtualElement(KVirtualElement element, Transform target)
            {
                SerializedObject serialized = new SerializedObject(element);
                serialized.FindProperty("targetBone").objectReferenceValue = target;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private sealed class TestAnimationSource : IAnimationCharacterSource
        {
            public TestAnimationSource(Transform transform)
            {
                Transform = transform;
            }

            public Transform Transform { get; }
            public Vector3 Velocity => Vector3.zero;
            public bool IsGrounded => true;
        }

        private sealed class SetLocalPositionLayerSettings : AnimationLayerSettings
        {
            public string TargetName { get; set; }
            public Vector3 LocalPosition { get; set; }

            public override IAnimationLayerJob CreateAnimationJob()
            {
                return new SetLocalPositionLayerJob(TargetName, LocalPosition);
            }
        }

        private sealed class SetLocalPositionLayerJob : IAnimationLayerJob
        {
            private readonly string targetName;
            private readonly Vector3 localPosition;
            private TransformStreamHandle targetHandle;
            private AnimationLayerSettings settings;

            public Type SettingsType => typeof(SetLocalPositionLayerSettings);

            public SetLocalPositionLayerJob(string targetName, Vector3 localPosition)
            {
                this.targetName = targetName;
                this.localPosition = localPosition;
            }

            public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
            {
                settings = layerSettings;
                Transform target = null;
                foreach (Transform candidate in jobData.RigComponent.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == targetName)
                    {
                        target = candidate;
                        break;
                    }
                }

                if (target == null) throw new InvalidOperationException("Missing test target: " + targetName);
                targetHandle = jobData.Animator.BindStreamTransform(target);
            }

            public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
            {
                return AnimationScriptPlayable.Create(
                    graph,
                    new SetLocalPositionJob
                    {
                        TargetHandle = targetHandle,
                        LocalPosition = localPosition
                    },
                    1);
            }

            public AnimationLayerSettings GetSettings() => settings;
            public void OnPreAnimationUpdate(float deltaTime, float weight) { }
            public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight) { }
            public void OnPostAnimationUpdate() { }
            public void Dispose() { }
        }

        private struct SetLocalPositionJob : IAnimationJob
        {
            public TransformStreamHandle TargetHandle;
            public Vector3 LocalPosition;

            public void ProcessAnimation(AnimationStream stream)
            {
                TargetHandle.SetLocalPosition(stream, LocalPosition);
            }

            public void ProcessRootMotion(AnimationStream stream) { }
        }
    }
}
