using UnityEngine;

namespace CGame.Animation
{
    public sealed class AnimationLocationData
    {
        private bool hasHistory;

        public Vector3 WorldLocation { get; private set; }
        public Vector3 PreviousWorldLocation { get; private set; }
        public Vector3 DisplacementDelta { get; private set; }
        public float DisplacementSpeed { get; private set; }

        internal void Update(Vector3 worldLocation, float deltaTime)
        {
            if (!hasHistory)
            {
                Reset(worldLocation);
                return;
            }

            PreviousWorldLocation = WorldLocation;
            WorldLocation = worldLocation;
            DisplacementDelta = WorldLocation - PreviousWorldLocation;
            DisplacementSpeed = deltaTime > 0f
                ? Vector3.ProjectOnPlane(DisplacementDelta, Vector3.up).magnitude / deltaTime
                : 0f;
        }

        internal void Reset(Vector3 worldLocation)
        {
            WorldLocation = worldLocation;
            PreviousWorldLocation = worldLocation;
            DisplacementDelta = Vector3.zero;
            DisplacementSpeed = 0f;
            hasHistory = true;
        }
    }
}
