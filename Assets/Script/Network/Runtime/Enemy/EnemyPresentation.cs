using System;
using UnityEngine;

namespace CGame.Network
{
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveDirectionParameter = Animator.StringToHash("MoveDirection");
        private static readonly int FireParameter = Animator.StringToHash("Fire");
        private static readonly int HitParameter = Animator.StringToHash("Hit");

        [SerializeField] private Animator animator;

        public Animator Animator => animator;

        public void Configure(Animator value)
        {
            animator = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void ApplyMovement(float speed, Vector2 moveDirection)
        {
            EnsureAnimator();
            animator.SetFloat(SpeedParameter, speed);
            animator.SetFloat(MoveDirectionParameter, moveDirection.x);
        }

        public void PlayFire()
        {
            EnsureAnimator();
            animator.SetTrigger(FireParameter);
        }

        public void PlayHit()
        {
            EnsureAnimator();
            animator.SetTrigger(HitParameter);
        }

        private void EnsureAnimator()
        {
            if (animator == null) throw new InvalidOperationException("EnemyPresentation requires an Animator.");
        }
    }
}
