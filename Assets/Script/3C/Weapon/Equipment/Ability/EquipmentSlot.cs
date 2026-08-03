using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Animation;

namespace CGame
{
    public sealed class EquipmentSlot : IDisposable
    {
        private readonly AbilitySystemComponent abilitySystem;
        private readonly WeaponRuntime weaponRuntime;
        private readonly IAbilityAnimationPlayer animationPlayer;
        private readonly IEquipmentDefinitionLoader definitionLoader;
        private readonly IWeaponSwitchPresentation switchPresentation;

        public EquipmentSlot(
            AbilitySystemComponent abilitySystem,
            WeaponRuntime weaponRuntime)
            : this(abilitySystem, weaponRuntime, null)
        {
        }

        public EquipmentSlot(
            AbilitySystemComponent abilitySystem,
            WeaponRuntime weaponRuntime,
            IAbilityAnimationPlayer animationPlayer)
            : this(
                abilitySystem,
                weaponRuntime,
                animationPlayer,
                null,
                animationPlayer as IWeaponSwitchPresentation)
        {
        }

        public EquipmentSlot(
            AbilitySystemComponent abilitySystem,
            WeaponRuntime weaponRuntime,
            IAbilityAnimationPlayer animationPlayer,
            IEquipmentDefinitionLoader definitionLoader,
            IWeaponSwitchPresentation switchPresentation)
        {
            this.abilitySystem = abilitySystem ?? throw new ArgumentNullException(nameof(abilitySystem));
            this.weaponRuntime = weaponRuntime ?? throw new ArgumentNullException(nameof(weaponRuntime));
            this.animationPlayer = animationPlayer;
            this.definitionLoader = definitionLoader;
            this.switchPresentation = switchPresentation;
        }

        public EquipmentInstance Current { get; private set; }
        public bool IsDisposed { get; private set; }
        internal IEquipmentDefinitionLoader DefinitionLoader => definitionLoader;
        internal IWeaponSwitchPresentation SwitchPresentation => switchPresentation;

        public EquipmentEquipResult TryEquip(
            IEquipmentDefinitionLease definitionLease,
            IEnumerable<AbilitySet> abilitySets,
            out EquipmentInstance equipment)
        {
            equipment = null;
            if (definitionLease == null)
            {
                return IsDisposed
                    ? EquipmentEquipResult.SlotDisposed
                    : EquipmentEquipResult.InvalidTarget;
            }

            if (IsDisposed)
            {
                definitionLease.Dispose();
                return EquipmentEquipResult.SlotDisposed;
            }

            if (!definitionLease.IsValid)
            {
                definitionLease.Dispose();
                return EquipmentEquipResult.InvalidTarget;
            }

            if (Current != null && Current.WeaponId == definitionLease.WeaponId)
            {
                definitionLease.Dispose();
                return EquipmentEquipResult.AlreadyEquipped;
            }

            EquipmentInstance candidate;
            try
            {
                candidate = new EquipmentInstance(
                    this,
                    abilitySystem,
                    weaponRuntime,
                    animationPlayer,
                    definitionLease,
                    abilitySets);
            }
            catch
            {
                definitionLease.Dispose();
                return EquipmentEquipResult.GrantFailed;
            }

            EquipmentInstance previous = Current;
            Current = candidate;
            equipment = candidate;
            previous?.Dispose();
            return EquipmentEquipResult.Equipped;
        }

        internal bool TryPrepareReplacement(
            IEquipmentDefinitionLease definitionLease,
            IEnumerable<AbilitySet> abilitySets,
            out EquipmentReplacement replacement)
        {
            replacement = null;
            if (IsDisposed
                || definitionLease == null
                || !definitionLease.IsValid
                || Current == null
                || Current.WeaponId == definitionLease.WeaponId)
            {
                definitionLease?.Dispose();
                return false;
            }

            EquipmentInstance candidate;
            try
            {
                candidate = new EquipmentInstance(
                    this,
                    abilitySystem,
                    weaponRuntime,
                    animationPlayer,
                    definitionLease,
                    abilitySets);
            }
            catch
            {
                definitionLease.Dispose();
                return false;
            }

            replacement = new EquipmentReplacement(
                this,
                Current,
                candidate);
            return true;
        }

        internal bool TryCommitReplacement(
            EquipmentReplacement replacement,
            EquipmentInstance expectedCurrent,
            EquipmentInstance candidate,
            out EquipmentInstance previous)
        {
            previous = null;
            if (IsDisposed
                || replacement == null
                || candidate == null
                || !ReferenceEquals(Current, expectedCurrent))
            {
                return false;
            }

            previous = Current;
            Current = candidate;
            return true;
        }

        public bool Unequip()
        {
            if (Current == null)
            {
                return false;
            }

            EquipmentInstance previous = Current;
            Current = null;
            previous.Dispose();
            return true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            Unequip();
        }
    }
}
