using CGame;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;
using CGame.Animation;

namespace CGame.InventoryEquipment
{
    public sealed class WeaponInstance : EquipmentInstance
    {
        private readonly WeaponDefinition definition;
        private readonly AbilitySet abilitySet;
        private GameObject presentationRoot;
        private CharacterAnimInstance animationInstance;
        private AnimationPlaybackHandle overlayHandle;
        private Animator presentationAnimator;

        internal WeaponInstance(EquipmentCreateContext context, WeaponDefinition definition)
            : base(context)
        {
            this.definition = definition;
            MagazineCapacity = definition.MagazineCapacity;
            abilitySet = definition.CreateAbilitySet();
        }

        public WeaponDefinition Definition => definition;
        public int MagazineCapacity { get; }

        public int FireCount { get; private set; }

        public int ReloadCount { get; private set; }

        public int RecoilCount { get; private set; }

        public int MeleeCount { get; private set; }

        public bool IsArmed { get; private set; }

        public bool IsPrepared => presentationRoot != null;

        public GameObject PresentationRoot => presentationRoot;

        public bool HasAnimationBinding =>
            animationInstance != null
            && overlayHandle != null
            && overlayHandle.State != AnimationPlaybackState.Failed;

        public void PreparePresentation(GameObject root)
        {
            if (root == null) throw new System.ArgumentNullException(nameof(root));
            if (IsDisposed) throw new System.ObjectDisposedException(nameof(WeaponInstance));
            if (presentationRoot != null) throw new System.InvalidOperationException("Weapon presentation is already prepared.");
            presentationRoot = root;
            presentationAnimator = root.GetComponentInChildren<Animator>(true);
            if (presentationAnimator != null)
            {
                presentationAnimator.enabled = true;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        public void SetAnimationBinding(
            CharacterAnimInstance owner,
            AnimationPlaybackHandle preparedOverlayHandle)
        {
            if (animationInstance != null || overlayHandle != null)
            {
                throw new System.InvalidOperationException("Weapon animation binding is already prepared.");
            }

            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (preparedOverlayHandle == null) throw new System.ArgumentNullException(nameof(preparedOverlayHandle));
            animationInstance = owner;
            overlayHandle = preparedOverlayHandle;
            if (overlayHandle.State == AnimationPlaybackState.Failed)
            {
                animationInstance = null;
                overlayHandle = null;
                throw new System.InvalidOperationException("Weapon overlay failed before Arming.");
            }
        }

        public void Arm(IReadOnlyList<AbilityGrantReceipt> replacedAbilityReceipts = null)
        {
            if (IsDisposed)
            {
                throw new System.ObjectDisposedException(nameof(WeaponInstance));
            }

            if (IsArmed)
            {
                return;
            }
            GrantAbilitySets(new[] { abilitySet }, replacedAbilityReceipts);
            IsArmed = true;
            if (presentationRoot != null)
            {
                foreach (Renderer renderer in presentationRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            }
        }

        public bool Fire()
        {
            return TryFire().Succeeded;
        }

        public FireResult TryFire()
        {
            if (IsDisposed || !IsArmed || !Item.TryConsumeMagazineAmmo())
            {
                return FireResult.Failed("Weapon is not armed or has no ammunition.", 0);
            }

            FireCount++;
            RecoilCount++;
            Item.SetDurability(Item.Durability - 0.001f);
            Pawn pawn = AbilitySystem.Avatar as Pawn;
            if (pawn != null && Definition.RecoilProfile != null)
            {
                if (!ReferenceEquals(pawn.RecoilProfile, Definition.RecoilProfile))
                {
                    pawn.BindRecoilProfile(Definition.RecoilProfile);
                }

                return pawn.NotifySuccessfulShot();
            }

            return new FireResult(true, FireCount);
        }

        public int Reload()
        {
            if (IsDisposed || !IsArmed)
            {
                return 0;
            }

            int loaded = Item.ReloadMagazine(MagazineCapacity);
            if (loaded > 0)
            {
                ReloadCount++;
            }

            return loaded;
        }

        public bool Melee()
        {
            if (IsDisposed || !IsArmed)
            {
                return false;
            }

            MeleeCount++;
            Item.SetDurability(Item.Durability - 0.01f);
            return true;
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            HidePresentation();
            if (animationInstance != null && overlayHandle != null)
            {
                animationInstance.StopAbilityAnimation(overlayHandle);
            }
            overlayHandle = null;
            animationInstance = null;
            if (presentationRoot != null)
            {
                UnityEngine.Object.Destroy(presentationRoot);
                presentationRoot = null;
            }
            base.Dispose();
        }

        private void HidePresentation()
        {
            if (presentationRoot == null) return;
            foreach (Renderer renderer in presentationRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }
    }
}
