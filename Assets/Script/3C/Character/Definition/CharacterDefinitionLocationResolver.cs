using System.Collections.Generic;

namespace CGame
{
    public sealed class CharacterDefinitionLocationResolver :
        ICharacterDefinitionLocationResolver
    {
        private static readonly IReadOnlyDictionary<
            CharacterDefinitionId,
            string> Locations =
                new Dictionary<CharacterDefinitionId, string>
                {
                    {
                        new CharacterDefinitionId("local-player"),
                        "CharacterDefinition"
                    },
                };

        public bool TryResolveLocation(
            CharacterDefinitionId definitionId,
            out string location)
        {
            if (!definitionId.IsValid)
            {
                location = string.Empty;
                return false;
            }

            return Locations.TryGetValue(definitionId, out location);
        }
    }
}
