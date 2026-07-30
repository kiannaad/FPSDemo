using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "FirstPersonCameraProfile", menuName = "CGame/Camera/First Person Camera Profile")]
    public sealed class FirstPersonCameraProfile : ScriptableObject
    {
        [SerializeField]
        [Range(-89f, 0f)]
        private float minPitch = -89f;

        [SerializeField]
        [Range(0f, 89f)]
        private float maxPitch = 89f;

        [SerializeField]
        [Min(1f)]
        private float baseFieldOfView = 90f;

        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float BaseFieldOfView => baseFieldOfView;
    }
}
