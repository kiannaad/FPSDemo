using System;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [Serializable]
    public sealed class WeaponBulletData
    {
        [SerializeField, Min(0.01f)] private float maxShootDistance = 1000f;
        [SerializeField] private LayerMask hitLayerMask = Physics.DefaultRaycastLayers;

        public float MaxShootDistance => maxShootDistance;

        public LayerMask HitLayerMask => hitLayerMask;

        public void Configure(float distance, LayerMask layerMask)
        {
            if (!IsFinitePositive(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Weapon maximum shoot distance must be finite and positive.");
            }

            maxShootDistance = distance;
            hitLayerMask = layerMask;
        }

        public void Validate()
        {
            if (!IsFinitePositive(maxShootDistance))
            {
                throw new InvalidOperationException("Weapon BulletData maximum shoot distance must be finite and positive.");
            }

        }

        private static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
