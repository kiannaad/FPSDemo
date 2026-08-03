using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Animation;
using CGame.Animation;

namespace CGame
{
    public sealed class EquipmentInstance : IDisposable
    {
        private readonly List<AbilityGrantReceipt> grantReceipts =
            new List<AbilityGrantReceipt>();
        private IEquipmentDefinitionLease definitionLease;

        internal EquipmentInstance(
            EquipmentSlot ownerSlot,
            AbilitySystemComponent abilitySystem,
            WeaponRuntime weaponRuntime,
            IAbilityAnimationPlayer animationPlayer,
            IEquipmentDefinitionLease definitionLease,
            IEnumerable<AbilitySet> abilitySets)
        {
            OwnerSlot = ownerSlot ?? throw new ArgumentNullException(nameof(ownerSlot));
            AbilitySystem = abilitySystem ?? throw new ArgumentNullException(nameof(abilitySystem));
            WeaponRuntime = weaponRuntime ?? throw new ArgumentNullException(nameof(weaponRuntime));
            AnimationPlayer = animationPlayer;
            this.definitionLease = definitionLease ?? throw new ArgumentNullException(nameof(definitionLease));
            if (AbilitySystem.IsDisposed || !definitionLease.IsValid)
            {
                throw new InvalidOperationException("A live ASC and valid equipment definition lease are required.");
            }

            Definition = definitionLease.Definition;
            WeaponId = definitionLease.WeaponId;
            var uniqueSets = new HashSet<AbilitySet>();
            try
            {
                if (abilitySets != null)
                {
                    foreach (AbilitySet abilitySet in abilitySets)
                    {
                        if (abilitySet == null)
                        {
                            throw new ArgumentException("Equipment ability sets cannot contain null.", nameof(abilitySets));
                        }

                        if (uniqueSets.Add(abilitySet))
                        {
                            grantReceipts.Add(
                                AbilitySystem.GiveAbilitySet(
                                    abilitySet,
                                    this));
                        }
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public AbilitySystemComponent AbilitySystem { get; }
        public EquipmentSlot OwnerSlot { get; }
        public WeaponRuntime WeaponRuntime { get; }
        public IAbilityAnimationPlayer AnimationPlayer { get; }
        public WeaponAnimationDefinition Definition { get; }
        public WeaponId WeaponId { get; }
        public object SourceObject => this;
        public IReadOnlyList<AbilityGrantReceipt> GrantReceipts => grantReceipts;
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            for (int index = grantReceipts.Count - 1; index >= 0; index--)
            {
                grantReceipts[index].Revoke();
            }

            grantReceipts.Clear();
            IEquipmentDefinitionLease currentLease = definitionLease;
            definitionLease = null;
            currentLease?.Dispose();
        }
    }
}
