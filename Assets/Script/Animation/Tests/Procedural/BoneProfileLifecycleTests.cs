using System;
using System.Linq;
using System.Reflection;
using CGame.Animation.Rig;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    public sealed class BoneProfileLifecycleTests
    {
        private GameObject root;
        private KRig rig;
        private PlayableGraph graph;
        private CharacterBoneController controller;
        private string animatorControllerPath;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(BoneProfileLifecycleTests));
            Animator animator = root.AddComponent<Animator>();
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            rig = ScriptableObject.CreateInstance<KRig>();
            rig.Import(rigComponent);
            rigComponent.Initialize(rig);

            animatorControllerPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/BoneProfileLifecycleTests.controller");
            animator.runtimeAnimatorController =
                AnimatorController.CreateAnimatorControllerAtPath(animatorControllerPath);
            animator.Rebind();
            animator.Update(0f);
            graph = animator.playableGraph;
            controller = new CharacterBoneController(
                animator,
                rigComponent,
                new AnimationUpdateContext(new TestAnimationSource(root.transform)));
            Assert.That(controller.TryRebuild(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();
            if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (!string.IsNullOrEmpty(animatorControllerPath))
            {
                AssetDatabase.DeleteAsset(animatorControllerPath);
            }
        }

        [Test]
        public void LinkProfile_RejectsNull()
        {
            Assert.That(() => controller.LinkProfile(null), Throws.ArgumentNullException);
        }

        [Test]
        public void LinkProfile_AppliesOnlyAfterTheCachedPoseEvaluation()
        {
            BoneProfile profile = CreateProfile();
            try
            {
                controller.LinkProfile(profile);
                Assert.That(controller.ActiveProfile, Is.Null);

                EvaluateAndPostUpdate();

                Assert.That(controller.ActiveProfile, Is.SameAs(profile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LinkProfile_LastWriteWinsWithinTheSameFrame()
        {
            BoneProfile first = CreateProfile();
            BoneProfile second = CreateProfile();
            try
            {
                controller.LinkProfile(first);
                controller.LinkProfile(second);

                EvaluateAndPostUpdate();

                Assert.That(controller.ActiveProfile, Is.SameAs(second));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void UnlinkProfile_ClearsTheProfileAfterTheCachedPoseEvaluation()
        {
            BoneProfile profile = CreateProfile();
            try
            {
                controller.LinkProfile(profile);
                EvaluateAndPostUpdate();

                controller.UnlinkProfile();
                Assert.That(controller.ActiveProfile, Is.SameAs(profile));

                EvaluateAndPostUpdate();
                Assert.That(controller.ActiveProfile, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [TestCase(0f, false)]
        [TestCase(0.5f, true)]
        public void LinkProfile_ConfiguresTheRequestedBlend(float blendIn, bool expectedBlending)
        {
            BoneProfile profile = CreateProfile();
            try
            {
                SetFloat(profile, "blendIn", blendIn);
                controller.LinkProfile(profile);
                EvaluateAndPostUpdate();

                AnimationBlendingJob job = GetBlendingPlayable().GetJobData<AnimationBlendingJob>();
                Assert.That(job.BlendDuration, Is.EqualTo(blendIn));
                Assert.That(job.IsBlending, Is.EqualTo(expectedBlending));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ConsecutiveProfileSwitches_DisposeEachReplacedLayerExactlyOnce()
        {
            BoneProfile firstProfile = CreateProfile();
            BoneProfile secondProfile = CreateProfile();
            TrackingLayerSettings firstLayer = ScriptableObject.CreateInstance<TrackingLayerSettings>();
            TrackingLayerSettings secondLayer = ScriptableObject.CreateInstance<TrackingLayerSettings>();
            firstLayer.Configure(rig);
            secondLayer.Configure(rig);
            SetLayers(firstProfile, firstLayer);
            SetLayers(secondProfile, secondLayer);
            try
            {
                controller.LinkProfile(firstProfile);
                EvaluateAndPostUpdate();
                Assert.That(firstLayer.DisposeCount, Is.Zero);

                controller.LinkProfile(secondProfile);
                EvaluateAndPostUpdate();
                Assert.That(firstLayer.DisposeCount, Is.EqualTo(1));
                Assert.That(secondLayer.DisposeCount, Is.Zero);

                controller.Dispose();
                Assert.That(firstLayer.DisposeCount, Is.EqualTo(1));
                Assert.That(secondLayer.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondLayer);
                UnityEngine.Object.DestroyImmediate(firstLayer);
                UnityEngine.Object.DestroyImmediate(secondProfile);
                UnityEngine.Object.DestroyImmediate(firstProfile);
            }
        }

        [Test]
        public void PublicApiAndLayerJobData_ExposeOnlyTheSimplifiedContract()
        {
            MethodInfo link = typeof(CharacterBoneController).GetMethod(nameof(CharacterBoneController.LinkProfile));
            MethodInfo unlink = typeof(CharacterBoneController).GetMethod(nameof(CharacterBoneController.UnlinkProfile));
            Assert.That(link.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(link.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(BoneProfile) }));
            Assert.That(unlink.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(unlink.GetParameters(), Is.Empty);
            Assert.That(typeof(CharacterBoneController).GetMethod("LinkLayer"), Is.Null);

            string[] properties = typeof(LayerJobData)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(properties, Is.EqualTo(new[]
            {
                nameof(LayerJobData.Animator),
                nameof(LayerJobData.CharacterRootHandle),
                nameof(LayerJobData.RigComponent),
                nameof(LayerJobData.UpdateContext)
            }.OrderBy(name => name).ToArray()));
        }

        private BoneProfile CreateProfile()
        {
            BoneProfile profile = ScriptableObject.CreateInstance<BoneProfile>();
            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty("rig").objectReferenceValue = rig;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private void EvaluateAndPostUpdate()
        {
            graph.Evaluate(0.01f);
            controller.PostAnimationUpdate();
        }

        private AnimationScriptPlayable GetBlendingPlayable()
        {
            FieldInfo field = typeof(CharacterBoneController).GetField(
                "blendingPlayable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (AnimationScriptPlayable)field.GetValue(controller);
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

        private sealed class TrackingLayerSettings : AnimationLayerSettings
        {
            public int DisposeCount { get; private set; }

            public override IAnimationLayerJob CreateAnimationJob()
            {
                return new TrackingLayerJob(this);
            }

            public void RecordDispose()
            {
                DisposeCount++;
            }
        }

        private sealed class TrackingLayerJob : IAnimationLayerJob
        {
            private readonly TrackingLayerSettings settings;
            private bool isInitialized;

            public TrackingLayerJob(TrackingLayerSettings settings)
            {
                this.settings = settings;
            }

            public Type SettingsType => typeof(TrackingLayerSettings);
            public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
            {
                isInitialized = true;
            }
            public AnimationScriptPlayable CreatePlayable(PlayableGraph playableGraph)
            {
                return AnimationScriptPlayable.Create(playableGraph, new TrackingJob(), 1);
            }

            public AnimationLayerSettings GetSettings() => settings;
            public void OnPreAnimationUpdate() { }
            public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight) { }
            public void OnPostAnimationUpdate() { }
            public void Dispose()
            {
                if (isInitialized)
                {
                    settings.RecordDispose();
                    isInitialized = false;
                }
            }
        }

        private struct TrackingJob : IAnimationJob
        {
            public void ProcessAnimation(AnimationStream stream) { }
            public void ProcessRootMotion(AnimationStream stream) { }
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
    }
}
