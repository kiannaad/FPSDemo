using UnityEngine;

namespace CGame
{
    public sealed class RecoilComponent
    {
        private readonly Pawn pawn;
        private RecoilProfile profile;
                private Pose weaponRecoilPose = new Pose(Vector3.zero, Quaternion.identity);
private Vector2 rotationOffsetDegrees;
        private int shotSequence;

        public RecoilComponent(Pawn pawn)
        {
            this.pawn = pawn ?? throw new System.ArgumentNullException(nameof(pawn));
        }

        public RecoilFrameData FrameData { get; private set; }
        public int ShotSequence => shotSequence;

        public void Bind(RecoilProfile nextProfile)
        {
            profile = nextProfile ?? throw new System.ArgumentNullException(nameof(nextProfile));
            profile.Validate();
            Reset();
        }

        public FireResult ApplySuccessfulShot(bool isAiming)
        {
            if (profile == null)
            {
                return FireResult.Failed("Recoil profile is not bound.", shotSequence);
            }

            Vector2 kick = profile.KickDegrees;
            rotationOffsetDegrees = new Vector2(
                Mathf.Clamp(rotationOffsetDegrees.x + kick.x, -profile.MaximumPitchDegrees, profile.MaximumPitchDegrees),
                Mathf.Clamp(rotationOffsetDegrees.y + kick.y, -profile.MaximumYawDegrees, profile.MaximumYawDegrees));
            shotSequence++;
            weaponRecoilPose = profile.WeaponRecoilPose;
            FrameData = new RecoilFrameData(
                rotationOffsetDegrees,
                profile.WeaponRecoilPose,
                profile.CameraShakeAmplitude,
                shotSequence);
            pawn.SetRecoilFrameData(FrameData);
            return new FireResult(true, shotSequence);
        }

        public void Advance(float deltaTime)
        {
            if (profile == null || deltaTime <= 0f)
            {
                return;
            }

            rotationOffsetDegrees = Vector2.MoveTowards(
                rotationOffsetDegrees,
                Vector2.zero,
                profile.RecoveryDegreesPerSecond * deltaTime);
            weaponRecoilPose = RecoverPose(weaponRecoilPose, deltaTime, profile.RecoveryDegreesPerSecond);
            FrameData = new RecoilFrameData(rotationOffsetDegrees, weaponRecoilPose, 0f, shotSequence);
            pawn.SetRecoilFrameData(FrameData);
        }

        public void Reset()
        {
            rotationOffsetDegrees = Vector2.zero;
            shotSequence = 0;
            weaponRecoilPose = new Pose(Vector3.zero, Quaternion.identity);
            FrameData = new RecoilFrameData(Vector2.zero, weaponRecoilPose, 0f, 0);
            pawn.SetRecoilFrameData(FrameData);
        }

        public void Unbind()
        {
            profile = null;
            Reset();
        }

                private static Pose RecoverPose(Pose pose, float deltaTime, float recoverySpeed)
        {
            float alpha = Mathf.Clamp01(recoverySpeed * deltaTime);
            return new Pose(
                Vector3.Lerp(pose.position, Vector3.zero, alpha),
                Quaternion.Slerp(pose.rotation, Quaternion.identity, alpha));
        }

    }
}
