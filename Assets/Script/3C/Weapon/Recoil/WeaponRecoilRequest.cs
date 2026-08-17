using UnityEngine;

namespace CGame
{
    public readonly struct WeaponRecoilRequest
    {
        public WeaponRecoilRequest(
            Vector2 gameplayKick,
            float gameplayRecoveryDegreesPerSecond,
            Vector3 cameraLocalPosition,
            Vector3 cameraLocalEulerAngles,
            Vector3 viewModelLocalPosition,
            Vector3 viewModelLocalEulerAngles,
            float visualPositionRecoverySpeed,
            float visualRotationRecoverySpeed)
        {
            GameplayKick = gameplayKick;
            GameplayRecoveryDegreesPerSecond = Mathf.Max(0f, gameplayRecoveryDegreesPerSecond);
            CameraLocalPosition = cameraLocalPosition;
            CameraLocalEulerAngles = cameraLocalEulerAngles;
            ViewModelLocalPosition = viewModelLocalPosition;
            ViewModelLocalEulerAngles = viewModelLocalEulerAngles;
            VisualPositionRecoverySpeed = Mathf.Max(0f, visualPositionRecoverySpeed);
            VisualRotationRecoverySpeed = Mathf.Max(0f, visualRotationRecoverySpeed);
        }

        public Vector2 GameplayKick { get; }
        public float GameplayRecoveryDegreesPerSecond { get; }
        public Vector3 CameraLocalPosition { get; }
        public Vector3 CameraLocalEulerAngles { get; }
        public Vector3 ViewModelLocalPosition { get; }
        public Vector3 ViewModelLocalEulerAngles { get; }
        public float VisualPositionRecoverySpeed { get; }
        public float VisualRotationRecoverySpeed { get; }
    }
}
