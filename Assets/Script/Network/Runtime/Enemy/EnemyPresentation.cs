using System;
using CGame.Animation;
using UnityEngine;

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
        private Pawn playbackPawn;
        private CharacterPlayablesController playablesController;

        public Animator Animator => animator;
        public RemoteEnemyAnimationState RemoteAnimationState { get; private set; }
        public EnemyActionKind LastConfirmedAction { get; private set; }
        public bool HasPlayableGraph => playablesController != null && playablesController.IsValid();

        public void Configure(Animator value)
        {
            animator = value ?? throw new ArgumentNullException(nameof(value));
            animator.applyRootMotion = false;
        }

        public void ApplyMovement(float speed, Vector2 moveDirection)
        {
            EnsureAnimator();
            animator.SetFloat(SpeedParameter, speed);
            animator.SetFloat(MoveDirectionParameter, moveDirection.x);
        }

        public void ApplyRemoteAnimationState(RemoteEnemyAnimationState state, float deltaTime)
        {
            RemoteAnimationState = state;
            ApplyMovement(state.Speed, state.MoveDirection);
            animator.SetBool(IsInCoverParameter, state.IsInCover);
            animator.SetBool(IsPeekingParameter, state.IsPeeking);
            EnsurePlayableGraph();
            playablesController?.Update(deltaTime);
        }

        public void PlayFire()
        {
            EnsureAnimator();
            LastConfirmedAction = EnemyActionKind.Fire;
            animator.SetTrigger(FireParameter);
        }

        public void PlayHit()
        {
            EnsureAnimator();
            LastConfirmedAction = EnemyActionKind.Hit;
            animator.SetTrigger(HitParameter);
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
                animator,
                synchronizeAnimatorParameters: false);
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
