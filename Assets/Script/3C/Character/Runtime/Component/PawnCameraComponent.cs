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
        private float diagnosticElapsed;
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
            if (ads != null && animation.RigComponent != null)
            {
                Transform animationCamera = RigHandleUtility.ResolveTransform(
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
            camera.transform.rotation = pawn.EffectivePresentationRotation
                * Quaternion.Euler(0f, 0f, pawn.CameraShakeSample);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogPresentationPose(pawn, deltaTime);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogPresentationPose(Pawn pawn, float deltaTime)
        {
            diagnosticElapsed += Mathf.Max(0f, deltaTime);
            if (diagnosticElapsed < 0.25f)
            {
                return;
            }

            diagnosticElapsed = 0f;
            Transform weaponMount = null;
            foreach (Transform transform in pawn.Root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "IK WeaponBone")
                {
                    weaponMount = transform;
                    break;
                }
            }

            if (weaponMount == null)
            {
                return;
            }

            Vector3 localPosition = camera.transform.InverseTransformPoint(weaponMount.position);
            Quaternion localRotation = Quaternion.Inverse(camera.transform.rotation) * weaponMount.rotation;
            Debug.Log(
                $"[CameraPresentation] Presentation={pawn.PresentationRotation.eulerAngles}; "
                + $"Camera={camera.transform.rotation.eulerAngles}; "
                + $"IkWeaponInCamera={localPosition:F3}/{localRotation.eulerAngles:F3}",
                camera);
        }
#endif
    }
}
