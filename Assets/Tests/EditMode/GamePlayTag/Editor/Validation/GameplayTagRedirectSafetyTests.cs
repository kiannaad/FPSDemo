using System;
using System.Collections.Generic;
using System.Linq;
using CGame.GameplayTags.Tests.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor.Tests
{
    public sealed class GameplayTagRedirectSafetyTests
    {
        private const string TemporaryFolder = "Assets/Tests/TempGameplayTagRedirectSafety";
        private const string TemporaryProductionFolder = "Assets/TempGameplayTagRedirectSafetyTests";
        private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();
        private GameplayTagSource source;
        private GameplayTagConfig config;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(
                "SafetyTests",
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
                                    new GameplayTagSourceNode("Fire", true),
                                    new GameplayTagSourceNode("Reload", true)
                                })
                        }),
                    new GameplayTagSourceNode("UI", true)
                });
            objectsToDestroy.Add(source);

            var configObject = new GameObject("GameplayTagSafetyConfig");
            config = configObject.AddComponent<GameplayTagConfig>();
            config.SetDefinition(
                new[] { source },
                new[] { new GameplayTagRedirect("Old.Fire", "Combat.Weapon.Fire") });
            objectsToDestroy.Add(configObject);
            EnsureTemporaryFolder();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            AssetDatabase.DeleteAsset(TemporaryFolder);
            AssetDatabase.DeleteAsset(TemporaryProductionFolder);
            foreach (UnityEngine.Object target in objectsToDestroy)
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void Scanner_LocatesStronglyTypedFieldsAndClassifiesRedirects()
        {
            string path = CreateAsset("Scanner.asset", "Old.Fire", "Ghost.Missing", "Combat.Weapon.Reload");

            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanAssetPaths(config, new[] { path });

            Assert.That(report.ConfigurationErrors, Is.Empty);
            Assert.That(report.References.Select(reference => reference.PropertyPath), Does.Contain("singleTag.tagName"));
            Assert.That(report.References.Single(reference => reference.RawName == "Old.Fire").Status,
                Is.EqualTo(GameplayTagReferenceStatus.NeedsMigration));
            Assert.That(report.References.Single(reference => reference.RawName == "Old.Fire").ResolvedName,
                Is.EqualTo("Combat.Weapon.Fire"));
            Assert.That(report.References.Single(reference => reference.RawName == "Ghost.Missing").Status,
                Is.EqualTo(GameplayTagReferenceStatus.Unregistered));
            Assert.That(report.References.Single(reference => reference.RawName == "Combat.Weapon.Reload").Status,
                Is.EqualTo(GameplayTagReferenceStatus.Registered));
        }

        [Test]
        public void FixAll_WritesResolvableReferencesAndRescanLeavesUnknownValuesUntouched()
        {
            string path = CreateAsset("Migration.asset", "Old.Fire", "Ghost.Missing", "Combat.Weapon.Reload");
            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanAssetPaths(config, new[] { path });

            GameplayTagAssetMigrationResult result = GameplayTagAssetMigrationService.FixAll(config, report);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.SavedAssets, Is.EqualTo(new[] { path }));
            Assert.That(result.Rescan.References.Any(reference => reference.Status == GameplayTagReferenceStatus.NeedsMigration), Is.False);
            Assert.That(result.Rescan.References.Single(reference => reference.RawName == "Ghost.Missing").Status,
                Is.EqualTo(GameplayTagReferenceStatus.Unregistered));
            GameplayTagAuthoringFixtureAsset reloaded = AssetDatabase.LoadAssetAtPath<GameplayTagAuthoringFixtureAsset>(path);
            Assert.That(reloaded.SingleTag.Name, Is.EqualTo("Combat.Weapon.Fire"));
            Assert.That(reloaded.UnregisteredTag.Name, Is.EqualTo("Ghost.Missing"));
        }

        [Test]
        public void FixAll_WritesMultipleRedirectedFieldsInTheSameAsset()
        {
            config.SetDefinition(
                new[] { source },
                new[]
                {
                    new GameplayTagRedirect("Old.Fire", "Combat.Weapon.Fire"),
                    new GameplayTagRedirect("Old.Reload", "Combat.Weapon.Reload")
                });
            string path = CreateAsset("MultipleMigration.asset", "Old.Fire", "Old.Reload", "Combat.Weapon.Fire");
            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanAssetPaths(config, new[] { path });

            GameplayTagAssetMigrationResult result = GameplayTagAssetMigrationService.FixAll(config, report);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Rescan.HasBlockingIssues, Is.False);
            GameplayTagAuthoringFixtureAsset reloaded = AssetDatabase.LoadAssetAtPath<GameplayTagAuthoringFixtureAsset>(path);
            Assert.That(reloaded.SingleTag.Name, Is.EqualTo("Combat.Weapon.Fire"));
            Assert.That(reloaded.UnregisteredTag.Name, Is.EqualTo("Combat.Weapon.Reload"));
        }

        [Test]
        public void PrefabScannerAndFix_PreservePreciseComponentAndPropertyLocation()
        {
            string path = $"{TemporaryFolder}/Migration.prefab";
            var gameObject = new GameObject("MigrationPrefab");
            try
            {
                GameplayTagAuthoringFixtureComponent component = gameObject.AddComponent<GameplayTagAuthoringFixtureComponent>();
                WriteRawValues(component, "Old.Fire", string.Empty, "Combat.Weapon.Reload");
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            GameplayTagAssetScanReport report = GameplayTagAssetScanner.ScanAssetPaths(config, new[] { path });
            GameplayTagAssetReference redirected = report.References.Single(reference => reference.RawName == "Old.Fire");
            GameplayTagAssetMigrationResult result = GameplayTagAssetMigrationService.FixThis(config, redirected);

            Assert.That(result.Errors, Is.Empty);
            GameplayTagAuthoringFixtureComponent reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                .GetComponent<GameplayTagAuthoringFixtureComponent>();
            Assert.That(reloaded.SingleTag.Name, Is.EqualTo("Combat.Weapon.Fire"));
            Assert.That(reloaded.Tags.Tags.Single().Name, Is.EqualTo("Combat.Weapon.Reload"));
        }

        [Test]
        public void RenamePlan_GeneratesEveryExplicitRedirectAndAppliesValidatedSnapshot()
        {
            bool created = GameplayTagRenameService.TryCreatePlan(
                config,
                source,
                "Combat.Weapon",
                "Arsenal.Gun",
                out GameplayTagRenamePlan plan,
                out string error);

            Assert.That(created, Is.True, error);
            Assert.That(
                plan.Mappings.Select(mapping => $"{mapping.OldName}->{mapping.NewName}"),
                Is.EqualTo(new[]
                {
                    "Combat.Weapon.Fire->Arsenal.Gun.Fire",
                    "Combat.Weapon.Reload->Arsenal.Gun.Reload"
                }));
            Assert.That(GameplayTagRenameService.Apply(plan, out error), Is.True, error);

            GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(config.Sources, config.Redirects);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.TryResolveName("Combat.Weapon.Fire", out string resolved), Is.True);
            Assert.That(resolved, Is.EqualTo("Arsenal.Gun.Fire"));
            Assert.That(source.Roots.Select(root => root.SegmentName), Is.EqualTo(new[] { "Arsenal", "UI" }));
        }

        [Test]
        public void RenamePlan_RejectsRedirectCycleWithoutMutatingSourceOrConfig()
        {
            config.SetDefinition(
                new[] { source },
                new[]
                {
                    new GameplayTagRedirect("Legacy.One", "Legacy.Two"),
                    new GameplayTagRedirect("Legacy.Two", "Legacy.One")
                });
            string[] originalRoots = source.Roots.Select(root => root.SegmentName).ToArray();
            GameplayTagRedirect[] originalRedirects = config.Redirects.ToArray();

            bool created = GameplayTagRenameService.TryCreatePlan(
                config,
                source,
                "Combat.Weapon",
                "Arsenal.Gun",
                out _,
                out string error);

            Assert.That(created, Is.False);
            Assert.That(error, Does.Contain("RedirectCycle"));
            Assert.That(source.Roots.Select(root => root.SegmentName), Is.EqualTo(originalRoots));
            Assert.That(config.Redirects, Is.EqualTo(originalRedirects));
        }

        [Test]
        public void RedirectValidation_RejectsOneToManyAndManyToOneDefinitions()
        {
            GameplayTagRegistryBuildResult result = new GameplayTagRegistryBuilder().Build(
                new[] { source },
                new[]
                {
                    new GameplayTagRedirect("Legacy.Shared", "Combat.Weapon.Fire"),
                    new GameplayTagRedirect("Legacy.Shared", "Combat.Weapon.Reload"),
                    new GameplayTagRedirect("Legacy.Other", "Combat.Weapon.Fire")
                });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors.Select(item => item.Code), Does.Contain("DuplicateRedirect"));
            Assert.That(result.Errors.Select(item => item.Code), Does.Contain("RedirectTargetConflict"));
        }

        [Test]
        public void RenamePlan_RejectsDestinationCollisionWithoutMutatingSourceOrConfig()
        {
            string[] originalRoots = source.Roots.Select(root => root.SegmentName).ToArray();
            int originalRedirectCount = config.Redirects.Count;

            bool created = GameplayTagRenameService.TryCreatePlan(
                config,
                source,
                "Combat.Weapon",
                "UI",
                out _,
                out string error);

            Assert.That(created, Is.False);
            Assert.That(error, Does.Contain("already exists"));
            Assert.That(source.Roots.Select(root => root.SegmentName), Is.EqualTo(originalRoots));
            Assert.That(config.Redirects.Count, Is.EqualTo(originalRedirectCount));
        }

        [Test]
        public void DestructiveOperations_AreBlockedWhileAProductionAssetReferencesTheSource()
        {
            EnsureTemporaryProductionFolder();
            string path = CreateAssetAt(
                $"{TemporaryProductionFolder}/Referenced.asset",
                "Combat.Weapon.Fire",
                string.Empty,
                "Combat.Weapon.Reload");

            bool deleted = GameplayTagSourceMutationService.DeleteSubtreeSafely(
                config,
                source,
                "Combat.Weapon",
                out string deleteError);
            bool removed = GameplayTagSourceAssetService.Remove(config, source, out string removeError);

            Assert.That(deleted, Is.False);
            Assert.That(deleteError, Does.Contain(path));
            Assert.That(removed, Is.False);
            Assert.That(removeError, Does.Contain(path));
            Assert.That(source.Roots.Single(root => root.SegmentName == "Combat").Children, Is.Not.Empty);
            Assert.That(config.Sources, Does.Contain(source));

            Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            Assert.That(GameplayTagSourceAssetService.Remove(config, source, out removeError), Is.True, removeError);
            Assert.That(config.Sources, Is.Empty);
        }

        [Test]
        public void DeleteSourceAsset_IsBlockedByRawReferencesEvenWhenSourceIsNotRegistered()
        {
            EnsureTemporaryProductionFolder();
            string referencePath = CreateAssetAt(
                $"{TemporaryProductionFolder}/LegacyReference.asset",
                "Legacy.Tag",
                string.Empty,
                string.Empty);
            string sourcePath = $"{TemporaryProductionFolder}/LegacySource.asset";
            GameplayTagSource legacySource = ScriptableObject.CreateInstance<GameplayTagSource>();
            legacySource.SetDefinition("LegacySource", new[] { new GameplayTagSourceNode("Legacy", children: new[] { new GameplayTagSourceNode("Tag", true) }) });
            AssetDatabase.CreateAsset(legacySource, sourcePath);
            AssetDatabase.SaveAssets();

            bool deleted = GameplayTagSourceAssetService.DeleteAsset(config, legacySource, out string error);

            Assert.That(deleted, Is.False);
            Assert.That(error, Does.Contain(referencePath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameplayTagSource>(sourcePath), Is.Not.Null);

            Assert.That(AssetDatabase.DeleteAsset(referencePath), Is.True);
            Assert.That(GameplayTagSourceAssetService.DeleteAsset(config, legacySource, out error), Is.True, error);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameplayTagSource>(sourcePath), Is.Null);
        }

        [Test]
        public void ValidationAndBuildGate_BlockUnknownProductionReferencesAndPassAfterRemoval()
        {
            EnsureTemporaryProductionFolder();
            string path = CreateAssetAt(
                $"{TemporaryProductionFolder}/Invalid.asset",
                "Ghost.Missing",
                string.Empty,
                string.Empty);

            bool valid = GameplayTagValidationGate.ValidateProject(out GameplayTagAssetScanReport report, out string error);

            Assert.That(valid, Is.False);
            Assert.That(report.References.Any(reference => reference.AssetPath == path &&
                                                           reference.Status == GameplayTagReferenceStatus.Unregistered), Is.True);
            Assert.That(error, Does.Contain(path));
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => new GameplayTagBuildGate().OnPreprocessBuild(null));

            Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            Assert.That(GameplayTagValidationGate.ValidateProject(out report, out error), Is.True, error);
            Assert.That(report.HasBlockingIssues, Is.False);
        }

        private string CreateAsset(string fileName, string single, string unregistered, string containerTag)
        {
            string path = $"{TemporaryFolder}/{fileName}";
            return CreateAssetAt(path, single, unregistered, containerTag);
        }

        private static string CreateAssetAt(string path, string single, string unregistered, string containerTag)
        {
            GameplayTagAuthoringFixtureAsset asset = ScriptableObject.CreateInstance<GameplayTagAuthoringFixtureAsset>();
            AssetDatabase.CreateAsset(asset, path);
            WriteRawValues(asset, single, unregistered, containerTag);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static void WriteRawValues(UnityEngine.Object target, string single, string unregistered, string containerTag)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty("singleTag").FindPropertyRelative("tagName").stringValue = single;
            serializedObject.FindProperty("unregisteredTag").FindPropertyRelative("tagName").stringValue = unregistered;
            SerializedProperty tags = serializedObject.FindProperty("tags").FindPropertyRelative("serializedTags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).FindPropertyRelative("tagName").stringValue = containerTag;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            GameplayTagSerializedPropertyWriter.RefreshContainerCaches(new[] { target }, "tags");
            EditorUtility.SetDirty(target);
        }

        private static void EnsureTemporaryFolder()
        {
            if (!AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.CreateFolder("Assets/Tests", "TempGameplayTagRedirectSafety");
            }
        }

        private static void EnsureTemporaryProductionFolder()
        {
            if (!AssetDatabase.IsValidFolder(TemporaryProductionFolder))
            {
                AssetDatabase.CreateFolder("Assets", "TempGameplayTagRedirectSafetyTests");
            }
        }
    }
}
