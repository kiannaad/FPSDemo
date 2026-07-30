using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    public sealed class LocalPlayerCameraRig
    {
        private readonly FirstPersonCameraBinding binding;
        private readonly ControllerManager controllerManager;
        private readonly CameraLocomotionEffects locomotionEffects;
        private readonly CameraImpulseCollisionConstraint impulseCollisionConstraint =
            new CameraImpulseCollisionConstraint();
        private IFirstPersonCameraTarget sampledTarget;
        private static readonly IReadOnlyList<CameraLayerContribution> NoEffects = Array.Empty<CameraLayerContribution>();

        public LocalPlayerCameraRig(
            FirstPersonCameraBinding binding,
            ControllerManager controllerManager,
            CameraLocomotionEffectProfile locomotionProfile)
        {
            this.binding = binding ?? throw new ArgumentNullException(nameof(binding));
            this.controllerManager = controllerManager ?? throw new ArgumentNullException(nameof(controllerManager));
            locomotionEffects = new CameraLocomotionEffects(
                locomotionProfile != null ? locomotionProfile : throw new ArgumentNullException(nameof(locomotionProfile)));
        }

        public IFirstPersonCameraTarget Target => binding.Target;

        public CameraDebugSnapshot Sample(float fieldOfView, int frame, float deltaTime)
        {
            return Sample(fieldOfView, frame, deltaTime, CameraPoseDelta.None, CameraPoseDelta.None);
        }

        public CameraDebugSnapshot Sample(
            float fieldOfView,
            int frame,
            float deltaTime,
            CameraPoseDelta visualRecoil)
        {
            return Sample(fieldOfView, frame, deltaTime, visualRecoil, CameraPoseDelta.None);
        }

        public CameraDebugSnapshot Sample(
            float fieldOfView,
            int frame,
            float deltaTime,
            CameraPoseDelta visualRecoil,
            CameraPoseDelta impulse)
        {
            IFirstPersonCameraTarget target = binding.Target;
            if (target == null)
            {
                sampledTarget = null;
                locomotionEffects.Reset();
                CameraCompositionResult missingTargetComposition = CameraPoseCompositor.Compose(
                    new CameraPose(default, Quaternion.identity),
                    new CameraLensState(fieldOfView),
                    NoEffects);
                return new CameraDebugSnapshot(false, missingTargetComposition, frame);
            }

            if (!ReferenceEquals(sampledTarget, target))
            {
                sampledTarget = target;
                locomotionEffects.Reset();
            }

            PlayerController controller = controllerManager.GettingController<PlayerController>();
            Quaternion rotation = controller != null ? controller.ControlRotation : target.Rotation;
            CameraLocomotionSample locomotionSample = target is IFirstPersonCameraLocomotionSource source
                ? source.LocomotionSample
                : CameraLocomotionSample.Idle;
            IReadOnlyList<CameraLayerContribution>
                locomotionContributions =
                    target is FirstPersonCameraAnchor anchor
                    && anchor.UsesAnimatedHeadMount
                        ? NoEffects
                        : locomotionEffects.Evaluate(
                            locomotionSample,
                            deltaTime);
            var contributions = new List<CameraLayerContribution>(locomotionContributions.Count + 2);
            for (int index = 0; index < locomotionContributions.Count; index++)
            {
                contributions.Add(locomotionContributions[index]);
            }

            contributions.Add(new CameraLayerContribution(
                CameraEffectLayer.VisualRecoil,
                visualRecoil,
                CameraLensDelta.None,
                visualRecoil.Weight > 0f));
            CameraPose basePose = new CameraPose(target.Position, rotation);
            CameraPoseDelta constrainedImpulse = impulseCollisionConstraint.Constrain(
                basePose,
                impulse,
                target);
            contributions.Add(new CameraLayerContribution(
                CameraEffectLayer.Impulse,
                constrainedImpulse,
                CameraLensDelta.None,
                constrainedImpulse.Weight > 0f));
            CameraCompositionResult composition = CameraPoseCompositor.Compose(
                basePose,
                new CameraLensState(fieldOfView),
                contributions);
            return new CameraDebugSnapshot(true, composition, frame);
        }
    }
}
