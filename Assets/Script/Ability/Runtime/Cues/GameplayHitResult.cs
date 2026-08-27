using UnityEngine;

namespace CGame.Ability.Cues
{
    public readonly struct GameplayHitResult
    {
        public GameplayHitResult(Vector3 location, Vector3 normal, Collider collider = null)
        {
            Location = location;
            Normal = normal;
            Collider = collider;
            PhysicMaterial = collider == null ? null : collider.sharedMaterial;
        }

        public Vector3 Location { get; }
        public Vector3 Normal { get; }
        public Collider Collider { get; }
        public PhysicMaterial PhysicMaterial { get; }
    }
}
