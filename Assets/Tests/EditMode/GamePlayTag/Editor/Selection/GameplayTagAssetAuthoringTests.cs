using System;
using System.Linq;
using CGame.GameplayTags.Tests.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor.Tests
{
    public sealed class GameplayTagAssetAuthoringTests
    {
        private const string TemporaryFolder = "Assets/Tests/TempGameplayTagAuthoring";
        private GameplayTagPickerModel model;

        [SetUp]
        public void SetUp()
        {
            var source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(
                "AuthoringTests",
                new[]
                {
                    new GameplayTagSourceNode(
                        "Combat",
                        children: new[]
                        {
                            new GameplayTagSourceNode(
                                "Weapon",
                                children: new[]
                                {
                                    new GameplayTagSourceNode("Reload", true),
                                    new GameplayTagSourceNode("Fire", true)
                                }),
                            new GameplayTagSourceNode("ImplicitOnly")
                        }),
                    new GameplayTagSourceNode("UI", children: new[] { new GameplayTagSourceNode("Menu", true) })
                });
            Assert.That(GameplayTagManager.Instance.Initialize(new[] { source }).Succeeded, Is.True);
            model = new GameplayTagPickerModel(GameplayTagManager.Instance.GetExplicitTags());
            UnityEngine.Object.DestroyImmediate(source);
        }

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            GameplayTagPickerModel.InvalidateAuthoritativeConfigCache();
            AssetDatabase.DeleteAsset(TemporaryFolder);
        }

        [Test]
        public void PickerModel_ContainsOnlyExplicitTagsInStableOrder()
        {
            Assert.That(
                model.Names,
                Is.EqualTo(new[] { "Combat.Weapon.Fire", "Combat.Weapon.Reload", "UI.Menu" }));
            Assert.That(model.Names, Does.Not.Contain("Combat"));
            Assert.That(model.Names, Does.Not.Contain("Combat.ImplicitOnly"));
        }

        [Test]
        public void PickerModel_SearchesFullPathAndSegmentsCaseInsensitively()
        {
            Assert.That(model.Search("weapon"), Is.EqualTo(new[] { "Combat.Weapon.Fire", "Combat.Weapon.Reload" }));
            Assert.That(model.Search("RELOAD"), Is.EqualTo(new[] { "Combat.Weapon.Reload" }));
            Assert.That(model.Search("Combat.Weapon.Fire"), Is.EqualTo(new[] { "Combat.Weapon.Fire" }));
        }

        [Test]
        public void SingleTagWriter_PreservesInvalidRawValueUntilExplicitSelection()
        {
            GameplayTagAuthoringFixtureAsset asset = ScriptableObject.CreateInstance<GameplayTagAuthoringFixtureAsset>();
            try
            {
                var serializedObject = new SerializedObject(asset);
                SerializedProperty tag = serializedObject.FindProperty("singleTag");
                tag.FindPropertyRelative("tagName").stringValue = "Ghost.Missing";
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(GameplayTagSerializedPropertyWriter.TryAssignTag(tag, "Ghost.Missing", model, out _), Is.False);
                Assert.That(GameplayTagSerializedPropertyWriter.ReadTagName(tag), Is.EqualTo("Ghost.Missing"));
                Assert.That(GameplayTagSerializedPropertyWriter.TryAssignTag(tag, "combat.weapon.fire", model, out string error), Is.True, error);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(asset.SingleTag.Name, Is.EqualTo("Combat.Weapon.Fire"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ContainerWriter_RejectsDuplicateAndRebuildsRuntimeCache()
        {
            GameplayTagAuthoringFixtureAsset asset = ScriptableObject.CreateInstance<GameplayTagAuthoringFixtureAsset>();
            try
            {
                var serializedObject = new SerializedObject(asset);
                SerializedProperty container = serializedObject.FindProperty("tags");
                Assert.That(GameplayTagSerializedPropertyWriter.AddEmptyContainerRow(container, out string error), Is.True, error);
                Assert.That(GameplayTagSerializedPropertyWriter.TryAssignContainerElement(container, 0, "Combat.Weapon.Fire", model, out error), Is.True, error);
                Assert.That(GameplayTagSerializedPropertyWriter.AddEmptyContainerRow(container, out error), Is.True, error);
                Assert.That(GameplayTagSerializedPropertyWriter.TryAssignContainerElement(container, 1, "combat.weapon.fire", model, out error), Is.False);
                Assert.That(error, Does.Contain("already"));
                Assert.That(GameplayTagSerializedPropertyWriter.TryAssignContainerElement(container, 1, "Combat.Weapon.Reload", model, out error), Is.True, error);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                GameplayTagSerializedPropertyWriter.RefreshContainerCaches(new UnityEngine.Object[] { asset }, "tags");

                Assert.That(asset.Tags.Count, Is.EqualTo(2));
                Assert.That(asset.Tags.Tags.Select(tag => tag.Name), Is.EqualTo(new[] { "Combat.Weapon.Fire", "Combat.Weapon.Reload" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ScriptableObject_SaveAndReload_PreservesTagAndContainer()
        {
            EnsureTemporaryFolder();
            string path = $"{TemporaryFolder}/AuthoringFixture.asset";
            GameplayTagAuthoringFixtureAsset asset = ScriptableObject.CreateInstance<GameplayTagAuthoringFixtureAsset>();
            AssetDatabase.CreateAsset(asset, path);
            WriteFixture(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            GameplayTagAuthoringFixtureAsset reloaded = AssetDatabase.LoadAssetAtPath<GameplayTagAuthoringFixtureAsset>(path);
            Assert.That(reloaded.SingleTag.Name, Is.EqualTo("Combat.Weapon.Fire"));
            Assert.That(reloaded.Tags.Count, Is.EqualTo(1));
            Assert.That(reloaded.Tags.Tags.Single().Name, Is.EqualTo("Combat.Weapon.Reload"));
        }

        [Test]
        public void Prefab_SaveAndReload_PreservesTagAndContainer()
        {
            EnsureTemporaryFolder();
            string path = $"{TemporaryFolder}/AuthoringFixture.prefab";
            var gameObject = new GameObject("AuthoringFixture");
            try
            {
                GameplayTagAuthoringFixtureComponent component = gameObject.AddComponent<GameplayTagAuthoringFixtureComponent>();
                WriteFixture(component);
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            GameplayTagAuthoringFixtureComponent reloaded = AssetDatabase
                .LoadAssetAtPath<GameObject>(path)
                .GetComponent<GameplayTagAuthoringFixtureComponent>();
            Assert.That(reloaded.SingleTag.Name, Is.EqualTo("Combat.Weapon.Fire"));
            Assert.That(reloaded.Tags.Count, Is.EqualTo(1));
            Assert.That(reloaded.Tags.Tags.Single().Name, Is.EqualTo("Combat.Weapon.Reload"));
        }

        private void WriteFixture(UnityEngine.Object target)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty singleTag = serializedObject.FindProperty("singleTag");
            SerializedProperty container = serializedObject.FindProperty("tags");
            Assert.That(GameplayTagSerializedPropertyWriter.TryAssignTag(singleTag, "combat.weapon.fire", model, out string error), Is.True, error);
            Assert.That(GameplayTagSerializedPropertyWriter.AddEmptyContainerRow(container, out error), Is.True, error);
            Assert.That(GameplayTagSerializedPropertyWriter.TryAssignContainerElement(container, 0, "combat.weapon.reload", model, out error), Is.True, error);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            GameplayTagSerializedPropertyWriter.RefreshContainerCaches(new[] { target }, "tags");
            EditorUtility.SetDirty(target);
        }

        private static void EnsureTemporaryFolder()
        {
            if (!AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.CreateFolder("Assets/Tests", "TempGameplayTagAuthoring");
            }
        }
    }
}
