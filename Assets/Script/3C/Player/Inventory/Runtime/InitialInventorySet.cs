using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "InitialInventorySet", menuName = "CGame/Gameplay/Initial Inventory Set")]
    public sealed class InitialInventorySet : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] itemDefinitions = Array.Empty<ItemDefinition>();
        [SerializeField] private int selectedSlot;
        [NonSerialized] private ItemDefinition[] runtimeDefinitions;

        public IReadOnlyList<ItemDefinition> ItemDefinitions => runtimeDefinitions ?? itemDefinitions;

        public int SelectedSlot => selectedSlot;

        public static InitialInventorySet CreateRuntime(int selectedSlot, params ItemDefinition[] definitions)
        {
            InitialInventorySet set = CreateInstance<InitialInventorySet>();
            set.selectedSlot = selectedSlot;
            set.runtimeDefinitions = definitions == null
                ? Array.Empty<ItemDefinition>()
                : (ItemDefinition[])definitions.Clone();
            return set;
        }
    }
}
