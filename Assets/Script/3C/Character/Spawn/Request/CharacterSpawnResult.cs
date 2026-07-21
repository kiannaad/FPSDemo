namespace CGame
{
    public readonly struct CharacterSpawnResult
    {
        public CharacterSpawnResult(CharacterRuntimeId runtimeId)
        {
            RuntimeId = runtimeId;
        }

        public CharacterRuntimeId RuntimeId { get; }
    }
}
