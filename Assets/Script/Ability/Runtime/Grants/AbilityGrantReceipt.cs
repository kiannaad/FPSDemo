using System.Collections.Generic;

namespace CGame.Ability
{
    public sealed class AbilityGrantReceipt
    {
        private readonly AbilitySystemComponent abilitySystem;
        private readonly AbilitySpecHandle[] specHandles;
        private readonly GameplayTagGrantHandle[] tagGrantHandles;

        internal AbilityGrantReceipt(
            AbilitySystemComponent abilitySystem,
            AbilitySet abilitySet,
            object sourceObject,
            IReadOnlyList<AbilitySpecHandle> specHandles,
            IReadOnlyList<GameplayTagGrantHandle> tagGrantHandles)
        {
            this.abilitySystem = abilitySystem;
            AbilitySet = abilitySet;
            SourceObject = sourceObject;
            this.specHandles = new List<AbilitySpecHandle>(specHandles).ToArray();
            this.tagGrantHandles = new List<GameplayTagGrantHandle>(tagGrantHandles).ToArray();
            IsActive = true;
        }

        public AbilitySet AbilitySet { get; }
        public object SourceObject { get; }
        public IReadOnlyList<AbilitySpecHandle> SpecHandles => specHandles;
        public bool IsActive { get; private set; }

        public bool Revoke()
        {
            return IsActive && abilitySystem.RevokeAbilitySet(this);
        }

        internal IReadOnlyList<GameplayTagGrantHandle> TagGrantHandles => tagGrantHandles;

        internal void MarkRevoked()
        {
            IsActive = false;
        }
    }
}
