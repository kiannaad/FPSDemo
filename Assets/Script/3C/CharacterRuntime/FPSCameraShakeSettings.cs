using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "FPSCameraShakeSettings", menuName = "CGame/Camera/FPS Camera Shake Settings")]
    public sealed class FPSCameraShakeSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] private float rotationAmplitude = 1f;
        [SerializeField, Min(0f)] private float decaySpeed = 12f;

        public float RotationAmplitude => rotationAmplitude;
        public float DecaySpeed => decaySpeed;
    }
}
