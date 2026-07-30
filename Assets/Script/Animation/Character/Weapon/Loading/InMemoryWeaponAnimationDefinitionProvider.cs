using System;
using System.Collections.Generic;

namespace CGame.Animation
{
    public sealed class InMemoryWeaponAnimationDefinitionProvider :
        IWeaponAnimationDefinitionProvider
    {
        private readonly Dictionary<WeaponId, WeaponAnimationDefinition>
            definitions;
        private readonly Action<WeaponId> release;
        private bool isDisposed;

        public InMemoryWeaponAnimationDefinitionProvider(
            IEnumerable<WeaponAnimationDefinition> definitions,
            Action<WeaponId> release = null)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            this.definitions =
                new Dictionary<WeaponId, WeaponAnimationDefinition>();
            this.release = release;
            foreach (WeaponAnimationDefinition definition in definitions)
            {
                if (definition == null || !definition.WeaponId.IsValid)
                {
                    continue;
                }

                if (!this.definitions.TryAdd(
                        definition.WeaponId,
                        definition))
                {
                    throw new ArgumentException(
                        $"Duplicate weapon definition ID: {definition.WeaponId}.",
                        nameof(definitions));
                }
            }
        }

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

            if (!definitions.TryGetValue(
                    weaponId,
                    out WeaponAnimationDefinition definition))
            {
                return Completed(
                    WeaponAnimationDefinitionResolveError.DefinitionNotFound);
            }

            WeaponAnimationDefinitionError validationError =
                definition.Validate(weaponId);
            if (validationError != WeaponAnimationDefinitionError.None)
            {
                return Completed(
                    validationError
                        == WeaponAnimationDefinitionError.WeaponIdMismatch
                            ? WeaponAnimationDefinitionResolveError.DefinitionIdMismatch
                            : WeaponAnimationDefinitionResolveError.InvalidDefinition);
            }

            return WeaponAnimationDefinitionResolveOperation.Completed(
                new WeaponAnimationDefinitionResolveResult(
                    new ResolvedWeaponAnimationDefinitionLease(
                        definition,
                        () => release?.Invoke(weaponId)),
                    WeaponAnimationDefinitionResolveError.None));
        }

        public void Dispose()
        {
            isDisposed = true;
        }

        private static IWeaponAnimationDefinitionResolveOperation Completed(
            WeaponAnimationDefinitionResolveError error)
        {
            return WeaponAnimationDefinitionResolveOperation.Completed(
                new WeaponAnimationDefinitionResolveResult(
                    (ResolvedWeaponAnimationDefinitionLease)null,
                    error));
        }
    }
}
