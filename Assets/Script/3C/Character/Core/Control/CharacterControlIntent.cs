using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 控制器提交给 Pawn 的角色控制意图。
    /// 连续移动与瞬时跳跃在 Pawn 内部按各自的时序规则缓冲。
    /// </summary>
    public readonly struct CharacterControlIntent
    {
        public CharacterControlIntent(Vector3 movementInput, bool jumpRequested)
        {
            MovementInput = Vector3.ClampMagnitude(movementInput, 1f);
            JumpRequested = jumpRequested;
        }

        public Vector3 MovementInput { get; }
        public bool JumpRequested { get; }
    }
}
