using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "RecoilProfile", menuName = "CGame/Weapon/Recoil Profile")]
    public sealed class RecoilProfile : ScriptableObject
    {
        [SerializeField] private Vector2 kickDegrees = new Vector2(1.2f, 0.25f);
        [SerializeField] private Vector3 weaponTranslation = new Vector3(0f, 0f, -0.025f);
        [SerializeField] private Vector3 weaponEulerDegrees = new Vector3(-2f, 0f, 0f);
        [SerializeField, Min(0f)] private float recoveryDegreesPerSecond = 8f;
        [SerializeField, Min(0f)] private float maximumPitchDegrees = 8f;
        [SerializeField, Min(0f)] private float maximumYawDegrees = 3f;
        [SerializeField, Min(0f)] private float cameraShakeAmplitude = 0.1f;
        [SerializeField, Min(0.001f)] private float fireInterval = 0.1f;
        [SerializeField, Range(0f, 1f)] private float adsScalar = 0.65f;

        public Vector2 KickDegrees => kickDegrees;
        public Pose WeaponRecoilPose => new Pose(weaponTranslation, Quaternion.Euler(weaponEulerDegrees));
        public float RecoveryDegreesPerSecond => recoveryDegreesPerSecond;
        public float MaximumPitchDegrees => maximumPitchDegrees;
        public float MaximumYawDegrees => maximumYawDegrees;
        public float CameraShakeAmplitude => cameraShakeAmplitude;
        public float FireInterval => fireInterval;
        public float AdsScalar => adsScalar;

        public void Validate()
        {
            if (recoveryDegreesPerSecond < 0f || maximumPitchDegrees < 0f || maximumYawDegrees < 0f
                || cameraShakeAmplitude < 0f || fireInterval <= 0f)
            {
                throw new System.InvalidOperationException("RecoilProfile contains invalid non-negative settings.");
            }
        }
    }
}
