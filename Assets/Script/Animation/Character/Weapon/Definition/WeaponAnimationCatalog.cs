using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(
        menuName = "CGame/Animation/Weapon Animation Catalog",
        fileName = "WeaponAnimationCatalog")]
    public sealed class WeaponAnimationCatalog : ScriptableObject
    {
        [SerializeField] private WeaponAnimationCatalogEntry[] entries;

        public IReadOnlyList<WeaponAnimationCatalogEntry> Entries =>
            entries ?? System.Array.Empty<WeaponAnimationCatalogEntry>();
        public bool IsValid =>
            Validate() == WeaponAnimationCatalogError.None;

        public WeaponAnimationCatalogError Validate()
        {
            if (entries == null || entries.Length == 0)
            {
                return WeaponAnimationCatalogError.MissingEntries;
            }

            var ids = new HashSet<WeaponId>();
            for (int i = 0; i < entries.Length; i++)
            {
                WeaponAnimationCatalogEntry entry = entries[i];
                if (entry == null || !entry.IsValid)
                {
                    return WeaponAnimationCatalogError.InvalidEntry;
                }

                if (!ids.Add(entry.WeaponId))
                {
                    return WeaponAnimationCatalogError.DuplicateWeaponId;
                }
            }

            return WeaponAnimationCatalogError.None;
        }

        public bool TryResolve(
            WeaponId weaponId,
            out string yooAssetLocation)
        {
            if (!weaponId.IsValid || !IsValid)
            {
                yooAssetLocation = string.Empty;
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                WeaponAnimationCatalogEntry entry = entries[i];
                if (entry.WeaponId == weaponId)
                {
                    yooAssetLocation = entry.YooAssetLocation;
                    return true;
                }
            }

            yooAssetLocation = string.Empty;
            return false;
        }
    }
}
