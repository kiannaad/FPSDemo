namespace CGame.Network
{
    public interface IDedicatedServerBootstrapConfiguration
    {
        CharacterPhysicsSettings CharacterPhysicsSettings { get; }
        LevelDefinition LevelDefinition { get; }
        PawnDefinition PlayerPawnDefinition { get; }
        EnemyRosterDefinition EnemyRosterDefinition { get; }
        EnemyArchetypeCombatCatalog EnemyArchetypeCombatCatalog { get; }
        CoverPointCatalog CoverPointCatalog { get; }
        DedicatedTargetSpawnDefinition DedicatedTargetSpawnDefinition { get; }
    }
}
