using System;

namespace CGame.Animation
{
    public sealed class CatalogWeaponAnimationDefinitionProvider :
        IWeaponAnimationDefinitionProvider
    {
        public const string DefaultCatalogLocation =
            "WeaponAnimationCatalog";

        private readonly IWeaponAnimationAssetLoader assetLoader;
        private readonly string catalogLocation;
        private IWeaponAnimationCatalogLoadOperation catalogLoadOperation;
        private WeaponAnimationCatalog catalog;
        private WeaponAnimationDefinitionResolveError catalogError;
        private bool isCatalogResolved;
        private bool isDisposed;

        public CatalogWeaponAnimationDefinitionProvider(
            IWeaponAnimationAssetLoader assetLoader,
            string catalogLocation = DefaultCatalogLocation)
        {
            this.assetLoader =
                assetLoader
                ?? throw new ArgumentNullException(nameof(assetLoader));
            if (string.IsNullOrWhiteSpace(catalogLocation))
            {
                throw new ArgumentException(
                    "A catalog location is required.",
                    nameof(catalogLocation));
            }

            this.catalogLocation = catalogLocation;
        }

        public int CatalogLoadStartCount { get; private set; }

        public IWeaponAnimationDefinitionResolveOperation BeginResolve(
            WeaponId weaponId)
        {
            if (isDisposed)
            {
                return Completed(
                    WeaponAnimationDefinitionResolveError.ProviderDisposed);
            }

            if (!weaponId.IsValid)
            {
                return Completed(
                    WeaponAnimationDefinitionResolveError.InvalidWeaponId);
            }

            EnsureCatalogLoadStarted();
            return new ResolveOperation(this, weaponId);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            catalogLoadOperation?.Dispose();
            catalogLoadOperation = null;
            catalog = null;
        }

        private static IWeaponAnimationDefinitionResolveOperation Completed(
            WeaponAnimationDefinitionResolveError error)
        {
            return WeaponAnimationDefinitionResolveOperation.Completed(
                new WeaponAnimationDefinitionResolveResult(
                    (ResolvedWeaponAnimationDefinitionLease)null,
                    error));
        }

        private void EnsureCatalogLoadStarted()
        {
            if (catalogLoadOperation != null || isCatalogResolved)
            {
                return;
            }

            CatalogLoadStartCount++;
            try
            {
                catalogLoadOperation =
                    assetLoader.BeginLoadCatalog(catalogLocation);
                if (catalogLoadOperation == null)
                {
                    ResolveCatalogFailure(
                        WeaponAnimationDefinitionResolveError.CatalogLoadFailed);
                }
            }
            catch
            {
                ResolveCatalogFailure(
                    WeaponAnimationDefinitionResolveError.CatalogLoadFailed);
            }
        }

        private bool TryGetCatalog(
            out WeaponAnimationCatalog resolvedCatalog,
            out WeaponAnimationDefinitionResolveError error)
        {
            ResolveCatalogIfReady();
            resolvedCatalog = catalog;
            error = catalogError;
            return isCatalogResolved;
        }

        private void ResolveCatalogIfReady()
        {
            if (isCatalogResolved
                || catalogLoadOperation == null
                || !catalogLoadOperation.IsCompleted)
            {
                return;
            }

            if (!catalogLoadOperation.IsSuccessful
                || catalogLoadOperation.Asset == null)
            {
                ResolveCatalogFailure(
                    WeaponAnimationDefinitionResolveError.CatalogLoadFailed);
                return;
            }

            WeaponAnimationCatalog loadedCatalog =
                catalogLoadOperation.Asset;
            if (!loadedCatalog.IsValid)
            {
                ResolveCatalogFailure(
                    WeaponAnimationDefinitionResolveError.CatalogInvalid);
                return;
            }

            catalog = loadedCatalog;
            catalogError = WeaponAnimationDefinitionResolveError.None;
            isCatalogResolved = true;
        }

        private void ResolveCatalogFailure(
            WeaponAnimationDefinitionResolveError error)
        {
            catalogLoadOperation?.Dispose();
            catalogLoadOperation = null;
            catalog = null;
            catalogError = error;
            isCatalogResolved = true;
        }

        private sealed class ResolveOperation :
            IWeaponAnimationDefinitionResolveOperation
        {
            private readonly CatalogWeaponAnimationDefinitionProvider provider;
            private readonly WeaponId expectedId;
            private IWeaponAnimationDefinitionLoadOperation loadOperation;
            private WeaponAnimationDefinitionResolveResult result;
            private bool isCompleted;
            private bool isDisposed;

            public ResolveOperation(
                CatalogWeaponAnimationDefinitionProvider provider,
                WeaponId expectedId)
            {
                this.provider = provider;
                this.expectedId = expectedId;
            }

            public bool IsCompleted
            {
                get
                {
                    TryComplete();
                    return isCompleted;
                }
            }

            public WeaponAnimationDefinitionResolveResult Result
            {
                get
                {
                    TryComplete();
                    if (!isCompleted)
                    {
                        throw new InvalidOperationException(
                            "Weapon definition resolve has not completed.");
                    }

                    return result;
                }
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                if (isCompleted)
                {
                    result.Lease?.Dispose();
                }
                else
                {
                    loadOperation?.Dispose();
                    loadOperation = null;
                }
            }

            private void TryComplete()
            {
                if (isCompleted || isDisposed)
                {
                    return;
                }

                if (provider.isDisposed)
                {
                    Complete(
                        WeaponAnimationDefinitionResolveError.ProviderDisposed);
                    return;
                }

                if (loadOperation == null)
                {
                    if (!provider.TryGetCatalog(
                            out WeaponAnimationCatalog catalog,
                            out WeaponAnimationDefinitionResolveError error))
                    {
                        return;
                    }

                    if (error != WeaponAnimationDefinitionResolveError.None)
                    {
                        Complete(error);
                        return;
                    }

                    if (!catalog.TryResolve(
                            expectedId,
                            out string definitionLocation))
                    {
                        Complete(
                            WeaponAnimationDefinitionResolveError.DefinitionNotFound);
                        return;
                    }

                    try
                    {
                        loadOperation =
                            provider.assetLoader.BeginLoadDefinition(
                                definitionLocation);
                        if (loadOperation == null)
                        {
                            Complete(
                                WeaponAnimationDefinitionResolveError.DefinitionLoadFailed);
                        }
                    }
                    catch
                    {
                        Complete(
                            WeaponAnimationDefinitionResolveError.DefinitionLoadFailed);
                    }

                    return;
                }

                if (!loadOperation.IsCompleted)
                {
                    return;
                }

                if (!loadOperation.IsSuccessful
                    || loadOperation.Asset == null)
                {
                    loadOperation.Dispose();
                    loadOperation = null;
                    Complete(
                        WeaponAnimationDefinitionResolveError.DefinitionLoadFailed);
                    return;
                }

                WeaponAnimationDefinition definition = loadOperation.Asset;
                WeaponAnimationDefinitionError validationError =
                    definition.Validate(expectedId);
                if (validationError != WeaponAnimationDefinitionError.None)
                {
                    loadOperation.Dispose();
                    loadOperation = null;
                    Complete(
                        validationError
                            == WeaponAnimationDefinitionError.WeaponIdMismatch
                                ? WeaponAnimationDefinitionResolveError.DefinitionIdMismatch
                                : WeaponAnimationDefinitionResolveError.InvalidDefinition);
                    return;
                }

                IWeaponAnimationDefinitionLoadOperation ownedLoadOperation =
                    loadOperation;
                loadOperation = null;
                result = new WeaponAnimationDefinitionResolveResult(
                    new ResolvedWeaponAnimationDefinitionLease(
                        definition,
                        ownedLoadOperation.Dispose),
                    WeaponAnimationDefinitionResolveError.None);
                isCompleted = true;
            }

            private void Complete(
                WeaponAnimationDefinitionResolveError error)
            {
                result = new WeaponAnimationDefinitionResolveResult(
                    (ResolvedWeaponAnimationDefinitionLease)null,
                    error);
                isCompleted = true;
            }
        }
    }
}
