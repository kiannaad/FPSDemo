using System;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public sealed class WeaponAnimationCatalogEntry
    {
        [SerializeField] private string weaponId;
        [SerializeField] private string yooAssetLocation;

        public WeaponId WeaponId => new WeaponId(weaponId);
        public string YooAssetLocation => yooAssetLocation;
        public bool IsValid =>
            WeaponId.IsValid && !string.IsNullOrWhiteSpace(yooAssetLocation);
    }
}
