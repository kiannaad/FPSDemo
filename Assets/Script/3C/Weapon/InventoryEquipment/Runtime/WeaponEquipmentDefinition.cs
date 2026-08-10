using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [CreateAssetMenu(fileName = "WeaponEquipmentDefinition", menuName = "CGame/Equipment/Weapon")]
    public sealed class WeaponEquipmentDefinition : EquipmentDefinition
    {
        [SerializeField] private int magazineCapacity = 30;
        [SerializeField] private int loadTicks = 1;
        [SerializeField] private bool simulateLoadFailure;
        [SerializeField] private GameplayTag fireInputTag;
        [SerializeField] private GameplayTag reloadInputTag;
        [SerializeField] private GameplayTag meleeInputTag;
        [NonSerialized] private AbilitySet[] runtimeAbilitySets;

        public int MagazineCapacity => magazineCapacity;

        public override int LoadTicks => Math.Max(0, loadTicks);

        public override bool SimulateLoadFailure => simulateLoadFailure;

        public IReadOnlyList<AbilitySet> AbilitySets => runtimeAbilitySets ?? CreateConfiguredAbilitySets();

        public override EquipmentInstance CreateInstance(EquipmentCreateContext context)
        {
            return new WeaponInstance(context, magazineCapacity, AbilitySets);
        }

        public static WeaponEquipmentDefinition CreateRuntime(
            int magazineCapacity,
            int loadTicks = 1,
            bool simulateLoadFailure = false,
            params AbilitySet[] abilitySets)
        {
            WeaponEquipmentDefinition definition = CreateInstance<WeaponEquipmentDefinition>();
            definition.magazineCapacity = Math.Max(1, magazineCapacity);
            definition.loadTicks = Math.Max(0, loadTicks);
            definition.simulateLoadFailure = simulateLoadFailure;
            definition.runtimeAbilitySets = abilitySets == null
                ? Array.Empty<AbilitySet>()
                : (AbilitySet[])abilitySets.Clone();
            return definition;
        }

        public void SetInputTags(GameplayTag fire, GameplayTag reload, GameplayTag melee)
        {
            fireInputTag = fire;
            reloadInputTag = reload;
            meleeInputTag = melee;
        }

        private AbilitySet[] CreateConfiguredAbilitySets()
        {
            var grants = new List<AbilityGrantDefinition>();
            AddInputGrant(grants, fireInputTag, WeaponInputAction.Fire);
            AddInputGrant(grants, reloadInputTag, WeaponInputAction.Reload);
            AddInputGrant(grants, meleeInputTag, WeaponInputAction.Melee);
            return grants.Count == 0 ? Array.Empty<AbilitySet>() : new[] { new AbilitySet(grants) };
        }

        private static void AddInputGrant(List<AbilityGrantDefinition> grants, GameplayTag inputTag, WeaponInputAction action)
        {
            if (!inputTag.IsEmpty)
            {
                grants.Add(new AbilityGrantDefinition(new WeaponInputAbilityDefinition(inputTag, action), inputTag));
            }
        }

        private enum WeaponInputAction { Fire, Reload, Melee }

        private sealed class WeaponInputAbilityDefinition : AbilityDefinition
        {
            private readonly WeaponInputAction action;

            public WeaponInputAbilityDefinition(GameplayTag abilityTag, WeaponInputAction action)
                : base(abilityTag)
            {
                this.action = action;
            }

            protected override AbilityInstance CreateInstance() => new WeaponInputAbilityInstance(action);
        }

        private sealed class WeaponInputAbilityInstance : AbilityInstance
        {
            private readonly WeaponInputAction action;

            public WeaponInputAbilityInstance(WeaponInputAction action)
            {
                this.action = action;
            }

            protected override void OnActivate()
            {
                WeaponInstance weapon = ActivationContext.Spec.SourceObject as WeaponInstance;
                if (weapon != null)
                {
                    switch (action)
                    {
                        case WeaponInputAction.Fire:
                            if (weapon.Fire())
                            {
                                Debug.Log($"[InputTagAbility] Fire succeeded. FireCount={weapon.FireCount}, Ammo={weapon.Item.MagazineAmmo}.");
                            }
                            break;
                        case WeaponInputAction.Reload:
                            int loaded = weapon.Reload();
                            Debug.Log($"[InputTagAbility] Reload processed. Loaded={loaded}, Ammo={weapon.Item.MagazineAmmo}.");
                            break;
                        case WeaponInputAction.Melee:
                            if (weapon.Melee())
                            {
                                Debug.Log($"[InputTagAbility] Melee succeeded. MeleeCount={weapon.MeleeCount}.");
                            }
                            break;
                    }
                }

                EndAbility(AbilityEndReason.Completed);
            }
        }
    }
}
