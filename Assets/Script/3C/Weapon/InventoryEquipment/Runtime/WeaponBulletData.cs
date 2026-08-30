using System;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [Serializable]
    public sealed class WeaponBulletData
    {
        [SerializeField, Min(0.01f)] private float maxShootDistance = 1000f;
        [SerializeField] private LayerMask hitLayerMask = Physics.DefaultRaycastLayers;
        [SerializeField] private AnimationCurve heatToHeatPerShot = AnimationCurve.Linear(0f, 0.12f, 1f, 0.12f);
        [SerializeField] private AnimationCurve heatToHeatCooldown = AnimationCurve.Linear(0f, 0.25f, 1f, 0.25f);
        [SerializeField] private AnimationCurve heatToSpreadAngle = AnimationCurve.Linear(0f, 0.15f, 1f, 3f);
        [SerializeField, Min(0.1f)] private float spreadExponent = 1f;

        public float MaxShootDistance => maxShootDistance;

        public LayerMask HitLayerMask => hitLayerMask;
        public float SpreadExponent => spreadExponent;

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

            ValidateCurve(heatToHeatPerShot, nameof(heatToHeatPerShot));
            ValidateCurve(heatToHeatCooldown, nameof(heatToHeatCooldown));
            ValidateCurve(heatToSpreadAngle, nameof(heatToSpreadAngle));
            if (spreadExponent < 0.1f || float.IsNaN(spreadExponent) || float.IsInfinity(spreadExponent))
            {
                throw new InvalidOperationException("Weapon BulletData spread exponent must be finite and at least 0.1.");
            }
        }

        public float EvaluateHeatPerShot(float heat) => EvaluateNonNegative(heatToHeatPerShot, heat, nameof(heatToHeatPerShot));
        public float EvaluateHeatCooldown(float heat) => EvaluateNonNegative(heatToHeatCooldown, heat, nameof(heatToHeatCooldown));
        public float EvaluateSpreadAngle(float heat) => EvaluateNonNegative(heatToSpreadAngle, heat, nameof(heatToSpreadAngle));

        private static void ValidateCurve(AnimationCurve curve, string name)
        {
            if (curve == null || curve.length == 0)
            {
                throw new InvalidOperationException($"Weapon BulletData {name} must be configured.");
            }
        }

        private static float EvaluateNonNegative(AnimationCurve curve, float heat, string name)
        {
            float value = curve.Evaluate(Mathf.Clamp01(heat));
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException($"Weapon BulletData {name} evaluated to an invalid value.");
            }
            return value;
        }

        private static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
