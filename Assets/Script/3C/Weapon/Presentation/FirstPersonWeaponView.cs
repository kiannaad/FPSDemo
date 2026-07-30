using System;
using UnityEngine;

namespace CGame
{
    public sealed class FirstPersonWeaponView : IDisposable
    {
        private const float HipFieldOfView = 60f;
        private const float AdsFieldOfView = 48f;
        private static readonly Vector3 HipLocalPosition = new Vector3(0.55f, -0.15f, 0.6f);
        private static readonly Quaternion HipLocalRotation = Quaternion.Euler(309.3f, 220.1f, 30f);
        private static readonly Vector3 AdsMuzzlePosition = new Vector3(0f, -0.03f, 1.2f);
        private static readonly Vector3 LocalScale = Vector3.one * 0.8f;
        private GameObject instance;
        private Camera outputCamera;
        private Transform viewTransform;
        private WeaponModelActionPlayer modelActionPlayer;
        private Vector3 adsLocalPosition;
        private Quaternion adsLocalRotation;
        private float adsWeight;

        public GameObject Instance => instance;
        public WeaponModelActionPlayer ModelActionPlayer => modelActionPlayer;
        public float AdsWeight => adsWeight;

        public bool Equip(GameObject presentationPrefab)
        {
            Release();
            outputCamera = Camera.main;
            if (presentationPrefab == null || outputCamera == null)
            {
                return false;
            }

            instance = UnityEngine.Object.Instantiate(presentationPrefab, outputCamera.transform, false);
            instance.name = $"{presentationPrefab.name}[FirstPerson]";
            viewTransform = instance.transform;
            viewTransform.localPosition = HipLocalPosition;
            viewTransform.localRotation = HipLocalRotation;
            viewTransform.localScale = LocalScale;

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            WeaponPresentationInstance presentation = instance.GetComponent<WeaponPresentationInstance>();
            modelActionPlayer = presentation?.ModelActionPlayer;
            if (presentation?.Muzzle == null)
            {
                Release();
                return false;
            }

            adsLocalRotation = Quaternion.Inverse(presentation.Muzzle.localRotation);
            Vector3 scaledMuzzlePosition = Vector3.Scale(presentation.Muzzle.localPosition, LocalScale);
            adsLocalPosition = AdsMuzzlePosition - adsLocalRotation * scaledMuzzlePosition;
            adsWeight = 0f;
            return modelActionPlayer != null;
        }

        public void SetWeight(float weight)
        {
            modelActionPlayer?.SetWeight(weight);
        }

        public void Advance(float deltaTime)
        {
            UpdatePose();
            modelActionPlayer?.Advance(deltaTime);
        }

        public bool Play(AnimationClip clip, ulong actionId)
        {
            return modelActionPlayer != null && modelActionPlayer.Play(clip, actionId);
        }

        public bool Stop(ulong actionId)
        {
            return modelActionPlayer != null && modelActionPlayer.Stop(actionId);
        }

        public void Dispose()
        {
            Release();
        }

        private void Release()
        {
            outputCamera = null;
            viewTransform = null;
            modelActionPlayer = null;
            adsWeight = 0f;
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            instance = null;
        }

        private void UpdatePose()
        {
            if (outputCamera == null || viewTransform == null)
            {
                return;
            }

            float targetAdsWeight = Mathf.Clamp01(
                (HipFieldOfView - outputCamera.fieldOfView) / (HipFieldOfView - AdsFieldOfView));
            adsWeight = targetAdsWeight;
            viewTransform.localPosition = Vector3.Lerp(HipLocalPosition, adsLocalPosition, adsWeight);
            viewTransform.localRotation = Quaternion.Slerp(HipLocalRotation, adsLocalRotation, adsWeight);
        }
    }
}
