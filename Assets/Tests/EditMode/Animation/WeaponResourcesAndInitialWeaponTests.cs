using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class WeaponResourcesAndInitialWeaponTests
    {
        private const string DefinitionPath =
            "Assets/Resources/FistsWeaponAnimationDefinition.asset";
        private const string CatalogPath =
            "Assets/Resources/WeaponAnimationCatalog.asset";
        private const string RifleDefinitionPath =
            "Assets/Art/Animation/Weapon/KINEMATION/AK/"
            + "RifleAKAnimationDefinition.asset";

        [Test]
        public void ProjectAssets_UseStrictCatalogAndDefinitionContracts()
        {
            WeaponAnimationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationCatalog>(
                    CatalogPath);
            WeaponAnimationDefinition definition =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    DefinitionPath);
            WeaponAnimationDefinition rifleDefinition =
                AssetDatabase.LoadAssetAtPath<
                    WeaponAnimationDefinition>(
                    RifleDefinitionPath);

            Assert.NotNull(catalog);
            Assert.NotNull(definition);
            Assert.NotNull(rifleDefinition);
            Assert.AreEqual(WeaponAnimationCatalogError.None, catalog.Validate());
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                definition.Validate(new WeaponId("knife")));
            Assert.IsTrue(catalog.TryResolve(
                new WeaponId("knife"),
                out string location));
            Assert.AreEqual("FistsWeaponAnimationDefinition", location);
            Assert.IsTrue(catalog.TryResolve(
                new WeaponId("rifle"),
                out string rifleLocation));
            Assert.AreEqual(
                "RifleAKAnimationDefinition",
                rifleLocation);
            Assert.AreEqual(2, catalog.Entries.Count);
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                rifleDefinition.Validate(
                    new WeaponId("rifle")));
            Assert.AreEqual(
                new WeaponRuntimeCapabilities(
                    true,
                    true,
                    false),
                rifleDefinition.Capabilities);

            string[] definitionFields = typeof(WeaponAnimationDefinition)
                .GetFields(
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "equip",
                    "fire",
                    "meleeAttack",
                    "overlayPose",
                    "reload",
                    "supportsFire",
                    "supportsMeleeAttack",
                    "supportsReload",
                    "unequip",
                    "weaponId",
                    "weaponPrefab",
                },
                definitionFields);

            Assert.IsFalse(definition.SupportsFire);
            Assert.IsFalse(definition.SupportsReload);
            Assert.IsTrue(definition.SupportsMeleeAttack);
            Assert.IsNull(definition.Fire);
            Assert.IsNull(definition.Reload);
            Assert.NotNull(definition.MeleeAttack);
            Assert.AreEqual(
                new WeaponRuntimeCapabilities(false, false, true),
                definition.Capabilities);
            Assert.IsFalse(typeof(WeaponRuntimeCapabilities)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field =>
                    typeof(UnityEngine.Object).IsAssignableFrom(
                        field.FieldType)));
        }

        [Test]
        public void DefinitionValidation_RequiresCommonFieldsAndExactCapabilities()
        {
            WeaponAnimationDefinition source =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    DefinitionPath);
            var definition =
                ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            try
            {
                EditorUtility.CopySerialized(source, definition);

                SetField(definition, "weaponPrefab", null);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError.MissingWeaponPrefab,
                    definition.Validate());
                EditorUtility.CopySerialized(source, definition);

                SetField(definition, "overlayPose", null);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError.MissingOverlayPose,
                    definition.Validate());
                EditorUtility.CopySerialized(source, definition);

                SetField(definition, "supportsFire", true);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError.InvalidPrimaryAction,
                    definition.Validate());
                EditorUtility.CopySerialized(source, definition);

                SetField(definition, "supportsMeleeAttack", false);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError.InvalidPrimaryAction,
                    definition.Validate());
                EditorUtility.CopySerialized(source, definition);

                SetField(definition, "supportsReload", false);
                SetField(definition, "reload", source.Equip);
                Assert.AreEqual(
                    WeaponAnimationDefinitionError.InvalidReload,
                    definition.Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CatalogValidation_RejectsInvalidAndDuplicateEntries()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponAnimationCatalog>();
            try
            {
                SetField(
                    catalog,
                    "entries",
                    new[]
                    {
                        CreateEntry("knife", "KnifeDefinition"),
                        CreateEntry("knife", "OtherKnifeDefinition"),
                    });
                Assert.AreEqual(
                    WeaponAnimationCatalogError.DuplicateWeaponId,
                    catalog.Validate());

                SetField(
                    catalog,
                    "entries",
                    new[] { CreateEntry("knife", " ") });
                Assert.AreEqual(
                    WeaponAnimationCatalogError.InvalidEntry,
                    catalog.Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Provider_LoadsCatalogOnceAndTransfersIndependentDefinitionLeases()
        {
            WeaponAnimationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationCatalog>(
                    CatalogPath);
            WeaponAnimationDefinition definition =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    DefinitionPath);
            var catalogLoad = new FakeCatalogLoadOperation(
                catalog,
                true,
                true);
            var firstDefinitionLoad = new FakeDefinitionLoadOperation(
                definition,
                true,
                true);
            var secondDefinitionLoad = new FakeDefinitionLoadOperation(
                definition,
                true,
                true);
            var loader = new FakeLoader(
                catalogLoad,
                firstDefinitionLoad,
                secondDefinitionLoad);
            var provider =
                new CatalogWeaponAnimationDefinitionProvider(loader);

            IWeaponAnimationDefinitionResolveOperation first =
                provider.BeginResolve(new WeaponId("knife"));
            IWeaponAnimationDefinitionResolveOperation second =
                provider.BeginResolve(new WeaponId("knife"));
            Complete(first);
            Complete(second);

            Assert.AreEqual(1, loader.CatalogLoadCount);
            Assert.AreEqual(2, loader.DefinitionLoadCount);
            Assert.AreEqual(1, provider.CatalogLoadStartCount);
            Assert.IsTrue(first.Result.IsSuccess);
            Assert.IsTrue(second.Result.IsSuccess);
            first.Result.Lease.Dispose();
            Assert.AreEqual(1, firstDefinitionLoad.ReleaseCount);
            Assert.AreEqual(0, secondDefinitionLoad.ReleaseCount);
            second.Result.Lease.Dispose();
            Assert.AreEqual(1, secondDefinitionLoad.ReleaseCount);

            provider.Dispose();
            provider.Dispose();
            Assert.AreEqual(1, catalogLoad.ReleaseCount);
        }

        [Test]
        public void Provider_CancellationAndIdMismatchReleaseLoadsExactlyOnce()
        {
            WeaponAnimationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationCatalog>(
                    CatalogPath);
            WeaponAnimationDefinition source =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                    DefinitionPath);
            var mismatched =
                ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            try
            {
                EditorUtility.CopySerialized(source, mismatched);
                SetField(mismatched, "weaponId", "other");
                var pendingLoad =
                    new FakeDefinitionLoadOperation(null, false, false);
                var mismatchedLoad =
                    new FakeDefinitionLoadOperation(mismatched, true, true);
                var loader = new FakeLoader(
                    new FakeCatalogLoadOperation(catalog, true, true),
                    pendingLoad,
                    mismatchedLoad);
                var provider =
                    new CatalogWeaponAnimationDefinitionProvider(loader);

                IWeaponAnimationDefinitionResolveOperation cancelled =
                    provider.BeginResolve(new WeaponId("knife"));
                Assert.IsFalse(cancelled.IsCompleted);
                cancelled.Dispose();
                cancelled.Dispose();
                Assert.AreEqual(1, pendingLoad.ReleaseCount);

                IWeaponAnimationDefinitionResolveOperation mismatch =
                    provider.BeginResolve(new WeaponId("knife"));
                Complete(mismatch);
                Assert.AreEqual(
                    WeaponAnimationDefinitionResolveError.DefinitionIdMismatch,
                    mismatch.Result.Error);
                Assert.AreEqual(1, mismatchedLoad.ReleaseCount);
                mismatch.Dispose();
                Assert.AreEqual(1, mismatchedLoad.ReleaseCount);
                provider.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mismatched);
            }
        }

        [Test]
        public void WeaponRuntime_InitializesExactlyOnceFromPureCapabilities()
        {
            var runtime = new WeaponRuntime();
            var capabilities =
                new WeaponRuntimeCapabilities(false, false, true);

            Assert.IsTrue(runtime.Initialize(
                new WeaponId("knife"),
                capabilities));
            Assert.IsTrue(runtime.IsInitialized);
            Assert.AreEqual(new WeaponId("knife"), runtime.Snapshot.EquippedWeaponId);
            Assert.AreEqual(1u, runtime.Snapshot.Generation);
            Assert.AreEqual(capabilities, runtime.Capabilities);
            Assert.IsFalse(runtime.Initialize(
                new WeaponId("other"),
                new WeaponRuntimeCapabilities(true, true, false)));
            Assert.IsFalse(runtime.RequestFire(out _));
            Assert.IsFalse(runtime.RequestReload(out _));
            Assert.IsTrue(runtime.RequestPrimaryAction(out WeaponActionFact melee));
            Assert.AreEqual(WeaponActionKind.MeleeAttack, melee.Kind);
        }

        [Test]
        public void ProjectDefinitionDependencies_AreProjectOwned()
        {
            string[] forbidden = AssetDatabase
                .GetDependencies(DefinitionPath, true)
                .Where(path =>
                    path.StartsWith(
                        "Assets/KINEMATION/",
                        StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(
                        "Assets/ThirdParty/",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(forbidden, Is.Empty);
            Assert.AreEqual(
                "Assets/Art/Weapon/KINEMATION/Knife/KnifeWeapon.fbx",
                AssetDatabase.GetAssetPath(
                    AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(
                        DefinitionPath).WeaponPrefab));
        }

        private static void Complete(
            IWeaponAnimationDefinitionResolveOperation operation)
        {
            for (int i = 0; i < 3 && !operation.IsCompleted; i++)
            {
            }

            Assert.IsTrue(operation.IsCompleted);
        }

        private static WeaponAnimationCatalogEntry CreateEntry(
            string weaponId,
            string location)
        {
            var entry = new WeaponAnimationCatalogEntry();
            SetField(entry, "weaponId", weaponId);
            SetField(entry, "yooAssetLocation", location);
            return entry;
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, name);
            field.SetValue(target, value);
        }

        private sealed class FakeLoader : IWeaponAnimationAssetLoader
        {
            private readonly IWeaponAnimationCatalogLoadOperation catalog;
            private readonly Queue<IWeaponAnimationDefinitionLoadOperation>
                definitions;

            public FakeLoader(
                IWeaponAnimationCatalogLoadOperation catalog,
                params IWeaponAnimationDefinitionLoadOperation[] definitions)
            {
                this.catalog = catalog;
                this.definitions =
                    new Queue<IWeaponAnimationDefinitionLoadOperation>(
                        definitions);
            }

            public int CatalogLoadCount { get; private set; }
            public int DefinitionLoadCount { get; private set; }

            public IWeaponAnimationCatalogLoadOperation BeginLoadCatalog(
                string location)
            {
                CatalogLoadCount++;
                Assert.AreEqual("WeaponAnimationCatalog", location);
                return catalog;
            }

            public IWeaponAnimationDefinitionLoadOperation
                BeginLoadDefinition(string location)
            {
                DefinitionLoadCount++;
                Assert.AreEqual("FistsWeaponAnimationDefinition", location);
                return definitions.Dequeue();
            }
        }

        private sealed class FakeCatalogLoadOperation :
            IWeaponAnimationCatalogLoadOperation
        {
            public FakeCatalogLoadOperation(
                WeaponAnimationCatalog asset,
                bool isCompleted,
                bool isSuccessful)
            {
                Asset = asset;
                IsCompleted = isCompleted;
                IsSuccessful = isSuccessful;
            }

            public bool IsCompleted { get; }
            public bool IsSuccessful { get; }
            public WeaponAnimationCatalog Asset { get; }
            public string Error => IsSuccessful ? string.Empty : "catalog failed";
            public int ReleaseCount { get; private set; }
            public void Dispose()
            {
                if (ReleaseCount == 0)
                {
                    ReleaseCount++;
                }
            }
        }

        private sealed class FakeDefinitionLoadOperation :
            IWeaponAnimationDefinitionLoadOperation
        {
            public FakeDefinitionLoadOperation(
                WeaponAnimationDefinition asset,
                bool isCompleted,
                bool isSuccessful)
            {
                Asset = asset;
                IsCompleted = isCompleted;
                IsSuccessful = isSuccessful;
            }

            public bool IsCompleted { get; }
            public bool IsSuccessful { get; }
            public WeaponAnimationDefinition Asset { get; }
            public string Error =>
                IsSuccessful ? string.Empty : "definition failed";
            public int ReleaseCount { get; private set; }
            public void Dispose()
            {
                if (ReleaseCount == 0)
                {
                    ReleaseCount++;
                }
            }
        }
    }
}
