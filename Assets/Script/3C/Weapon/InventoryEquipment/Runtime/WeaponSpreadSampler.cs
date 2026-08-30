using UnityEngine;

namespace CGame.InventoryEquipment
{
    public static class WeaponSpreadSampler
    {
        public static Vector3 SampleDirection(WeaponSpreadContext context, float halfAngleDegrees, float exponent, ulong seed)
        {
            float clampedAngle = Mathf.Clamp(halfAngleDegrees, 0f, 89f);
            float clampedExponent = Mathf.Max(0.1f, exponent);
            if (clampedAngle <= 0f)
            {
                return context.Forward;
            }

            float radial = Mathf.Pow(Random01(seed), clampedExponent) * clampedAngle * Mathf.Deg2Rad;
            float azimuth = Random01(seed + 0x9E3779B97F4A7C15UL) * Mathf.PI * 2f;
            float sin = Mathf.Sin(radial);
            Vector3 direction = context.Forward * Mathf.Cos(radial)
                + (context.Right * Mathf.Cos(azimuth) + context.Up * Mathf.Sin(azimuth)) * sin;
            return direction.normalized;
        }

        private static float Random01(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (value >> 40) * (1f / 16777216f);
        }
    }
}
