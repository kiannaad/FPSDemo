using System;
using UnityEngine;

namespace CGame
{
    public sealed class PawnCameraComponent : ActorComponent
    {
        private readonly Camera camera;
        private readonly bool requireCamera;

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

            camera.transform.rotation = pawn.ControlRotation;
        }

    }
}
