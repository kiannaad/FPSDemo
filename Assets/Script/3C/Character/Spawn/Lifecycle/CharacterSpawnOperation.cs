namespace CGame
{
    public sealed class CharacterSpawnOperation
    {
        internal CharacterSpawnOperation(CharacterSpawnRequest request)
        {
            Request = request;
            State = CharacterSpawnState.Requested;
        }

        public CharacterSpawnRequest Request { get; }
        public CharacterSpawnState State { get; internal set; }
        public CharacterRuntimeId RuntimeId { get; internal set; }
        public CharacterSpawnResult Result { get; internal set; }
        public CharacterSpawnError Error { get; internal set; }
        public bool IsComplete => State == CharacterSpawnState.CharacterReady
            || State == CharacterSpawnState.Cancelled
            || State == CharacterSpawnState.Released
            || State == CharacterSpawnState.Failed;

        internal static CharacterSpawnOperation CreateDuplicate(CharacterSpawnRequest request)
        {
            return new CharacterSpawnOperation(request)
            {
                State = CharacterSpawnState.Failed,
                Error = CharacterSpawnError.DuplicateRequestId,
            };
        }
    }
}
