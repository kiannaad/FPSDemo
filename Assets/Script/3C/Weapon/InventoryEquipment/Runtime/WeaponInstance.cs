using CGame;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;
using CGame.Animation;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
        private Transform weaponAimPoint;
        private PlayableGraph reloadPresentationGraph;
        private AnimationClipPlayable reloadPresentationPlayable;

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

        public bool IsPresentationVisible { get; private set; }

        public bool HasAnimationBinding =>
            animationInstance != null
            && overlayHandle != null
            && overlayHandle.State != AnimationPlaybackState.Failed;

        internal bool TryGetCharacterAnimation(out CharacterAnimInstance value)
        {
            value = animationInstance;
            return !IsDisposed && value != null;
        }

        public bool IsReloadPresentationActive => reloadPresentationGraph.IsValid();

        public bool CanBeginReloadPresentation =>
            !IsDisposed
            && IsArmed
            && presentationAnimator != null
            && Definition.ReloadDefinition?.WeaponAnimation != null;

        public bool IsReloadPresentationComplete =>
            reloadPresentationGraph.IsValid()
            && reloadPresentationPlayable.IsValid()
            && Definition.ReloadDefinition?.WeaponAnimation != null
            && reloadPresentationPlayable.GetTime() >= Definition.ReloadDefinition.WeaponAnimation.length;

        public void PreparePresentation(GameObject root)
        {
            if (root == null) throw new System.ArgumentNullException(nameof(root));
            if (IsDisposed) throw new System.ObjectDisposedException(nameof(WeaponInstance));
            if (presentationRoot != null) throw new System.InvalidOperationException("Weapon presentation is already prepared.");
            presentationRoot = root;
            weaponAimPoint = FindWeaponAimPoint(root.transform);
            BindAimPointToPawn();
            presentationAnimator = root.GetComponentInChildren<Animator>(true);
            if (presentationAnimator != null)
            {
                presentationAnimator.enabled = true;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            IsPresentationVisible = false;
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
        }

        public void Disarm()
        {
            if (IsDisposed)
            {
                return;
            }

            StopReloadPresentation();
            ClearAnimationBinding();
            RevokeAbilitySets();
            IsArmed = false;
            ClearAimPointFromPawn();
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

        public bool BeginReloadPresentation()
        {
            if (!CanBeginReloadPresentation)
            {
                return false;
            }

            StopReloadPresentation();
            reloadPresentationGraph = PlayableGraph.Create($"{presentationAnimator.name}.ReloadPresentation");
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                reloadPresentationGraph,
                "ReloadPresentation",
                presentationAnimator);
            reloadPresentationPlayable = AnimationClipPlayable.Create(
                reloadPresentationGraph,
                Definition.ReloadDefinition.WeaponAnimation);
            output.SetSourcePlayable(reloadPresentationPlayable);
            reloadPresentationGraph.Play();
            return true;
        }

        public bool StopReloadPresentation()
        {
            if (!reloadPresentationGraph.IsValid())
            {
                return false;
            }

            reloadPresentationGraph.Destroy();
            reloadPresentationPlayable = default;
            return true;
        }

        public bool FinishReloadPresentation()
        {
            if (!IsReloadPresentationComplete)
            {
                return false;
            }

            return StopReloadPresentation();
        }

        internal void NotifyReloadCommitted(int loadedAmmo)
        {
            if (loadedAmmo > 0)
            {
                ReloadCount++;
            }
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
            Disarm();
            HidePresentation();
            if (presentationRoot != null)
            {
                UnityEngine.Object.Destroy(presentationRoot);
                presentationRoot = null;
            }
            weaponAimPoint = null;
            base.Dispose();
        }

        private void BindAimPointToPawn()
        {
            if (!(AbilitySystem.Avatar is Pawn pawn) || presentationRoot == null || weaponAimPoint == null)
            {
                return;
            }

            Transform weaponIkTransform = presentationRoot.transform.parent;
            if (weaponIkTransform == null)
            {
                throw new System.InvalidOperationException("Weapon presentation requires an IK WeaponBone parent.");
            }

            Pose aimPointOffset = new Pose(
                -weaponIkTransform.InverseTransformPoint(weaponAimPoint.position),
                Quaternion.Inverse(weaponIkTransform.rotation) * weaponAimPoint.rotation);
            pawn.SetCurrentWeaponAimPoint(weaponAimPoint);
            pawn.SetAimAnimationFacts(pawn.IsAiming, aimPointOffset);
        }

        private void ClearAimPointFromPawn()
        {
            if (AbilitySystem.Avatar is Pawn pawn && ReferenceEquals(pawn.CurrentWeaponAimPoint, weaponAimPoint))
            {
                pawn.SetCurrentWeaponAimPoint(null);
            }
        }

        private static Transform FindWeaponAimPoint(Transform root)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "WeaponAimPoint")
                {
                    return candidate;
                }
            }

            return null;
        }

        public void HidePresentation()
        {
            IsPresentationVisible = false;
            if (presentationRoot == null) return;
            foreach (Renderer renderer in presentationRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        public void ShowPresentation()
        {
            if (presentationRoot == null)
            {
                IsPresentationVisible = false;
                return;
            }

            IsPresentationVisible = true;
            foreach (Renderer renderer in presentationRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
            }
        }

        private void ClearAnimationBinding()
        {
            if (animationInstance != null && overlayHandle != null)
            {
                animationInstance.StopAbilityAnimation(overlayHandle);
            }

            overlayHandle = null;
            animationInstance = null;
        }
    }
}
