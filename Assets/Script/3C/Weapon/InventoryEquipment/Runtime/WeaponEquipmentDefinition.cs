using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame.InventoryEquipment
{
    [CreateAssetMenu(fileName = "WeaponEquipmentDefinition", menuName = "CGame/Equipment/Weapon")]
    public sealed class WeaponEquipmentDefinition : EquipmentDefinition
    {
        [SerializeField] private int magazineCapacity = 30;
        [SerializeField] private int loadTicks = 1;
        [SerializeField] private bool simulateLoadFailure;
        [NonSerialized] private AbilitySet[] runtimeAbilitySets;

        public int MagazineCapacity => magazineCapacity;

        public override int LoadTicks => Math.Max(0, loadTicks);

        public override bool SimulateLoadFailure => simulateLoadFailure;

        public IReadOnlyList<AbilitySet> AbilitySets => runtimeAbilitySets ?? Array.Empty<AbilitySet>();

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
    }
}
