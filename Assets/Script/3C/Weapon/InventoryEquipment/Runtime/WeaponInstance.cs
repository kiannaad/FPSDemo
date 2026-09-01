using System;
using CGame;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Cues;
using CGame.Ability.Targeting;
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
        private Transform muzzlePoint;
        private PlayableGraph reloadPresentationGraph;
        private AnimationClipPlayable reloadPresentationPlayable;
        private bool reloadControllerPlaying;
        private bool reloadTraceLogged;
        private ulong nextShotId;
        private static readonly int ReloadState = Animator.StringToHash("Reload");
        private static readonly int IdleState = Animator.StringToHash("Idle");

        internal WeaponInstance(EquipmentCreateContext context, WeaponDefinition definition)
            : base(context)
        {
            this.definition = definition;
            MagazineCapacity = definition.MagazineCapacity;
            abilitySet = definition.CreateAbilitySet();
        }

        public WeaponDefinition Definition => definition;
        public int MagazineCapacity { get; }

        public int ReloadCount { get; private set; }

        public bool IsArmed { get; private set; }

        public bool IsPrepared => presentationRoot != null;

        public GameObject PresentationRoot => presentationRoot;
        public Transform MuzzlePoint => muzzlePoint;

        public bool IsPresentationVisible { get; private set; }
        public ulong SpreadSequence => nextShotId;

        public bool HasAnimationBinding =>
            animationInstance != null
            && overlayHandle != null
            && overlayHandle.State != AnimationPlaybackState.Failed;

        internal bool TryGetCharacterAnimation(out CharacterAnimInstance value)
        {
            value = animationInstance;
            return !IsDisposed && value != null;
        }

        public bool IsReloadPresentationActive => reloadControllerPlaying || reloadPresentationGraph.IsValid();

        public bool CanBeginReloadPresentation =>
            !IsDisposed
            && IsArmed
            && presentationAnimator != null
            && Definition.ReloadDefinition?.WeaponAnimation != null;

        private bool HasWeaponReloadController =>
            presentationAnimator != null
            && presentationAnimator.runtimeAnimatorController != null
            && presentationAnimator.HasState(0, ReloadState);

        public bool IsReloadPresentationComplete
        {
            get
            {
                if (reloadControllerPlaying)
                {
                    AnimatorStateInfo stateInfo = presentationAnimator.GetCurrentAnimatorStateInfo(0);
                    return stateInfo.shortNameHash == ReloadState
                           && !presentationAnimator.IsInTransition(0)
                           && stateInfo.normalizedTime >= 1f;
                }

                return reloadPresentationGraph.IsValid()
                       && reloadPresentationPlayable.IsValid()
                       && Definition.ReloadDefinition?.WeaponAnimation != null
                       && reloadPresentationPlayable.GetTime() >= Definition.ReloadDefinition.WeaponAnimation.length;
            }
        }

        public void PreparePresentation(GameObject root)
        {
            if (root == null) throw new System.ArgumentNullException(nameof(root));
            if (IsDisposed) throw new System.ObjectDisposedException(nameof(WeaponInstance));
            if (presentationRoot != null) throw new System.InvalidOperationException("Weapon presentation is already prepared.");
            presentationRoot = root;
            weaponAimPoint = FindWeaponAimPoint(root.transform);
            muzzlePoint = FindUniqueMuzzlePoint(root.transform);
            presentationAnimator = root.GetComponentInChildren<Animator>(true);
            if (presentationAnimator != null)
            {
                if (Definition.WeaponAnimatorController != null)
                {
                    presentationAnimator.runtimeAnimatorController = Definition.WeaponAnimatorController;
                }
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
            BindAimPointToPawn();
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

        public bool BeginReloadPresentation()
        {
            if (!CanBeginReloadPresentation)
            {
                return false;
            }

            if (HasWeaponReloadController)
            {
                Debug.Log($"[ReloadTrace] Weapon controller path: animator={presentationAnimator.name}, root={presentationAnimator.transform.root.name}, controller={presentationAnimator.runtimeAnimatorController.name}, reloadState=true, clip={Definition.ReloadDefinition.WeaponAnimation.name}, mag={FindHierarchyPath(presentationAnimator.transform, "Mag")}");
                StopReloadPresentation();
                presentationAnimator.Rebind();
                presentationAnimator.Play(ReloadState, 0, 0f);
                reloadControllerPlaying = true;
                return true;
            }

            StopReloadPresentation();
            Debug.LogWarning($"[ReloadTrace] Weapon controller fallback: animator={presentationAnimator.name}, controller={(presentationAnimator.runtimeAnimatorController != null ? presentationAnimator.runtimeAnimatorController.name : "<null>")}, reloadState=false, clip={Definition.ReloadDefinition.WeaponAnimation.name}, mag={FindHierarchyPath(presentationAnimator.transform, "Mag")}");
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
            if (reloadControllerPlaying)
            {
                reloadControllerPlaying = false;
                if (presentationAnimator != null && presentationAnimator.runtimeAnimatorController != null)
                {
                    presentationAnimator.Rebind();
                    if (presentationAnimator.HasState(0, IdleState))
                    {
                        presentationAnimator.Play(IdleState, 0, 0f);
                    }
                }
            }

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

        public override void Dispose()
        {
            if (IsDisposed) return;
            Disarm();
            HidePresentation();
            if (presentationRoot != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(presentationRoot);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(presentationRoot);
                }
                presentationRoot = null;
            }
            weaponAimPoint = null;
            muzzlePoint = null;
            base.Dispose();
        }

        public GameplayHitResult? QueryHit(Vector3 cameraOrigin, Vector3 cameraDirection)
        {
            if (!IsPrepared || muzzlePoint == null)
            {
                throw new InvalidOperationException("Weapon hit query requires a prepared MuzzlePoint.");
            }

            if (cameraDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraDirection), "Weapon hit query direction must be non-zero.");
            }

            WeaponBulletData bulletData = Definition.BulletData;
            bulletData.Validate();
            Vector3 direction = cameraDirection.normalized;
            bool cameraHasHit = TryGetNearestValidHit(cameraOrigin, direction, bulletData.MaxShootDistance, bulletData.HitLayerMask, out RaycastHit cameraHit);
            Vector3 candidatePoint = cameraHasHit ? cameraHit.point : cameraOrigin + direction * bulletData.MaxShootDistance;
            Vector3 muzzlePosition = muzzlePoint.position;
            Vector3 muzzleToCandidate = candidatePoint - muzzlePosition;
            float candidateDistance = muzzleToCandidate.magnitude;
            if (candidateDistance <= 0.01f)
            {
                return cameraHasHit
                    ? ToGameplayHitResult(cameraHit, cameraOrigin, candidatePoint)
                    : null;
            }

            bool muzzleBlocked = TryGetNearestValidHit(muzzlePosition, muzzleToCandidate / candidateDistance, candidateDistance - 0.01f, bulletData.HitLayerMask, out RaycastHit muzzleHit);
            if (muzzleBlocked) return ToGameplayHitResult(muzzleHit, muzzlePosition, candidatePoint);
            return cameraHasHit ? ToGameplayHitResult(cameraHit, cameraOrigin, candidatePoint) : null;
        }

        public GameplayAbilityTargetDataHandle QueryTargetData(
            Vector3 cameraOrigin,
            Vector3 cameraDirection)
        {
            ulong shotId = ++nextShotId;
            GameplayHitResult? hit = QueryHit(cameraOrigin, cameraDirection);
            if (!hit.HasValue)
            {
                return new GameplayAbilityTargetDataHandle(shotId, System.Array.Empty<SingleTargetHitData>());
            }

            return new GameplayAbilityTargetDataHandle(shotId, new[]
            {
                new SingleTargetHitData(0, hit.Value)
            });
        }

        public void AdvanceHeat(float deltaTime)
        {
        }

        public void ResetHeat()
        {
        }

        private static GameplayHitResult ToGameplayHitResult(
            RaycastHit hit,
            Vector3 traceStart,
            Vector3 traceEnd)
        {
            return new GameplayHitResult(hit.point, hit.normal, hit.collider, traceStart, traceEnd);
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

        private Transform FindUniqueMuzzlePoint(Transform root)
        {
            Transform match = null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "MuzzlePoint")
                {
                    continue;
                }

                if (match != null)
                {
                    throw CreateMuzzlePointException(root.gameObject, "found more than one Transform named MuzzlePoint");
                }

                match = candidate;
            }

            return match ?? throw CreateMuzzlePointException(root.gameObject, "is missing a Transform named MuzzlePoint");
        }

        private InvalidOperationException CreateMuzzlePointException(GameObject root, string reason)
        {
            string definitionName = Definition != null ? Definition.name : "<null>";
            string prefabName = Definition?.Prefab != null ? Definition.Prefab.name : root.name;
            string message = $"[WeaponHit] MuzzlePoint preparation failed: WeaponDefinition={definitionName}, Prefab={prefabName}, reason={reason}.";
            Debug.LogError(message, root);
            return new InvalidOperationException(message);
        }

        private bool TryGetNearestValidHit(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, out RaycastHit nearestHit)
        {
            nearestHit = default;
            if (distance <= 0f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, layerMask, QueryTriggerInteraction.Collide);
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || IsIgnoredHit(hit.collider))
                {
                    continue;
                }

                if (!found || hit.distance < nearestHit.distance)
                {
                    nearestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool IsIgnoredHit(Collider collider)
        {
            Transform colliderTransform = collider.transform;
            if (presentationRoot != null && colliderTransform.IsChildOf(presentationRoot.transform))
            {
                return true;
            }

            if (AbilitySystem.Avatar is Pawn pawn && pawn.Root != null && colliderTransform.IsChildOf(pawn.Root.transform))
            {
                return true;
            }

            return false;
        }
        private static string FindHierarchyPath(Transform root, string targetName)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != targetName)
                {
                    continue;
                }

                return AnimationUtilityPath(candidate, root);
            }

            return "<missing>";
        }

        private static string AnimationUtilityPath(Transform target, Transform root)
        {
            List<string> names = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            if (current == root)
            {
                names.Add(root.name);
            }

            names.Reverse();
            return string.Join("/", names);
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


// Weapon hit-query implementation.
