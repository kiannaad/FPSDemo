using UnityEngine;

namespace CGame.Ability.Cues
{
    public readonly struct GameplayHitResult
    {
        public GameplayHitResult(
            Vector3 location,
            Vector3 normal,
            Collider collider = null,
            Vector3 traceStart = default,
            Vector3 traceEnd = default)
        {
            Location = location;
            Normal = normal;
            Collider = collider;
            PhysicMaterial = collider == null ? null : collider.sharedMaterial;
            TraceStart = traceStart;
            TraceEnd = traceEnd;
        }

        public Vector3 Location { get; }
        public Vector3 Normal { get; }
        public Collider Collider { get; }
        public PhysicMaterial PhysicMaterial { get; }
        public Vector3 TraceStart { get; }
        public Vector3 TraceEnd { get; }
    }
}
