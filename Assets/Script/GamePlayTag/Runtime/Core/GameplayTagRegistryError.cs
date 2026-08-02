namespace CGame.GameplayTags
{
    public sealed class GameplayTagRegistryError
    {
        public GameplayTagRegistryError(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"{Code}: {Message}";
        }
    }
}
