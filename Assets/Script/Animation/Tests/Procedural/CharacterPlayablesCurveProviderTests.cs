using System.Collections.Generic;
using CGame;
using CGame.Animation.Rig;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    public sealed class CharacterPlayablesCurveProviderTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GameObject root;
        private Animator animator;
        private CharacterAnimInstance controller;
        private KRig rig;
        private string animatorControllerPath;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(CharacterPlayablesCurveProviderTests));
            animator = root.AddComponent<Animator>();
            KRigComponent rigComponent = root.AddComponent<KRigComponent>();
            rig = ScriptableObject.CreateInstance<KRig>();
            rig.Import(rigComponent);
            rigComponent.Initialize(rig);
            animatorControllerPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/CharacterPlayablesCurveProviderTests.controller");
            AnimatorController animatorController =
                AnimatorController.CreateAnimatorControllerAtPath(animatorControllerPath);
            animatorController.AddParameter("WeaponBoneWeight", AnimatorControllerParameterType.Float);
            animatorController.AddParameter("FullBodyWeight", AnimatorControllerParameterType.Float);
            animatorController.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            animatorController.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            animatorController.AddParameter("Velocity", AnimatorControllerParameterType.Float);
            animatorController.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            animatorController.AddParameter("InAir", AnimatorControllerParameterType.Bool);
            animatorController.AddParameter("Sprinting", AnimatorControllerParameterType.Float);
            animator.runtimeAnimatorController = animatorController;
            animator.Rebind();
            animator.Update(0f);

            controller = new CharacterAnimInstance(
                new Pawn(root),
                new TestAnimationSource(root.transform),
                animator,
                rigComponent);
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();
            if (rig != null) Object.DestroyImmediate(rig);
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }

            createdObjects.Clear();
            if (root != null) Object.DestroyImmediate(root);
            if (!string.IsNullOrEmpty(animatorControllerPath))
            {
                AssetDatabase.DeleteAsset(animatorControllerPath);
            }
        }

        [Test]
        public void GetCurveValue_ReturnsZeroWithoutAnActiveSlotOrNamedCurve()
        {
            Assert.That(controller.GetCurveValue("WeaponBoneWeight"), Is.Zero);

            AnimationClipAsset asset = CreateAsset("MissingCurve", 0f, null);
            controller.PlayAbilityAnimation(asset, 1);
            GetPlayablesController().Update(0.1f);

            Assert.That(controller.GetCurveValue("WeaponBoneWeight"), Is.Zero);
        }

        [Test]
        public void GetCurveValue_ReturnsTheActiveSlotNamedCurve()
        {
            AnimationClipAsset asset = CreateAsset("NamedCurve", 0f, 0.75f);
            animator.SetFloat("WeaponBoneWeight", 0.25f);

            controller.PlayAbilityAnimation(asset, 1);
            GetPlayablesController().Update(0.1f);

            Assert.That(controller.GetCurveValue("WeaponBoneWeight"),
                Is.EqualTo(0.75f).Within(0.00001f));
        }

        [Test]
        public void GetCurveValue_BlendsOnlySlotNamedCurvesByTheirInputWeights()
        {
            AnimationClipAsset first = CreateAsset("FirstCurve", 0f, 0.2f);
            AnimationClipAsset second = CreateAsset("SecondCurve", 1f, 1f);
            controller.PlayAbilityAnimation(first, 1);
            GetPlayablesController().Update(0.1f);
            Assert.That(controller.GetCurveValue("WeaponBoneWeight"),
                Is.EqualTo(0.2f).Within(0.00001f));

            controller.PlayAbilityAnimation(second, 2);
            CharacterPlayablesController playablesController = GetPlayablesController();
            AnimationLayerMixerPlayable slotMixer = (AnimationLayerMixerPlayable)typeof(CharacterPlayablesController)
                .GetProperty("SlotMixer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(playablesController);
            slotMixer.SetInputWeight(1, 0.5f);
            slotMixer.SetInputWeight(2, 0.5f);

            Assert.That(controller.GetCurveValue("WeaponBoneWeight"),
                Is.EqualTo(0.6f).Within(0.00001f));
        }

        [Test]
        public void GetCurveValue_FallsBackToAnimatorWhenMixerIsZero()
        {
            animator.SetFloat("WeaponBoneWeight", 0.4f);

            Assert.That(controller.GetCurveValue("WeaponBoneWeight"),
                Is.EqualTo(0.4f).Within(0.00001f));
        }

        [Test]
        public void LayerWeight_UsesTheExplicitAnimatorSource()
        {
            ViewLayerSettings settings = CreateLayerSettings(
                "WeaponBoneWeight",
                AnimationCurveBlendSource.Animator);

            animator.SetFloat("WeaponBoneWeight", 0.4f);
            AnimationClipAsset asset = CreateAsset("OwnerWeightCurve", 0f, 0.75f);
            controller.PlayAbilityAnimation(asset, 7);
            GetPlayablesController().Update(0.1f);

            Assert.That(settings.EvaluateWeight(controller), Is.EqualTo(0.4f).Within(0.00001f));
        }

        [Test]
        public void ViewFullBodyWeightMask_MapsAnimatorZeroToOneAndOneToZero()
        {
            ViewLayerSettings settings = CreateLayerSettings(
                "FullBodyWeight",
                AnimationCurveBlendSource.Animator,
                AnimationCurveBlendMode.Mask);

            animator.SetFloat("FullBodyWeight", 0f);
            Assert.That(settings.EvaluateWeight(controller), Is.EqualTo(1f).Within(0.00001f));

            animator.SetFloat("FullBodyWeight", 1f);
            Assert.That(settings.EvaluateWeight(controller), Is.Zero.Within(0.00001f));
        }

        [Test]
        public void LayerWeight_UsesTheExplicitPlayablesSource()
        {
            ViewLayerSettings settings = CreateLayerSettings(
                "WeaponBoneWeight",
                AnimationCurveBlendSource.Playables);
            animator.SetFloat("WeaponBoneWeight", 0.4f);
            AnimationClipAsset asset = CreateAsset("OwnerWeightCurve", 0f, 0.75f);
            controller.PlayAbilityAnimation(asset, 7);
            GetPlayablesController().Update(0.1f);

            Assert.That(settings.EvaluateWeight(controller), Is.EqualTo(0.75f).Within(0.00001f));
        }

        [Test]
        public void LayerWeight_UsesTheExplicitContextSource()
        {
            ViewLayerSettings settings = CreateLayerSettings(
                "AimingWeight",
                AnimationCurveBlendSource.Context);
            typeof(AnimationUpdateContext)
                .GetMethod("SetAimingWeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller.UpdateContext, new object[] { 0.65f });

            Assert.That(settings.EvaluateWeight(controller), Is.EqualTo(0.65f).Within(0.00001f));
        }

        private CharacterPlayablesController GetPlayablesController()
        {
            return (CharacterPlayablesController)typeof(CharacterAnimInstance)
                .GetField("playablesController", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(controller);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private ViewLayerSettings CreateLayerSettings(
            string curveName,
            AnimationCurveBlendSource source,
            AnimationCurveBlendMode mode = AnimationCurveBlendMode.Direct)
        {
            var settings = ScriptableObject.CreateInstance<ViewLayerSettings>();
            var blend = new AnimationCurveBlend();
            SetPrivateField(blend, "curveName", curveName);
            SetPrivateField(blend, "mode", mode);
            SetPrivateField(blend, "clampMinimum", 0f);
            SetPrivateField(blend, "source", source);
            typeof(AnimationLayerSettings)
                .GetField("curveBlending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(settings, new[] { blend });
            createdObjects.Add(settings);
            return settings;
        }

        private AnimationClipAsset CreateAsset(string name, float blendInTime, float? curveValue)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 0f));
            var asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(asset.TryInitialize(clip), Is.True);
            asset.BlendInTime = blendInTime;
            asset.BlendOutTime = 0f;
            if (curveValue.HasValue)
            {
                asset.SetNamedCurve(
                    "WeaponBoneWeight",
                    AnimationCurve.Constant(0f, 1f, curveValue.Value));
            }

            createdObjects.Add(asset);
            createdObjects.Add(clip);
            return asset;
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
