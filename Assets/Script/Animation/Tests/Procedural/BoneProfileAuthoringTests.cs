using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CGame.Animation.Editor;
using CGame.Animation.Rig;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation.Tests
{
    public sealed class BoneProfileAuthoringTests
    {
        private string testFolder;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            string folderName = "__BoneProfileAuthoringTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folderName);
            testFolder = "Assets/" + folderName;
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(testFolder) && AssetDatabase.IsValidFolder(testFolder))
            {
                AssetDatabase.DeleteAsset(testFolder);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void LayerTypeDropdown_ExposesApprovedLayerTypes()
        {
            Type[] expected =
            {
                typeof(PoseSamplerLayerSettings),
                typeof(PoseOffsetLayerSettings),
                typeof(AttachHandLayerSettings),
                typeof(ViewLayerSettings),
                typeof(AdditiveLayerSettings),
                typeof(LookLayerSettings),
                typeof(TurnLayerSettings),
                typeof(IkLayerSettings)
            };

            Assert.That(AnimationLayerTypeDropdown.SupportedTypes, Is.EquivalentTo(expected));
            Assert.That(AnimationLayerTypeDropdown.SupportedTypes.Count, Is.EqualTo(8));
        }

        [Test]
        public void LayerEditorWindow_RestoresSerializedLayerAfterDomainReloadCallback()
        {
            BoneProfile profile = CreatePersistentProfile();
            AnimationLayerSettings layer = BoneProfileLayerAssetService.AddLayer(
                profile,
                typeof(PoseSamplerLayerSettings));
            AnimationLayerEditorWindow.Open(layer);
            AnimationLayerEditorWindow window = EditorWindow.GetWindow<AnimationLayerEditorWindow>();
            FieldInfo serializedLayerField = typeof(AnimationLayerEditorWindow).GetField(
                "serializedLayer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo onEnable = typeof(AnimationLayerEditorWindow).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(serializedLayerField, Is.Not.Null);
            Assert.That(onEnable, Is.Not.Null);
            serializedLayerField.SetValue(window, null);
            onEnable.Invoke(window, null);

            SerializedObject restored = (SerializedObject)serializedLayerField.GetValue(window);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.targetObject, Is.SameAs(layer));
            window.Close();
        }

        [Test]
        public void LayerAssetService_AddMoveCopyPasteRemoveAndUndo_PreservesSubassets()
        {
            BoneProfile profile = CreatePersistentProfile();
            AnimationLayerSettings sampler = BoneProfileLayerAssetService.AddLayer(
                profile,
                typeof(PoseSamplerLayerSettings));
            AnimationLayerSettings sourceOffset = BoneProfileLayerAssetService.AddLayer(
                profile,
                typeof(PoseOffsetLayerSettings));
            AnimationLayerSettings targetOffset = BoneProfileLayerAssetService.AddLayer(
                profile,
                typeof(PoseOffsetLayerSettings));
            SetAlpha(sourceOffset, 0.25f);

            Assert.That(profile.Layers.Count, Is.EqualTo(3), "Three added subassets must be present before reorder.");
            Assert.That(AssetDatabase.IsSubAsset(sampler), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(sampler), Is.EqualTo(AssetDatabase.GetAssetPath(profile)));
            BoneProfileLayerAssetService.CopyLayer(profile, 1);
            Assert.That(BoneProfileLayerAssetService.CanPasteTo(profile, 2), Is.True);
            Assert.That(BoneProfileLayerAssetService.CanPasteTo(profile, 0), Is.False);
            BoneProfileLayerAssetService.PasteLayer(profile, 2);
            Assert.That(targetOffset.Alpha, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(targetOffset.Rig, Is.SameAs(profile.Rig));

            BoneProfileLayerAssetService.MoveLayer(profile, 2, 0);
            Assert.That(profile.Layers[0], Is.SameAs(targetOffset));
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile), ImportAssetOptions.ForceSynchronousImport);
            BoneProfile reloaded = AssetDatabase.LoadAssetAtPath<BoneProfile>(AssetDatabase.GetAssetPath(profile));
            Assert.That(reloaded.Layers.Select(layer => layer.GetType()).ToArray(), Is.EqualTo(new[]
            {
                typeof(PoseOffsetLayerSettings),
                typeof(PoseSamplerLayerSettings),
                typeof(PoseOffsetLayerSettings)
            }));

            BoneProfileLayerAssetService.RemoveLayer(reloaded, 1);
            Assert.That(reloaded.Layers.Count, Is.EqualTo(2));
            Undo.PerformUndo();
            Assert.That(reloaded.Layers.Count, Is.EqualTo(3), "Undo must restore the removed subasset reference.");
            Assert.That(reloaded.Layers[1], Is.Not.Null);
            Undo.PerformRedo();
            Assert.That(reloaded.Layers.Count, Is.EqualTo(2));
        }

        [Test]
        public void OwnershipValidation_RejectsExternalAndDuplicateLayerReferences()
        {
            BoneProfile profile = CreatePersistentProfile();
            PoseSamplerLayerSettings external = ScriptableObject.CreateInstance<PoseSamplerLayerSettings>();
            external.Configure(profile.Rig);
            AssetDatabase.CreateAsset(external, testFolder + "/ExternalLayer.asset");
            profile.Configure(profile.Rig, new AnimationLayerSettings[] { external, external });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            IReadOnlyList<string> errors = BoneProfileLayerAssetService.GetOwnershipErrors(profile);

            Assert.That(errors.Any(error => error.Contains("must be a subasset")), Is.True);
            Assert.That(errors.Any(error => error.Contains("duplicates subasset")), Is.True);
        }

        [Test]
        public void GenericValidation_RejectsAJobThatDeclaresAnotherSettingsType()
        {
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            MismatchedLayerSettings settings = ScriptableObject.CreateInstance<MismatchedLayerSettings>();
            BoneProfile profile = ScriptableObject.CreateInstance<BoneProfile>();
            try
            {
                settings.Configure(rig);
                profile.Configure(rig, new[] { settings });

                Assert.That(
                    () => profile.Validate(rig),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("expects PoseSamplerLayerSettings"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void WeaponValidator_AcceptsExactOrdersAndRejectsIllegalCombinations()
        {
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            BoneProfile knife = ScriptableObject.CreateInstance<BoneProfile>();
            BoneProfile ak12 = ScriptableObject.CreateInstance<BoneProfile>();
            List<AnimationLayerSettings> settings = new List<AnimationLayerSettings>();
            try
            {
                PoseSamplerLayerSettings knifeSampler = CreateSettings<PoseSamplerLayerSettings>(rig, settings);
                IkLayerSettings knifeIk = CreateSettings<IkLayerSettings>(rig, settings);
                knife.Configure(rig, new AnimationLayerSettings[] { knifeSampler, knifeIk });
                Assert.That(() => WeaponBoneProfileValidator.ValidateKnife(knife), Throws.Nothing);

                PoseSamplerLayerSettings akSampler = CreateSettings<PoseSamplerLayerSettings>(rig, settings);
                IkLayerSettings ik = CreateSettings<IkLayerSettings>(rig, settings);
                ak12.Configure(rig, new AnimationLayerSettings[] { akSampler, ik });
                Assert.That(() => WeaponBoneProfileValidator.ValidateAk12(ak12), Throws.Nothing);

                ak12.Configure(rig, new AnimationLayerSettings[] { ik, akSampler });
                Assert.That(
                    () => WeaponBoneProfileValidator.ValidateAk12(ak12),
                    Throws.TypeOf<InvalidOperationException>());
                knife.Configure(rig, new AnimationLayerSettings[] { knifeSampler });
                Assert.That(
                    () => WeaponBoneProfileValidator.ValidateKnife(knife),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                for (int index = settings.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object.DestroyImmediate(settings[index]);
                }

                UnityEngine.Object.DestroyImmediate(ak12);
                UnityEngine.Object.DestroyImmediate(knife);
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        private BoneProfile CreatePersistentProfile()
        {
            KRig rig = ScriptableObject.CreateInstance<KRig>();
            AssetDatabase.CreateAsset(rig, testFolder + "/Rig.asset");
            BoneProfile profile = ScriptableObject.CreateInstance<BoneProfile>();
            profile.Configure(rig, Array.Empty<AnimationLayerSettings>());
            AssetDatabase.CreateAsset(profile, testFolder + "/Profile.asset");
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T CreateSettings<T>(KRig rig, ICollection<AnimationLayerSettings> owner)
            where T : AnimationLayerSettings
        {
            T settings = ScriptableObject.CreateInstance<T>();
            settings.Configure(rig);
            owner.Add(settings);
            return settings;
        }

        private static void SetAlpha(AnimationLayerSettings settings, float value)
        {
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("alpha").floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class MismatchedLayerSettings : AnimationLayerSettings
        {
            public override IAnimationLayerJob CreateAnimationJob()
            {
                return new MismatchedLayerJob();
            }
        }

        private sealed class MismatchedLayerJob : IAnimationLayerJob
        {
            public Type SettingsType => typeof(PoseSamplerLayerSettings);
            public void Initialize(LayerJobData jobData, AnimationLayerSettings settings) { }
            public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Null;
            public AnimationLayerSettings GetSettings() => null;
            public void OnPreAnimationUpdate(float deltaTime, float weight) { }
            public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight) { }
            public void OnPostAnimationUpdate() { }
            public void Dispose() { }
        }
    }
}
