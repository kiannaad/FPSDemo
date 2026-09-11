using System;
using CGame.Animation;
using UnityEngine;
using UnityEngine.Playables;

namespace CGame.Network
{
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveDirectionParameter = Animator.StringToHash("MoveDirection");
        private static readonly int FireParameter = Animator.StringToHash("Fire");
        private static readonly int HitParameter = Animator.StringToHash("Hit");
        private static readonly int IsInCoverParameter = Animator.StringToHash("IsInCover");
        private static readonly int IsPeekingParameter = Animator.StringToHash("IsPeeking");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        private Pawn playbackPawn;
        private CharacterPlayablesController playablesController;
        private float hitRemainingSeconds;
        private float deathElapsedSeconds;
        private Vector3 deathStartPosition;
        private Quaternion deathStartRotation;
        private Quaternion deathFallRotation;
        private Vector3 deathEndPosition;
        private bool deathPoseFrozen;

        public Animator Animator => animator;
        public RemoteEnemyAnimationState RemoteAnimationState { get; private set; }
        public EnemyActionKind LastConfirmedAction { get; private set; }
        public bool HasPlayableGraph => playablesController != null && playablesController.IsValid();
        public Transform VisualRoot => visualRoot;
        public bool IsDead { get; private set; }
        public bool IsDeathPresentationComplete => IsDead && deathElapsedSeconds >= 2.5f;

        public void Configure(Animator value)
        {
            animator = value ?? throw new ArgumentNullException(nameof(value));
            animator.applyRootMotion = false;
        }

        public void ApplyMovement(float speed, Vector2 moveDirection)
        {
            if (IsDead) return;
            EnsureAnimator();
            animator.SetFloat(SpeedParameter, speed);
            animator.SetFloat(MoveDirectionParameter, moveDirection.x);
            animator.SetFloat("MoveX", moveDirection.x);
            animator.SetFloat("MoveY", moveDirection.y);
        }

        public void ApplyRemoteAnimationState(RemoteEnemyAnimationState state, float deltaTime)
        {
            if (IsDead) return;
            RemoteAnimationState = state;
            ApplyMovement(state.Speed, state.MoveDirection);
            animator.SetBool(IsInCoverParameter, state.IsInCover);
            animator.SetBool(IsPeekingParameter, state.IsPeeking);
            EnsurePlayableGraph();
            playablesController?.Update(deltaTime);
        }

        public void PlayFire()
        {
            if (IsDead || hitRemainingSeconds > 0f) return;
            EnsureAnimator();
            LastConfirmedAction = EnemyActionKind.Fire;
            EnsurePlayableGraph();
            if (playablesController == null) animator.SetTrigger(FireParameter);
            else playablesController.TrySetTrigger("Fire");
        }

        public void PlayHit()
        {
            if (IsDead) return;
            hitRemainingSeconds = 0.8f;
            EnsureAnimator();
            LastConfirmedAction = EnemyActionKind.Hit;
            EnsurePlayableGraph();
            if (playablesController == null) animator.SetTrigger(HitParameter);
            else playablesController.TrySetTrigger("Hit");
        }

        public void PlayDeath()
        {
            if (IsDead) return;
            PlayHit();
            IsDead = true;
            LastConfirmedAction = EnemyActionKind.Death;
            deathElapsedSeconds = 0f;
            if (visualRoot != null && visualRoot != transform)
            {
                deathStartPosition = visualRoot.localPosition;
                deathStartRotation = visualRoot.localRotation;
                deathFallRotation = ChooseDeathFallRotation();
            }
        }

        public void TickPresentation(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            hitRemainingSeconds = Mathf.Max(0f, hitRemainingSeconds - deltaTime);
            playablesController?.Update(deltaTime);
            if (!IsDead) return;
            deathElapsedSeconds += deltaTime;
            if (deathElapsedSeconds < 0.2f || visualRoot == null || visualRoot == transform) return;
            // Freeze the hit pose and topple only the visual child. The network root
            // stays fixed and cannot be displaced by this presentation terminal.
            animator.speed = 0f;
            if (!deathPoseFrozen)
            {
                var graph = animator.playableGraph;
                if (graph.IsValid())
                {
                    for (int index = 0; index < graph.GetRootPlayableCount(); index++)
                        graph.GetRootPlayable(index).SetSpeed(0d);
                    // Commit the frozen pose before baking; normal Animator
                    // evaluation happens later than the network presentation tick.
                    graph.Evaluate(0f);
                }
                deathEndPosition = calculateGroundedDeathPosition();
                deathPoseFrozen = true;
            }
            float phase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((deathElapsedSeconds - 0.2f) / 0.65f));
            visualRoot.localRotation = Quaternion.Slerp(deathStartRotation, deathFallRotation, phase);
            visualRoot.localPosition = Vector3.Lerp(deathStartPosition, deathEndPosition, phase);
        }

        private Vector3 calculateGroundedDeathPosition()
        {
            float lowest = float.PositiveInfinity;
            Matrix4x4 finalVisualToWorld = visualRoot.parent.localToWorldMatrix *
                Matrix4x4.TRS(deathStartPosition, deathFallRotation, visualRoot.localScale);
            foreach (var renderer in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Bake in the evaluated pose, without temporarily moving its
                // animated hierarchy. Transform the vertices mathematically to
                // the final fall pose instead of disturbing Animator bindings.
                Matrix4x4 rendererToFinalWorld = finalVisualToWorld *
                    visualRoot.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var mesh = new Mesh();
                try
                {
                    renderer.BakeMesh(mesh);
                    foreach (Vector3 vertex in mesh.vertices)
                        lowest = Mathf.Min(lowest, rendererToFinalWorld.MultiplyPoint3x4(vertex).y);
                }
                finally
                {
                    if (Application.isPlaying) Destroy(mesh);
                    else DestroyImmediate(mesh);
                }
            }
            return float.IsPositiveInfinity(lowest) ? deathStartPosition + Vector3.up * .2f :
                deathStartPosition + visualRoot.parent.InverseTransformVector(Vector3.up * (transform.position.y - lowest));
        }

        private Quaternion ChooseDeathFallRotation()
        {
            // This query only selects the visual terminal's free side. It does
            // not move the network root or make a gameplay/authority decision.
            const float bodyLength = 1.8f;
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            Vector3 bestDirection = transform.right;
            float bestClearance = -1f;
            for (int index = 0; index < 8; index++)
            {
                Vector3 direction = Quaternion.AngleAxis(index * 45f, Vector3.up) * transform.right;
                float clearance = bodyLength;
                foreach (RaycastHit hit in Physics.SphereCastAll(origin, 0.3f, direction, bodyLength,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                    clearance = Mathf.Min(clearance, hit.distance);
                }
                if (clearance <= bestClearance) continue;
                bestClearance = clearance;
                bestDirection = direction;
                if (clearance >= bodyLength) break;
            }
            Vector3 localDirection = transform.InverseTransformDirection(bestDirection);
            return Quaternion.AngleAxis(85f, Vector3.Cross(Vector3.up, localDirection)) * deathStartRotation;
        }

        private void EnsureAnimator()
        {
            if (animator == null) throw new InvalidOperationException("EnemyPresentation requires an Animator.");
        }

        private void EnsurePlayableGraph()
        {
            if (playablesController != null && playablesController.IsValid()) return;
            playbackPawn ??= new Pawn(gameObject);
            playablesController?.Dispose();
            playablesController = new CharacterPlayablesController(
                playbackPawn,
                animator);
            if (!playablesController.TryRebuild())
            {
                playablesController.Dispose();
                playablesController = null;
            }
        }

        private void OnDestroy()
        {
            playablesController?.Dispose();
            playablesController = null;
        }
    }
}
