using System;
using System.Linq;
using UnityEngine;

namespace CGame.Animation
{
    public sealed class CharacterWeaponPresentationController : IDisposable
    {
        private readonly Animator animator;
        private WeaponRuntime boundRuntime;
        private WeaponAnimationDefinition currentDefinition;
        private WeaponPresentationInstance currentPresentation;
        private CharacterWeaponPresentationReplacement pendingReplacement;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private Vector3 baseLocalScale;
        private WeaponActionKind presentationAction;
        private ulong presentationActionId;
        private float presentationActionTime;
        private float presentationActionDuration;
        private uint currentGeneration;
        private bool isDisposed;

        public CharacterWeaponPresentationController(Animator animator)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
        }

        public WeaponPresentationInstance CurrentPresentation => currentPresentation;
        public WeaponAnimationDefinition CurrentDefinition => currentDefinition;
        public uint CurrentGeneration => currentGeneration;

        public void BindRuntime(WeaponRuntime runtime)
        {
            if (ReferenceEquals(boundRuntime, runtime))
            {
                return;
            }

            if (boundRuntime != null)
            {
                boundRuntime.ActionChanged -= HandleActionChanged;
            }

            boundRuntime = runtime;
            if (boundRuntime != null)
            {
                boundRuntime.ActionChanged += HandleActionChanged;
            }
        }

        public bool TryEquip(WeaponAnimationDefinition definition, uint generation)
        {
            if (isDisposed
                || pendingReplacement != null
                || definition == null
                || definition.WeaponPrefab == null
                || generation == 0u)
            {
                return false;
            }

            Transform attachment = ResolveAttachment();
            if (attachment == null)
            {
                return false;
            }

            GameObject candidateObject = UnityEngine.Object.Instantiate(definition.WeaponPrefab);
            candidateObject.name = $"{definition.WeaponPrefab.name}[{generation}]";
            WeaponPresentationInstance candidate =
                candidateObject.GetComponent<WeaponPresentationInstance>();
            if (candidate == null || !candidate.AttachTo(attachment))
            {
                DestroyObject(candidateObject);
                return false;
            }

            WeaponPresentationInstance previous = currentPresentation;
            currentPresentation = candidate;
            currentDefinition = definition;
            currentGeneration = generation;
            CaptureBasePose();
            ClearPresentationAction();
            DestroyPresentation(previous);
            return true;
        }

        public CharacterWeaponPresentationReplacement PrepareReplacement(
            WeaponAnimationDefinition definition,
            uint generation)
        {
            if (isDisposed
                || pendingReplacement != null
                || currentPresentation == null
                || definition == null
                || definition.WeaponPrefab == null
                || generation == 0u)
            {
                return null;
            }

            Transform attachment = ResolveAttachment();
            if (attachment == null)
            {
                return null;
            }

            GameObject candidateObject =
                UnityEngine.Object.Instantiate(definition.WeaponPrefab);
            candidateObject.name = $"{definition.WeaponPrefab.name}[{generation}]";
            WeaponPresentationInstance candidate =
                candidateObject.GetComponent<WeaponPresentationInstance>();
            if (candidate == null || !candidate.AttachTo(attachment))
            {
                DestroyObject(candidateObject);
                return null;
            }

            currentPresentation.gameObject.SetActive(false);
            pendingReplacement = new CharacterWeaponPresentationReplacement(
                this,
                currentPresentation,
                candidate,
                definition,
                generation);
            return pendingReplacement;
        }

        internal bool TryCommitReplacement(
            CharacterWeaponPresentationReplacement replacement)
        {
            if (isDisposed
                || replacement == null
                || !ReferenceEquals(pendingReplacement, replacement)
                || !ReferenceEquals(
                    currentPresentation,
                    replacement.ExpectedCurrent)
                || replacement.Candidate == null)
            {
                return false;
            }

            WeaponPresentationInstance previous = currentPresentation;
            currentPresentation = replacement.Candidate;
            currentDefinition = replacement.Definition;
            currentGeneration = replacement.Generation;
            pendingReplacement = null;
            replacement.MarkFinalized();
            CaptureBasePose();
            ClearPresentationAction();
            DestroyPresentation(previous);
            return true;
        }

        internal void RollbackReplacement(
            CharacterWeaponPresentationReplacement replacement)
        {
            if (replacement == null
                || !ReferenceEquals(pendingReplacement, replacement))
            {
                replacement?.MarkFinalized();
                return;
            }

            pendingReplacement = null;
            WeaponPresentationInstance candidate = replacement.Candidate;
            if (ReferenceEquals(
                    currentPresentation,
                    replacement.ExpectedCurrent)
                && currentPresentation != null)
            {
                currentPresentation.gameObject.SetActive(true);
            }

            replacement.MarkFinalized();
            DestroyPresentation(candidate);
        }

        public void Update(float deltaTime)
        {
            if (isDisposed || deltaTime < 0f)
            {
                return;
            }

            currentPresentation?.ModelActionPlayer?.Advance(deltaTime);
            UpdatePresentationAction(deltaTime);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            pendingReplacement?.Dispose();
            pendingReplacement = null;
            isDisposed = true;
            BindRuntime(null);
            DestroyPresentation(currentPresentation);
            currentPresentation = null;
            currentDefinition = null;
            currentGeneration = 0u;
        }

        private Transform ResolveAttachment()
        {
            Camera outputCamera = Application.isPlaying ? Camera.main : null;
            return outputCamera != null ? outputCamera.transform : ResolveRightHand();
        }

        private Transform ResolveRightHand()
        {
            if (animator.isHuman)
            {
                Transform humanRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (humanRightHand != null)
                {
                    return humanRightHand;
                }
            }

            return animator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform =>
                    string.Equals(transform.name, "Right_Hand", StringComparison.Ordinal)
                    || string.Equals(transform.name, "RightHand", StringComparison.Ordinal));
        }

        private void HandleActionChanged(WeaponActionFact fact)
        {
            if (currentPresentation == null
                || currentDefinition == null
                || fact.Generation != currentGeneration
                || fact.WeaponId != currentDefinition.WeaponId)
            {
                return;
            }

            if (fact.Phase == WeaponActionPhase.Started)
            {
                StartPresentationAction(fact);
                WeaponModelActionPlayer player = currentPresentation.ModelActionPlayer;
                AnimationClipAsset asset = fact.Kind == WeaponActionKind.Fire
                    ? currentDefinition.WeaponModelFire
                    : fact.Kind == WeaponActionKind.Reload
                        ? currentDefinition.WeaponModelReload
                        : null;
                if (player != null && asset?.AnimationClip != null)
                {
                    player.Play(asset.AnimationClip, fact.ActionId);
                }

                return;
            }

            currentPresentation.ModelActionPlayer?.Stop(fact.ActionId);
            if (presentationActionId == fact.ActionId)
            {
                ClearPresentationAction();
                ApplyPresentationPose(0f);
            }
        }

        private void CaptureBasePose()
        {
            Transform presentationTransform = currentPresentation.transform;
            baseLocalPosition = presentationTransform.localPosition;
            baseLocalRotation = presentationTransform.localRotation;
            baseLocalScale = presentationTransform.localScale;
        }

        private void StartPresentationAction(WeaponActionFact fact)
        {
            presentationAction = fact.Kind;
            presentationActionId = fact.ActionId;
            presentationActionTime = 0f;
            AnimationClipAsset asset = fact.Kind == WeaponActionKind.Fire
                ? currentDefinition.Fire
                : fact.Kind == WeaponActionKind.Reload
                    ? currentDefinition.Reload
                    : fact.Kind == WeaponActionKind.MeleeAttack
                        ? currentDefinition.MeleeAttack
                        : null;
            presentationActionDuration = asset?.AnimationClip == null
                ? 0.2f
                : asset.AnimationClip.length / Mathf.Max(0.01f, Mathf.Abs(asset.Speed));
            ApplyPresentationPose(0f);
        }

        private void UpdatePresentationAction(float deltaTime)
        {
            if (currentPresentation == null || presentationAction == WeaponActionKind.None)
            {
                return;
            }

            presentationActionTime += deltaTime;
            float normalizedTime = Mathf.Clamp01(
                presentationActionTime / Mathf.Max(0.01f, presentationActionDuration));
            ApplyPresentationPose(normalizedTime);
        }

        private void ApplyPresentationPose(float normalizedTime)
        {
            if (currentPresentation == null)
            {
                return;
            }

            float weight = Mathf.Sin(Mathf.PI * Mathf.Clamp01(normalizedTime));
            Vector3 positionOffset = Vector3.zero;
            Vector3 rotationOffset = Vector3.zero;
            switch (presentationAction)
            {
                case WeaponActionKind.Fire:
                    float recoilTime = Mathf.Clamp01(normalizedTime / 0.35f);
                    weight = Mathf.Sin(Mathf.PI * recoilTime);
                    positionOffset = new Vector3(0f, -0.012f, -0.06f) * weight;
                    rotationOffset = new Vector3(-8f, 0f, 1.5f) * weight;
                    break;
                case WeaponActionKind.Reload:
                    positionOffset = new Vector3(-0.08f, -0.04f, 0.025f) * weight;
                    rotationOffset = new Vector3(8f, 0f, -24f) * weight;
                    break;
                case WeaponActionKind.MeleeAttack:
                    positionOffset = new Vector3(-0.1f, 0.05f, -0.08f) * weight;
                    rotationOffset = new Vector3(-18f, -65f, 32f) * weight;
                    break;
            }

            Transform presentationTransform = currentPresentation.transform;
            presentationTransform.localPosition = baseLocalPosition + positionOffset;
            presentationTransform.localRotation = baseLocalRotation * Quaternion.Euler(rotationOffset);
            presentationTransform.localScale = baseLocalScale;
        }

        private void ClearPresentationAction()
        {
            presentationAction = WeaponActionKind.None;
            presentationActionId = 0ul;
            presentationActionTime = 0f;
            presentationActionDuration = 0f;
        }

        private static void DestroyPresentation(WeaponPresentationInstance presentation)
        {
            if (presentation != null)
            {
                DestroyObject(presentation.gameObject);
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
