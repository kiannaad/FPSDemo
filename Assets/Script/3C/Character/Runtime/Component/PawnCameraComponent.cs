using System;
using System.Linq;
using CGame.Animation;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame
{
    public sealed class PawnCameraComponent : ActorComponent
    {
        private readonly Camera camera;
        private readonly bool requireCamera;
        private float defaultFieldOfView;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string lastFacingProbeWeaponName;
        private int ak12FacingProbeFramesRemaining;
        private Transform facingProbeSkeleton;
        private Transform facingProbeSpine;
        private Transform facingProbeUpperChest;
        private Transform facingProbeWeaponBone;
        private Transform facingProbeIkWeaponBone;
#endif

        public PawnCameraComponent(Camera camera, bool requireCamera)
        {
            this.camera = camera;
            this.requireCamera = requireCamera;
            AddDependency<PawnMovementComponent>();
            AddDependency<PawnAnimationComponent>();
        }

        public Camera Camera => camera;

        public int TickCount { get; private set; }

        protected override void OnInitialize()
        {
            if (requireCamera && camera == null)
            {
                throw new InvalidOperationException("PawnDefinition requires a Camera, but the Pawn Prefab has none.");
            }

            AddTickTask("Pawn.Camera", TickGroup.TG_Camera, Tick);
            if (camera != null)
            {
                defaultFieldOfView = camera.fieldOfView;
                camera.enabled = false;
            }
        }

        protected override void OnBeginPlay()
        {
            if (!(Owner is Pawn pawn) || pawn.Controller == null)
            {
                throw new InvalidOperationException("PawnCameraComponent requires possession before BeginPlay.");
            }

            if (camera != null)
            {
                camera.enabled = true;
            }
        }

        protected override void OnEndPlay()
        {
            if (camera != null)
            {
                camera.enabled = false;
            }
        }

        private void Tick(float deltaTime)
        {
            TickCount++;
            if (camera == null || !(Owner is Pawn pawn) || pawn.Transform == null)
            {
                return;
            }

            PawnAnimationComponent animation = pawn.GetComponent<PawnAnimationComponent>();
            AdsLayerSettings ads = animation?.AnimInstance?.BoneController.ActiveProfile?.Layers
                .OfType<AdsLayerSettings>()
                .FirstOrDefault();
            Transform animationCamera = null;
            if (ads != null && animation.RigComponent != null)
            {
                animationCamera = RigHandleUtility.ResolveTransform(
                    animation.RigComponent,
                    ads.AimTargetBone,
                    nameof(PawnCameraComponent));
                camera.transform.position = animationCamera.position;
            }

            EquipmentManagerComponent equipment = pawn.GetComponent<EquipmentManagerComponent>();
            float targetFieldOfView = pawn.IsAiming && equipment?.CurrentWeapon?.Definition != null
                ? equipment.CurrentWeapon.Definition.AimFov
                : defaultFieldOfView;
            camera.fieldOfView = Mathf.MoveTowards(camera.fieldOfView, targetFieldOfView, 90f * deltaTime);
            float controlPitch = Mathf.DeltaAngle(0f, pawn.ControlRotation.eulerAngles.x);
            Vector2 recoilOffset = IsFinite(pawn.RecoilRotationOffsetDegrees)
                ? pawn.RecoilRotationOffsetDegrees
                : Vector2.zero;
            float cameraShake = IsFinite(pawn.CameraShakeSample)
                ? pawn.CameraShakeSample
                : 0f;

            Quaternion finalRotation = pawn.Transform.rotation
                * Quaternion.Euler(controlPitch, 0f, 0f)
                * Quaternion.Euler(-recoilOffset.x, recoilOffset.y, 0f)
                * Quaternion.Euler(0f, 0f, cameraShake);
            camera.transform.rotation = finalRotation;
            pawn.PublishCameraShotRay(camera.transform.position, camera.transform.forward);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WriteAk12FacingProbe(animation, equipment);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void WriteAk12FacingProbe(
            PawnAnimationComponent animation,
            EquipmentManagerComponent equipment)
        {
            string weaponName = equipment?.CurrentWeapon?.Definition?.name;
            if (weaponName != lastFacingProbeWeaponName)
            {
                lastFacingProbeWeaponName = weaponName;
                ak12FacingProbeFramesRemaining = weaponName == "AK12WeaponDefinition" ? 45 : 0;
                ClearFacingProbeTransforms();
            }

            if (ak12FacingProbeFramesRemaining <= 0 || animation?.RigComponent == null)
            {
                return;
            }

            CacheFacingProbeTransforms(animation.RigComponent.transform);
            if (facingProbeSkeleton == null
                || facingProbeSpine == null
                || facingProbeUpperChest == null
                || facingProbeWeaponBone == null
                || facingProbeIkWeaponBone == null)
            {
                Debug.LogWarning("[Ak12FacingProbe] required rig transforms are unavailable.", camera);
                ak12FacingProbeFramesRemaining = 0;
                return;
            }

            float cameraYaw = ResolveHorizontalYaw(camera.transform);
            Debug.Log(
                $"[Ak12FacingProbe] frame={Time.frameCount}; remaining={ak12FacingProbeFramesRemaining}; "
                + $"profile={animation.AnimInstance?.BoneController.ActiveProfile?.name ?? "<none>"}; "
                + $"switching={equipment.IsSwitchInProgress}; "
                + $"camera={cameraYaw:F2}; parent={ResolveHorizontalYaw(camera.transform.parent):F2}; "
                + $"skeleton={ResolveHorizontalYaw(facingProbeSkeleton):F2}; "
                + $"spine={ResolveHorizontalYaw(facingProbeSpine):F2}; "
                + $"upperChest={ResolveHorizontalYaw(facingProbeUpperChest):F2}; "
                + $"weaponBone={ResolveHorizontalYaw(facingProbeWeaponBone):F2}; "
                + $"ikWeapon={ResolveHorizontalYaw(facingProbeIkWeaponBone):F2}; "
                + $"cameraToUpperChest={SignedHorizontalAngle(camera.transform, facingProbeUpperChest):F2}; "
                + $"cameraToIkWeapon={SignedHorizontalAngle(camera.transform, facingProbeIkWeaponBone):F2}",
                camera);
            ak12FacingProbeFramesRemaining--;
        }

        private void CacheFacingProbeTransforms(Transform root)
        {
            if (facingProbeSkeleton != null)
            {
                return;
            }

            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                switch (candidate.name)
                {
                    case "Skeleton": facingProbeSkeleton = candidate; break;
                    case "Spine": facingProbeSpine = candidate; break;
                    case "UpperChest": facingProbeUpperChest = candidate; break;
                    case "WeaponBone": facingProbeWeaponBone = candidate; break;
                    case "IK WeaponBone": facingProbeIkWeaponBone = candidate; break;
                }
            }
        }

        private void ClearFacingProbeTransforms()
        {
            facingProbeSkeleton = null;
            facingProbeSpine = null;
            facingProbeUpperChest = null;
            facingProbeWeaponBone = null;
            facingProbeIkWeaponBone = null;
        }

        private static float ResolveHorizontalYaw(Transform transform)
        {
            if (transform == null)
            {
                return 0f;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.000001f
                ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
                : 0f;
        }

        private static float SignedHorizontalAngle(Transform from, Transform to)
        {
            Vector3 fromForward = Vector3.ProjectOnPlane(from.forward, Vector3.up);
            Vector3 toForward = Vector3.ProjectOnPlane(to.forward, Vector3.up);
            return Vector3.SignedAngle(fromForward, toForward, Vector3.up);
        }
#endif

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
