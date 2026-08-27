using UnityEngine;

namespace CGame
{
    public readonly struct WeaponHitResult
    {
        public WeaponHitResult(
            bool cameraHasHit,
            RaycastHit cameraHit,
            bool hasHit,
            RaycastHit hit,
            Vector3 candidatePoint,
            bool muzzleBlocked,
            Vector3 muzzlePosition,
            Vector3 direction)
        {
            CameraHasHit = cameraHasHit;
            CameraHit = cameraHit;
            HasHit = hasHit;
            Hit = hit;
            CandidatePoint = candidatePoint;
            MuzzleBlocked = muzzleBlocked;
            MuzzlePosition = muzzlePosition;
            Direction = direction;
        }

        public bool CameraHasHit { get; }

        public RaycastHit CameraHit { get; }

        public bool HasHit { get; }

        public RaycastHit Hit { get; }

        public Vector3 CandidatePoint { get; }

        public bool MuzzleBlocked { get; }

        public Vector3 MuzzlePosition { get; }

        public Vector3 Direction { get; }
    }
}
