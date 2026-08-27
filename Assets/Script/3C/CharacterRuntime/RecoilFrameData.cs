using UnityEngine;

namespace CGame
{
    public readonly struct RecoilFrameData
    {
        public RecoilFrameData(Vector2 rotationOffsetDegrees, Pose weaponRecoilPose, float cameraShakeSample, int shotSequence)
        {
            RotationOffsetDegrees = rotationOffsetDegrees;
            WeaponRecoilPose = weaponRecoilPose;
            CameraShakeSample = cameraShakeSample;
            ShotSequence = shotSequence;
        }

        public Vector2 RotationOffsetDegrees { get; }
        public Pose WeaponRecoilPose { get; }
        public float CameraShakeSample { get; }
        public int ShotSequence { get; }
    }
}
