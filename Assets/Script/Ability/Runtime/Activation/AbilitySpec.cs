namespace CGame.Ability
{
    public sealed class AbilitySpec
    {
        internal AbilitySpec(AbilitySpecHandle handle, AbilityDefinition definition, object sourceObject)
        {
            Handle = handle;
            Definition = definition;
            SourceObject = sourceObject;
        }

        public AbilitySpecHandle Handle { get; }
        public AbilityDefinition Definition { get; }
        public object SourceObject { get; }
        public AbilityInstance PrimaryInstance { get; internal set; }
        public int ActiveInstanceCount { get; internal set; }
    }
}
