using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CGame
{
    public sealed class ViewModelCameraOutput : IDisposable
    {
        public const float FieldOfView = 72f;
        public const float NearClipPlane = 0.01f;

        private readonly Camera worldCamera;
        private readonly UniversalAdditionalCameraData worldCameraData;
        private readonly int originalWorldCullingMask;
        private readonly FirstPersonViewModelPrototype prototype;
        private bool isDisposed;

        private ViewModelCameraOutput(
            Camera worldCamera,
            UniversalAdditionalCameraData worldCameraData,
            Camera camera,
            FirstPersonViewModelPrototype prototype,
            int originalWorldCullingMask)
        {
            this.worldCamera = worldCamera;
            this.worldCameraData = worldCameraData;
            Camera = camera;
            this.prototype = prototype;
            this.originalWorldCullingMask = originalWorldCullingMask;
        }

        public Camera Camera { get; }
        public FirstPersonViewModelPrototype Prototype => prototype;

        public static ViewModelCameraOutput Create(Transform parent, Camera worldCamera, int viewModelLayer)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (worldCamera == null)
            {
                throw new ArgumentNullException(nameof(worldCamera));
            }

            if (viewModelLayer < 0 || viewModelLayer > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(viewModelLayer));
            }

            int viewModelMask = 1 << viewModelLayer;
            int originalWorldCullingMask = worldCamera.cullingMask;
            worldCamera.cullingMask &= ~viewModelMask;
            UniversalAdditionalCameraData worldData =
                worldCamera.GetComponent<UniversalAdditionalCameraData>()
                ?? worldCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            worldData.renderType = CameraRenderType.Base;
            worldData.renderPostProcessing = true;
            worldData.renderShadows = true;

            var cameraObject = new GameObject("ViewModel Overlay Camera");
            cameraObject.transform.SetParent(parent, false);
            Camera viewModelCamera = cameraObject.AddComponent<Camera>();
            viewModelCamera.cullingMask = viewModelMask;
            viewModelCamera.clearFlags = CameraClearFlags.Depth;
            viewModelCamera.fieldOfView = FieldOfView;
            viewModelCamera.nearClipPlane = NearClipPlane;
            viewModelCamera.farClipPlane = 10f;
            viewModelCamera.allowHDR = true;
            viewModelCamera.allowMSAA = true;
            viewModelCamera.depth = worldCamera.depth + 1f;

            UniversalAdditionalCameraData viewModelData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            viewModelData.renderType = CameraRenderType.Overlay;
            viewModelData.renderPostProcessing = false;
            viewModelData.renderShadows = true;
            worldData.cameraStack.Add(viewModelCamera);

            FirstPersonViewModelPrototype prototype = FirstPersonViewModelPrototype.Create(viewModelCamera.transform, viewModelLayer);
            return new ViewModelCameraOutput(worldCamera, worldData, viewModelCamera, prototype, originalWorldCullingMask);
        }

        public void Render(
            bool hasTarget,
            float adsProgress,
            WeaponCameraProfile weaponCameraProfile,
            CameraPoseDelta viewModelRecoil)
        {
            if (isDisposed)
            {
                return;
            }

            Camera.enabled = hasTarget;
            if (hasTarget)
            {
                float progress = Mathf.Clamp01(adsProgress);
                Camera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
                Camera.fieldOfView = Mathf.Lerp(
                    FieldOfView,
                    weaponCameraProfile.AdsViewModelFieldOfView,
                    progress);
                prototype.ApplyPresentation(
                    progress,
                    weaponCameraProfile.AdsViewModelLocalPosition,
                    viewModelRecoil);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (worldCameraData != null && Camera != null)
            {
                worldCameraData.cameraStack.Remove(Camera);
            }

            if (worldCamera != null)
            {
                worldCamera.cullingMask = originalWorldCullingMask;
            }

            prototype.Dispose();
            if (Camera != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Camera.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Camera.gameObject);
                }
            }
        }
    }
}
