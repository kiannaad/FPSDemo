namespace CGame.Network
{
    public interface IDedicatedServerBootstrapConfiguration
    {
        CharacterPhysicsSettings CharacterPhysicsSettings { get; }
        LevelDefinition LevelDefinition { get; }
        PawnDefinition PlayerPawnDefinition { get; }
        EnemyRosterDefinition EnemyRosterDefinition { get; }
        EnemyArchetypeCombatCatalog EnemyArchetypeCombatCatalog { get; }
        DedicatedTargetSpawnDefinition DedicatedTargetSpawnDefinition { get; }
    }
}
