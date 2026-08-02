namespace CGame
{
    /// <summary>
    /// 向角色物理层提供每个模拟步的控制命令。
    /// </summary>
    public interface ICharacterMovementCommandSource
    {
        CharacterMovementCommand ConsumeMovementCommand();
    }
}
