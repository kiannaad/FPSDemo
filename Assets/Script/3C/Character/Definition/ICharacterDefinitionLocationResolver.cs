namespace CGame
{
    public interface ICharacterDefinitionLocationResolver
    {
        bool TryResolveLocation(
            CharacterDefinitionId definitionId,
            out string location);
    }
}
