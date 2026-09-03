namespace CGame.Network
{
    public interface IDedicatedServerBootstrapConfiguration
    {
        CharacterPhysicsSettings CharacterPhysicsSettings { get; }
        LevelDefinition LevelDefinition { get; }
        PawnDefinition PlayerPawnDefinition { get; }
        DedicatedTargetSpawnDefinition DedicatedTargetSpawnDefinition { get; }
    }
}
