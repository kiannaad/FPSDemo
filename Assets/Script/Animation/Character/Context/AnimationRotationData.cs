using UnityEngine;

namespace CGame.Animation
{
    public sealed class AnimationRotationData
    {
        private bool hasHistory;

        public Quaternion WorldRotation { get; private set; } = Quaternion.identity;
        public Quaternion PreviousWorldRotation { get; private set; } = Quaternion.identity;
        public float YawDelta { get; private set; }
        public float YawDeltaSpeed { get; private set; }

        internal void Update(Quaternion worldRotation, float deltaTime)
        {
            if (!hasHistory)
            {
                Reset(worldRotation);
                return;
            }

            PreviousWorldRotation = WorldRotation;
            WorldRotation = worldRotation;
            YawDelta = Mathf.DeltaAngle(
                PreviousWorldRotation.eulerAngles.y,
                WorldRotation.eulerAngles.y);
            YawDeltaSpeed = deltaTime > 0f ? YawDelta / deltaTime : 0f;
        }

        internal void Reset(Quaternion worldRotation)
        {
            WorldRotation = worldRotation;
            PreviousWorldRotation = worldRotation;
            YawDelta = 0f;
            YawDeltaSpeed = 0f;
            hasHistory = true;
        }
    }
}
