using System;
using System.Reflection;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class YooAssetCharacterDefinitionProviderTests
    {
        [Test]
        public void BeginResolve_MapsIdInternallyAndTransfersLoadOwnershipToLease()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            var loadOperation = new FakeLoadOperation(definition, true, true);
            var loader = new FakeLoader(() => loadOperation);
            var provider = new YooAssetCharacterDefinitionProvider(loader);

            try
            {
                ICharacterDefinitionResolveOperation operation = provider.BeginResolve(new CharacterDefinitionId("local-player"));

                Assert.IsTrue(operation.IsCompleted);
                Assert.AreEqual("CharacterDefinition", loader.LastLocation);
                Assert.IsTrue(operation.Result.IsSuccess);
                Assert.AreSame(definition, operation.Result.Definition);
                Assert.AreEqual(0, loadOperation.ReleaseCount);

                operation.Result.Lease.Dispose();
                operation.Result.Lease.Dispose();
                Assert.AreEqual(1, loadOperation.ReleaseCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void BeginResolve_DoesNotExposeLocationsForUnknownIds()
        {
            var loader = new FakeLoader(() => throw new InvalidOperationException());
            var provider = new YooAssetCharacterDefinitionProvider(loader);

            ICharacterDefinitionResolveOperation operation = provider.BeginResolve(new CharacterDefinitionId("unknown"));

            Assert.IsTrue(operation.IsCompleted);
            Assert.AreEqual(CharacterDefinitionResolveError.DefinitionNotFound, operation.Result.Error);
            Assert.AreEqual(0, loader.BeginLoadCount);
        }

        [Test]
        public void BeginResolve_ReleasesFailedAndMismatchedLoadsExactlyOnce()
        {
            CharacterDefinition mismatchedDefinition = CreateValidDefinition("other-player");
            var failedLoad = new FakeLoadOperation(null, true, false);
            var mismatchedLoad = new FakeLoadOperation(mismatchedDefinition, true, true);
            int invocation = 0;
            var loader = new FakeLoader(() => invocation++ == 0 ? failedLoad : mismatchedLoad);
            var provider = new YooAssetCharacterDefinitionProvider(loader);

            try
            {
                ICharacterDefinitionResolveOperation failed = provider.BeginResolve(new CharacterDefinitionId("local-player"));
                ICharacterDefinitionResolveOperation mismatched = provider.BeginResolve(new CharacterDefinitionId("local-player"));

                Assert.IsTrue(failed.IsCompleted);
                Assert.AreEqual(CharacterDefinitionResolveError.AssetLoadFailed, failed.Result.Error);
                Assert.AreEqual(1, failedLoad.ReleaseCount);
                Assert.IsTrue(mismatched.IsCompleted);
                Assert.AreEqual(CharacterDefinitionResolveError.DefinitionIdMismatch, mismatched.Result.Error);
                Assert.AreEqual(1, mismatchedLoad.ReleaseCount);

                failed.Dispose();
                mismatched.Dispose();
                Assert.AreEqual(1, failedLoad.ReleaseCount);
                Assert.AreEqual(1, mismatchedLoad.ReleaseCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mismatchedDefinition);
            }
        }

        [Test]
        public void Dispose_ReleasesPendingLoadInsteadOfAbandoningIt()
        {
            var loadOperation = new FakeLoadOperation(null, false, false);
            var provider = new YooAssetCharacterDefinitionProvider(new FakeLoader(() => loadOperation));
            ICharacterDefinitionResolveOperation operation = provider.BeginResolve(new CharacterDefinitionId("local-player"));

            Assert.IsFalse(operation.IsCompleted);
            operation.Dispose();
            operation.Dispose();

            Assert.AreEqual(1, loadOperation.ReleaseCount);
        }

        [Test]
        public void ConcurrentResolves_OwnIndependentLoadsAndLeases()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            var firstLoad = new FakeLoadOperation(definition, true, true);
            var secondLoad = new FakeLoadOperation(definition, true, true);
            int invocation = 0;
            var loader = new FakeLoader(() => invocation++ == 0 ? firstLoad : secondLoad);
            var provider = new YooAssetCharacterDefinitionProvider(loader);

            try
            {
                ICharacterDefinitionResolveOperation first = provider.BeginResolve(new CharacterDefinitionId("local-player"));
                ICharacterDefinitionResolveOperation second = provider.BeginResolve(new CharacterDefinitionId("local-player"));

                Assert.IsTrue(first.IsCompleted);
                Assert.IsTrue(second.IsCompleted);
                Assert.AreEqual(2, loader.BeginLoadCount);
                first.Result.Lease.Dispose();
                Assert.AreEqual(1, firstLoad.ReleaseCount);
                Assert.AreEqual(0, secondLoad.ReleaseCount);
                second.Result.Lease.Dispose();
                Assert.AreEqual(1, secondLoad.ReleaseCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ResolveOperation_DisposesLeaseThatCompletesAfterShutdown()
        {
            CharacterDefinition definition = CreateValidDefinition("local-player");
            var operation = new CharacterDefinitionResolveOperation();
            int releaseCount = 0;

            try
            {
                operation.Dispose();
                operation.Complete(new CharacterDefinitionResolveResult(
                    new ResolvedCharacterDefinitionLease(definition, () => releaseCount++),
                    CharacterDefinitionResolveError.None));

                Assert.AreEqual(1, releaseCount);
                Assert.IsFalse(operation.IsCompleted);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static CharacterDefinition CreateValidDefinition(string definitionId)
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            SetField(definition, "definitionId", definitionId);
            SetField(definition, "visualPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Animation/FemaleLocomotionSet/Prefabs/Robot Kyle.prefab"));
            SetField(definition, "animationConfig", Resources.Load<CharacterAnimationConfig>("CharacterAnimationConfig"));
            SetField(definition, "supportedControlKinds", new[] { CharacterControlKind.LocalPlayer });
            SetField(definition, "initialWeaponId", "knife");
            Assert.IsTrue(definition.IsValid);
            return definition;
        }

        private static void SetField(CharacterDefinition definition, string name, object value)
        {
            FieldInfo field = typeof(CharacterDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(definition, value);
        }

        private sealed class FakeLoader : ICharacterDefinitionAssetLoader
        {
            private readonly Func<ICharacterDefinitionAssetLoadOperation> createOperation;

            public FakeLoader(Func<ICharacterDefinitionAssetLoadOperation> createOperation)
            {
                this.createOperation = createOperation;
            }

            public int BeginLoadCount { get; private set; }
            public string LastLocation { get; private set; }

            public ICharacterDefinitionAssetLoadOperation BeginLoad(string location)
            {
                BeginLoadCount++;
                LastLocation = location;
                return createOperation();
            }
        }

        private sealed class FakeLoadOperation : ICharacterDefinitionAssetLoadOperation
        {
            public FakeLoadOperation(CharacterDefinition asset, bool isCompleted, bool isSuccessful)
            {
                Asset = asset;
                IsCompleted = isCompleted;
                IsSuccessful = isSuccessful;
            }

            public bool IsCompleted { get; }
            public bool IsSuccessful { get; }
            public CharacterDefinition Asset { get; }
            public string Error => IsSuccessful ? string.Empty : "load failed";
            public int ReleaseCount { get; private set; }

            public void Dispose()
            {
                if (ReleaseCount == 0)
                {
                    ReleaseCount = 1;
                }
            }
        }
    }
}
