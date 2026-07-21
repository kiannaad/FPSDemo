namespace CGame
{
    public readonly struct CharacterSpawnRequest
    {
        public CharacterSpawnRequest(CharacterSpawnRequestId requestId, CharacterDefinitionId definitionId, CharacterControlKind controlKind, CharacterSpawnPlacement placement, InputType inputType, string displayName)
        {
            RequestId = requestId;
            DefinitionId = definitionId;
            ControlKind = controlKind;
            Placement = placement;
            InputType = inputType;
            DisplayName = displayName;
        }

        public CharacterSpawnRequestId RequestId { get; }
        public CharacterDefinitionId DefinitionId { get; }
        public CharacterControlKind ControlKind { get; }
        public CharacterSpawnPlacement Placement { get; }
        public InputType InputType { get; }
        public string DisplayName { get; }
    }
}
