namespace CGame
{
    public sealed class EnemyPlayerState
    {
        public EnemyPlayerState(EnemyPlayerStateDefinition definition)
        {
            Definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        }

        public EnemyPlayerStateDefinition Definition { get; }
    }
}
