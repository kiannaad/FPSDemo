using System;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    public readonly struct WeaponSpreadContext
    {
        public WeaponSpreadContext(Vector3 forward, Vector3 right, Vector3 up, bool isAiming, bool isGrounded, float horizontalSpeed)
        {
            if (forward.sqrMagnitude <= Mathf.Epsilon || right.sqrMagnitude <= Mathf.Epsilon || up.sqrMagnitude <= Mathf.Epsilon)
            {
                throw new ArgumentException("Weapon spread context requires an orthogonal camera basis.");
            }

            Forward = forward.normalized;
            Right = right.normalized;
            Up = up.normalized;
            IsAiming = isAiming;
            IsGrounded = isGrounded;
            HorizontalSpeed = Mathf.Max(0f, horizontalSpeed);
        }

        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public Vector3 Up { get; }
        public bool IsAiming { get; }
        public bool IsGrounded { get; }
        public float HorizontalSpeed { get; }
    }
}
