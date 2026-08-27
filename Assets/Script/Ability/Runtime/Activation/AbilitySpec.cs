using CGame.GameplayTags;

namespace CGame.Ability
{
    public sealed class AbilitySpec
    {
        internal AbilitySpec(AbilitySpecHandle handle, AbilityDefinition definition, object sourceObject, GameplayTag inputTag = default)
        {
            Handle = handle;
            Definition = definition;
            SourceObject = sourceObject;
            InputTag = inputTag;
        }

        public AbilitySpecHandle Handle { get; }
        public AbilityDefinition Definition { get; }
        public object SourceObject { get; }
        public GameplayTag InputTag { get; }
        public bool InputPressed { get; internal set; }
        public AbilityInstance PrimaryInstance { get; internal set; }
        public int ActiveInstanceCount { get; internal set; }
    }
}
