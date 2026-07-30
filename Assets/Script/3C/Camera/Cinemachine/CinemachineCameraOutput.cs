using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CGame
{
    public sealed class CinemachineCameraOutput : IDisposable
    {
        private const string RuntimeRootName = "[CameraRuntimeRoot]";
        private readonly GameObject runtimeRoot;
        private readonly CinemachineCamera virtualCamera;
        private readonly CinemachineCamera firstModeCamera;
        private readonly CinemachineCamera secondModeCamera;
        private readonly SingleCameraPresentationDriver presentationDriver;
        private readonly ViewModelCameraOutput viewModelOutput;
        private CinemachineCamera activeModeCamera;
        private CameraModeRequest activeRequest;
        private CameraModeTransition returnTransition = CameraModeTransition.Cut;
        private float returnDuration;
        private CameraDebugSnapshot pendingGameplaySnapshot;
        private bool hasPendingGameplaySnapshot;
        private bool isDisposed;

        private CinemachineCameraOutput(
            GameObject runtimeRoot,
            Camera worldCamera,
            CinemachineBrain brain,
            CinemachineCamera virtualCamera,
            CinemachineCamera firstModeCamera,
            CinemachineCamera secondModeCamera,
            ViewModelCameraOutput viewModelOutput)
        {
            this.runtimeRoot = runtimeRoot;
            WorldCamera = worldCamera;
            Brain = brain;
            this.virtualCamera = virtualCamera;
            this.firstModeCamera = firstModeCamera;
            this.secondModeCamera = secondModeCamera;
            this.viewModelOutput = viewModelOutput;
            presentationDriver = worldCamera.GetComponent<SingleCameraPresentationDriver>();
            Camera.onPreCull += PresentGameplayCameraBeforeRender;
        }

        public Camera WorldCamera { get; }
        public CinemachineBrain Brain { get; }
        public CinemachineCamera VirtualCamera => virtualCamera;
        public Camera ViewModelCamera => viewModelOutput?.Camera;
        public FirstPersonViewModelPrototype ViewModelPrototype => viewModelOutput?.Prototype;

        public void SetOwnerHead(Transform ownerHead)
        {
            presentationDriver.SetOwnerHead(
                ownerHead,
                ownerHead != null);
        }

        public static CinemachineCameraOutput Create()
        {
            return Create(false);
        }

        public static CinemachineCameraOutput CreateWithViewModel()
        {
            return Create(true);
        }

        private static CinemachineCameraOutput Create(bool createViewModel)
        {
            GameObject root = new GameObject(RuntimeRootName);

            int viewModelLayer = createViewModel ? LayerMask.NameToLayer("FirstPersonViewModel") : -1;
            if (createViewModel && viewModelLayer < 0)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException("The FirstPersonViewModel layer is required for the Camera Stack.");
            }

            GameObject worldCameraObject = new GameObject("Main Camera");
            worldCameraObject.tag = "MainCamera";
            worldCameraObject.transform.SetParent(root.transform, false);
            Camera worldCamera = worldCameraObject.AddComponent<Camera>();
            worldCamera.nearClipPlane = 0.03f;
            worldCameraObject.AddComponent<SingleCameraPresentationDriver>();
            worldCameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData worldData = worldCameraObject.AddComponent<UniversalAdditionalCameraData>();
            worldData.renderType = CameraRenderType.Base;
            worldData.renderPostProcessing = true;
            worldData.renderShadows = true;
            CinemachineBrain brain = worldCameraObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            GameObject virtualCameraObject = new GameObject("First Person Camera");
            virtualCameraObject.transform.SetParent(root.transform, false);
            CinemachineCamera virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.Priority = 30;

            CinemachineCamera firstModeCamera = CreatingModeCamera(root.transform, "Camera Mode A");
            CinemachineCamera secondModeCamera = CreatingModeCamera(root.transform, "Camera Mode B");

            ViewModelCameraOutput viewModelOutput = createViewModel
                ? ViewModelCameraOutput.Create(root.transform, worldCamera, viewModelLayer)
                : null;

            return new CinemachineCameraOutput(
                root,
                worldCamera,
                brain,
                virtualCamera,
                firstModeCamera,
                secondModeCamera,
                viewModelOutput);
        }

        public void Render(
            CameraDebugSnapshot snapshot,
            float adsProgress,
            WeaponCameraProfile weaponCameraProfile,
            CameraPoseDelta viewModelRecoil,
            CameraModeRequest modeRequest)
        {
            if (isDisposed)
            {
                return;
            }

            virtualCamera.enabled = snapshot.HasTarget;
            if (snapshot.HasTarget)
            {
                virtualCamera.transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
                virtualCamera.ForceCameraPosition(snapshot.Position, snapshot.Rotation);
                LensSettings lens = virtualCamera.Lens;
                lens.FieldOfView = snapshot.FieldOfView;
                virtualCamera.Lens = lens;
            }

            if (!ReferenceEquals(activeRequest, modeRequest))
            {
                SwitchingRequest(modeRequest);
            }

            if (modeRequest != null && modeRequest.Target.IsValid)
            {
                UpdatingModeCamera(activeModeCamera, modeRequest.Target);
            }

            Brain.ManualUpdate();
            hasPendingGameplaySnapshot = modeRequest == null && snapshot.HasTarget && activeModeCamera == null;
            if (hasPendingGameplaySnapshot)
            {
                pendingGameplaySnapshot = snapshot;
                presentationDriver.SetFrame(
                    new CameraPose(snapshot.Position, snapshot.Rotation),
                    snapshot.FieldOfView);
                PresentGameplayCamera();
            }
            else
            {
                presentationDriver.ClearFrame();
            }

            bool gameplayPresentationVisible = modeRequest == null && snapshot.HasTarget && !Brain.IsBlending;
            presentationDriver.SetOwnerHeadHidden(
                modeRequest == null && snapshot.HasTarget);
            viewModelOutput?.Render(gameplayPresentationVisible, adsProgress, weaponCameraProfile, viewModelRecoil);

            if (modeRequest == null && !Brain.IsBlending && activeModeCamera != null)
            {
                activeModeCamera.Priority = 0;
                activeModeCamera.enabled = false;
                activeModeCamera = null;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Camera.onPreCull -= PresentGameplayCameraBeforeRender;
            presentationDriver.SetOwnerHead(null, false);
            viewModelOutput?.Dispose();
            if (runtimeRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(runtimeRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        private static CinemachineCamera CreatingModeCamera(Transform parent, string name)
        {
            GameObject modeCameraObject = new GameObject(name);
            modeCameraObject.transform.SetParent(parent, false);
            CinemachineCamera modeCamera = modeCameraObject.AddComponent<CinemachineCamera>();
            modeCamera.Priority = 0;
            modeCamera.enabled = false;
            return modeCamera;
        }

        private void PresentGameplayCameraBeforeRender(Camera camera)
        {
            if (!isDisposed && camera == WorldCamera && hasPendingGameplaySnapshot)
            {
                PresentGameplayCamera();
            }
        }

        private void PresentGameplayCamera()
        {
            WorldCamera.transform.SetPositionAndRotation(
                pendingGameplaySnapshot.Position,
                pendingGameplaySnapshot.Rotation);
            WorldCamera.fieldOfView = pendingGameplaySnapshot.FieldOfView;
        }

        private void SwitchingRequest(CameraModeRequest request)
        {
            if (request == null)
            {
                if (activeRequest != null)
                {
                    returnTransition = activeRequest.Transition;
                    returnDuration = activeRequest.Duration;
                }

                SettingBlend(returnTransition, returnDuration);
                virtualCamera.Priority = 30;
                if (activeModeCamera != null)
                {
                    activeModeCamera.Priority = 20;
                }

                activeRequest = null;
                return;
            }

            CinemachineCamera nextModeCamera = ReferenceEquals(activeModeCamera, firstModeCamera)
                ? secondModeCamera
                : firstModeCamera;
            UpdatingModeCamera(nextModeCamera, request.Target);
            nextModeCamera.enabled = true;
            SettingBlend(request.Transition, request.Duration);
            if (activeModeCamera != null)
            {
                activeModeCamera.Priority = 20;
            }

            virtualCamera.Priority = 10;
            nextModeCamera.Priority = 30;
            activeModeCamera = nextModeCamera;
            activeRequest = request;
            returnTransition = request.Transition;
            returnDuration = request.Duration;
        }

        private void SettingBlend(CameraModeTransition transition, float duration)
        {
            CinemachineBlendDefinition.Styles style = transition == CameraModeTransition.Cut
                ? CinemachineBlendDefinition.Styles.Cut
                : CinemachineBlendDefinition.Styles.EaseInOut;
            Brain.DefaultBlend = new CinemachineBlendDefinition(style, transition == CameraModeTransition.Cut ? 0f : duration);
        }

        private static void UpdatingModeCamera(CinemachineCamera modeCamera, ICameraModeTarget target)
        {
            if (modeCamera == null || target == null || !target.IsValid)
            {
                return;
            }

            CameraPose pose = target.Pose;
            modeCamera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            modeCamera.ForceCameraPosition(pose.Position, pose.Rotation);
            LensSettings lens = modeCamera.Lens;
            lens.FieldOfView = target.FieldOfView;
            modeCamera.Lens = lens;
        }
    }
}
