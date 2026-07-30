using System;
using UnityEngine;

namespace CGame
{
    public sealed class CameraManager : IManager, IWeaponRecoilReceiver, ICameraImpulseReceiver
    {
        private FirstPersonCameraBinding binding;
        private ControllerManager controllerManager;
        private LocalPlayerCameraTargetBinding targetBinding;
        private LocalPlayerCameraRig rig;
        private CinemachineCameraOutput output;
        private FirstPersonCameraProfile profile;
        private WeaponCameraProfile weaponCameraProfile;
        private CameraLocomotionEffectProfile locomotionEffectProfile;
        private OwnerAdsPresentationState adsPresentationState;
        private CameraVisualRecoilState visualRecoilState;
        private CameraImpulseState impulseState;
        private CameraModeRequestStack modeRequestStack;
        private IFirstPersonCameraTarget effectTarget;
        private Func<bool, AimGameplayDecision> aimGameplayDecisionProvider;

        public override int Priority => 60;
        public CameraDebugSnapshot DebugSnapshot { get; private set; }
        public IAdsPresentationState AdsPresentationState => adsPresentationState;
        public WeaponCameraProfile WeaponCameraProfile => weaponCameraProfile;
        public CameraMode ActiveCameraMode => modeRequestStack?.ActiveMode ?? CameraMode.GameplayFirstPerson;

        public override void Init()
        {
            CharacterSpawnManager spawnManager = GameManager.GetManager<CharacterSpawnManager>();
            controllerManager = GameManager.GetManager<ControllerManager>();
            GameManager.GetManager<PhysicsManager>();

            output = CinemachineCameraOutput.Create();
            binding = new FirstPersonCameraBinding();
            targetBinding = new LocalPlayerCameraTargetBinding(spawnManager, binding);
            profile = ScriptableObject.CreateInstance<FirstPersonCameraProfile>();
            weaponCameraProfile = ScriptableObject.CreateInstance<WeaponCameraProfile>();
            locomotionEffectProfile = ScriptableObject.CreateInstance<CameraLocomotionEffectProfile>();
            rig = new LocalPlayerCameraRig(binding, controllerManager, locomotionEffectProfile);
            adsPresentationState = new OwnerAdsPresentationState();
            visualRecoilState = new CameraVisualRecoilState();
            impulseState = new CameraImpulseState();
            modeRequestStack = new CameraModeRequestStack();
        }

        public ICameraModeRequestHandle RequestingCameraMode(CameraModeRequest request)
        {
            if (modeRequestStack == null)
            {
                throw new InvalidOperationException("CameraManager must be initialized before requesting a Camera Mode.");
            }

            return modeRequestStack.Request(request);
        }

        public void ApplyingCameraImpulse(CameraImpulseRequest request)
        {
            if (rig?.Target == null)
            {
                return;
            }

            impulseState?.Apply(request);
        }

        public void ClearingCameraImpulse()
        {
            impulseState?.Reset();
        }

        public void ApplyingWeaponRecoil(WeaponRecoilRequest request)
        {
            if (rig?.Target == null)
            {
                return;
            }

            controllerManager
                ?.GettingController<PlayerController>()
                ?.ApplyingGameplayRecoil(request.GameplayKick, request.GameplayRecoveryDegreesPerSecond);
            visualRecoilState?.Apply(request);
        }

        public void ClearingWeaponRecoil()
        {
            controllerManager
                ?.GettingController<PlayerController>()
                ?.ClearingGameplayRecoil();
            visualRecoilState?.Reset();
        }

        public void SettingAimGameplayDecisionProvider(Func<bool, AimGameplayDecision> provider)
        {
            aimGameplayDecisionProvider = provider;
            if (provider == null)
            {
                adsPresentationState?.Reset();
                controllerManager
                    ?.GettingController<PlayerController>()
                    ?.SettingLookSensitivityMultiplier(1f);
            }
        }

        public override void Update(float elapseSeconds)
        {
        }

        public override void LateUpdate(float elapseSeconds)
        {
            if (rig == null || output == null || profile == null ||
                weaponCameraProfile == null || adsPresentationState == null ||
                visualRecoilState == null || impulseState == null)
            {
                return;
            }

            PlayerController controller =
                controllerManager?.GettingController<PlayerController>();
            IFirstPersonCameraTarget currentTarget = rig.Target;
            if (!ReferenceEquals(effectTarget, currentTarget))
            {
                ClearingWeaponRecoil();
                ClearingCameraImpulse();
                effectTarget = currentTarget;
                output.SetOwnerHead(
                    currentTarget != null
                        ? FindOwnerHead(controller)
                        : null);
            }

            bool aimHeld = controller != null && controller.AimHeld;
            AimGameplayDecision decision = ResolveAimDecision(aimHeld);
            adsPresentationState.ApplyDecision(decision);
            adsPresentationState.Advance(
                elapseSeconds,
                weaponCameraProfile.AdsEnterDuration,
                weaponCameraProfile.AdsExitDuration);

            float adsProgress = adsPresentationState.AdsProgress;
            controller?.SettingLookSensitivityMultiplier(
                Mathf.Lerp(1f, weaponCameraProfile.AdsLookSensitivityMultiplier, adsProgress));
            float worldFieldOfView = Mathf.Lerp(
                profile.BaseFieldOfView,
                weaponCameraProfile.AdsWorldFieldOfView,
                adsProgress);
            CameraVisualRecoilFrame recoilFrame = visualRecoilState.Advance(elapseSeconds);
            CameraPoseDelta impulse = impulseState.Advance(elapseSeconds);
            DebugSnapshot = rig.Sample(
                worldFieldOfView,
                Time.frameCount,
                elapseSeconds,
                recoilFrame.CameraDelta,
                impulse);
            output.Render(
                DebugSnapshot,
                adsProgress,
                weaponCameraProfile,
                recoilFrame.ViewModelDelta,
                modeRequestStack.ActiveRequest);
        }

        public override void Shutdown()
        {
            controllerManager
                ?.GettingController<PlayerController>()
                ?.SettingLookSensitivityMultiplier(1f);
            aimGameplayDecisionProvider = null;
            ClearingWeaponRecoil();
            ClearingCameraImpulse();
            effectTarget = null;
            visualRecoilState = null;
            impulseState = null;
            adsPresentationState?.Reset();
            adsPresentationState = null;
            modeRequestStack?.Dispose();
            modeRequestStack = null;
            targetBinding?.Dispose();
            targetBinding = null;
            binding?.Dispose();
            binding = null;
            rig = null;
            controllerManager = null;
            output?.Dispose();
            output = null;
            if (profile != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(profile);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(profile);
                }

                profile = null;
            }

            if (weaponCameraProfile != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(weaponCameraProfile);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(weaponCameraProfile);
                }

                weaponCameraProfile = null;
            }

            if (locomotionEffectProfile != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(locomotionEffectProfile);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(locomotionEffectProfile);
                }

                locomotionEffectProfile = null;
            }

            DebugSnapshot = default;
        }

        private AimGameplayDecision ResolveAimDecision(bool aimHeld)
        {
            if (!aimHeld)
            {
                return AimGameplayDecision.Released;
            }

            AimGameplayDecision decision = aimGameplayDecisionProvider?.Invoke(true) ??
                AimGameplayDecision.Rejected(AimRejectionReason.NoWeapon);
            if (decision == null || !decision.AimHeld)
            {
                throw new InvalidOperationException("Aim gameplay authority must preserve held aim intent in its decision.");
            }

            return decision;
        }

        private static Transform FindOwnerHead(
            PlayerController controller)
        {
            Transform characterRoot =
                controller?.ControlledPawn?.Host?.transform;
            if (characterRoot == null)
            {
                return null;
            }

            Transform[] transforms =
                characterRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == "Head")
                {
                    return transforms[index];
                }
            }

            return null;
        }
    }
}
