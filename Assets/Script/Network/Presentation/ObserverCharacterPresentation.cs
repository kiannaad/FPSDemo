using System;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    public sealed class ObserverCharacterPresentation
    {
        private readonly Transform bodyRoot;
        private readonly GameObject weaponRoot;
        private readonly ObserverAimPresentationState state = new ObserverAimPresentationState();

        public ObserverCharacterPresentation(
            Transform bodyRoot,
            GameObject weaponRoot)
        {
            this.bodyRoot = bodyRoot ?? throw new ArgumentNullException(nameof(bodyRoot));
            this.weaponRoot = weaponRoot;
            if (weaponRoot != null)
            {
                weaponRoot.SetActive(false);
            }
        }

        public ObserverAimPresentationSnapshot Snapshot => state.Snapshot;

        public void ApplyFrame(ObserverAimFrame frame)
        {
            state.Apply(frame);
        }

        public ObserverAimPresentationSnapshot Advance(float deltaTime)
        {
            ObserverAimPresentationSnapshot snapshot = state.Advance(deltaTime);
            bodyRoot.rotation = Quaternion.Euler(0f, snapshot.BodyYaw, 0f);
            if (weaponRoot != null && weaponRoot.activeSelf != snapshot.WeaponVisible)
            {
                weaponRoot.SetActive(snapshot.WeaponVisible);
            }

            return snapshot;
        }

        public void Clear()
        {
            state.Clear();
            if (weaponRoot != null)
            {
                weaponRoot.SetActive(false);
            }
        }
    }
}
