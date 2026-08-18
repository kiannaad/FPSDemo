using System;
using UnityEngine;

namespace CGame
{
public sealed class PawnCameraComponent : ActorComponent
    {
        private readonly Camera camera;
        private readonly bool requireCamera;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float diagnosticElapsed;
#endif

        public PawnCameraComponent(Camera camera, bool requireCamera)
        {
            this.camera = camera;
            this.requireCamera = requireCamera;
            AddDependency<PawnMovementComponent>();
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
