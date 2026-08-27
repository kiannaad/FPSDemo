using UnityEngine;

namespace CGame.Ability.Cues
{
    public readonly struct GameplayCueParameters
    {
        public GameplayCueParameters(
            GameplayEffectContext effectContext,
            object target = null,
            Vector3 location = default,
            bool hasLocation = false,
            Vector3 normal = default,
            bool hasNormal = false,
            PhysicMaterial physicMaterial = null)
        {
            EffectContext = effectContext;
            Target = target;
            Location = location;
            HasLocation = hasLocation;
            Normal = normal;
            HasNormal = hasNormal;
            PhysicMaterial = physicMaterial;
        }

        public GameplayEffectContext EffectContext { get; }
        public object Target { get; }
        public Vector3 Location { get; }
        public bool HasLocation { get; }
        public Vector3 Normal { get; }
        public bool HasNormal { get; }
        public PhysicMaterial PhysicMaterial { get; }

        public GameplayCueParameters WithTarget(object target)
        {
            return new GameplayCueParameters(EffectContext, target, Location, HasLocation, Normal, HasNormal, PhysicMaterial);
        }
    }
}
