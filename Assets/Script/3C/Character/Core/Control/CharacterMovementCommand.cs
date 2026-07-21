using UnityEngine;

namespace CGame
{
    /// <summary>
    /// 单个物理步消费的角色移动命令。
    /// </summary>
    public readonly struct CharacterMovementCommand
    {
        public CharacterMovementCommand(Vector3 movementInput, bool jumpRequested)
        {
            MovementInput = movementInput;
            JumpRequested = jumpRequested;
        }

        public Vector3 MovementInput { get; }
        public bool JumpRequested { get; }
    }
}
