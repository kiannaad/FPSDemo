using UnityEngine;

namespace CGame.Network
{
    public sealed class NetworkLoopAnimationPhaseSynchronizer
    {
        private bool synchronizedForCurrentIdle;

        public bool Synchronize(Animator animator, long serverTick, bool isMoving)
        {
            if (isMoving)
            {
                synchronizedForCurrentIdle = false;
                return false;
            }

            if (synchronizedForCurrentIdle || animator == null || !animator.isActiveAndEnabled || serverTick < 0)
                return false;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.loop || state.length <= Mathf.Epsilon || state.fullPathHash == 0) return false;
            animator.Play(state.fullPathHash, 0, CalculateNormalizedPhase(serverTick, state.length));
            synchronizedForCurrentIdle = true;
            return true;
        }

        public static float CalculateNormalizedPhase(long serverTick, float clipLengthSeconds)
        {
            if (serverTick < 0 || clipLengthSeconds <= Mathf.Epsilon) return 0f;
            float cycles = serverTick / (NetworkTickClock.TicksPerSecond * clipLengthSeconds);
            return cycles - Mathf.Floor(cycles);
        }
    }
}
