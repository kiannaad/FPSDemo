using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.GameplayTags;

namespace CGame.InventoryEquipment
{
    public sealed class WeaponCatalogSubSystem : WorldSubSystem
    {
        private readonly WeaponDefinition[] definitions;
        private Dictionary<GameplayTag, WeaponDefinition> definitionsByTag;

        public WeaponCatalogSubSystem(IEnumerable<WeaponDefinition> definitions)
        {
            this.definitions = definitions == null
                ? Array.Empty<WeaponDefinition>()
                : new List<WeaponDefinition>(definitions).ToArray();
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            definitionsByTag = new Dictionary<GameplayTag, WeaponDefinition>();
            foreach (WeaponDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new InvalidOperationException("Weapon Catalog contains a null definition.");
                }

                definition.Validate();
                if (!definitionsByTag.TryAdd(definition.WeaponTag, definition))
                {
                    throw new InvalidOperationException($"Weapon Catalog contains duplicate tag {definition.WeaponTag}.");
                }
            }

            return Task.CompletedTask;
        }

        public WeaponDefinition ResolveExact(GameplayTag weaponTag)
        {
            if (weaponTag.IsEmpty || definitionsByTag == null || !definitionsByTag.TryGetValue(weaponTag, out WeaponDefinition definition))
            {
                throw new KeyNotFoundException($"Weapon Catalog has no exact entry for {weaponTag}.");
            }

            return definition;
        }

        public bool ContainsExact(WeaponDefinition definition)
        {
            return definition != null
                && definitionsByTag != null
                && definitionsByTag.TryGetValue(definition.WeaponTag, out WeaponDefinition registered)
                && ReferenceEquals(registered, definition);
        }
    }
}
