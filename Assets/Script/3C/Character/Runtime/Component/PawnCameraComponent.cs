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
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
