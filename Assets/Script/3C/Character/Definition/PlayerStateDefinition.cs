using System;
using System.Collections.Generic;
using CGame.Ability;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "PlayerStateDefinition", menuName = "CGame/Gameplay/Player State Definition")]
    public sealed class PlayerStateDefinition : ScriptableObject
    {
        [SerializeField] private PawnData pawnData;
        [SerializeField] private InputProfile inputProfile;
        [SerializeField] private AbilityDefinition[] baseAbilities = Array.Empty<AbilityDefinition>();
        [SerializeField] private InitialInventorySet initialInventorySet;
        [NonSerialized] private AbilitySet[] runtimeBaseAbilitySets;

        public PawnData PawnData => pawnData;
        public InputProfile InputProfile => inputProfile;
        public InitialInventorySet InitialInventorySet => initialInventorySet;

        public IReadOnlyList<AbilitySet> ResolveBaseAbilitySets()
        {
            return runtimeBaseAbilitySets ?? new[] { new AbilitySet(baseAbilities) };
        }

        public void Configure(
            PawnData pawn,
            InputProfile input,
            InitialInventorySet inventory,
            params AbilitySet[] abilitySets)
        {
            pawnData = pawn ?? throw new ArgumentNullException(nameof(pawn));
            inputProfile = input;
            initialInventorySet = inventory;
            runtimeBaseAbilitySets = abilitySets == null
                ? Array.Empty<AbilitySet>()
                : (AbilitySet[])abilitySets.Clone();
            if (Array.Exists(runtimeBaseAbilitySets, abilitySet => abilitySet == null))
            {
                throw new ArgumentException("PlayerStateDefinition cannot contain a null AbilitySet.", nameof(abilitySets));
            }
        }

        public void ConfigureSerialized(
            PawnData pawn,
            InputProfile input,
            InitialInventorySet inventory,
            params AbilityDefinition[] abilities)
        {
            pawnData = pawn ?? throw new ArgumentNullException(nameof(pawn));
            inputProfile = input;
            initialInventorySet = inventory;
            baseAbilities = abilities == null
                ? Array.Empty<AbilityDefinition>()
                : (AbilityDefinition[])abilities.Clone();
            if (Array.Exists(baseAbilities, ability => ability == null))
            {
                throw new ArgumentException("PlayerStateDefinition cannot contain a null AbilityDefinition.", nameof(abilities));
            }
            runtimeBaseAbilitySets = null;
        }

        public void SetInputProfile(InputProfile input)
        {
            inputProfile = input;
        }

        public void SetInitialInventory(InitialInventorySet inventory)
        {
            initialInventorySet = inventory;
        }

        public void ValidateRequiredReferences()
        {
            if (pawnData == null)
            {
                throw new InvalidOperationException("PlayerStateDefinition requires one PawnData.");
            }

            if (pawnData.ControllerDefinition == null || pawnData.PawnFactoryDefinition == null || pawnData.PawnPrefab == null)
            {
                throw new InvalidOperationException("PawnData requires ControllerDefinition, PawnFactoryDefinition and PawnPrefab.");
            }
        }
    }
}
