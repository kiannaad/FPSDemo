using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Animation;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "PawnData", menuName = "CGame/Gameplay/Pawn Data")]
    public sealed class PawnData : ScriptableObject
    {
        [SerializeField] private AbilityDefinition[] baseAbilities = Array.Empty<AbilityDefinition>();
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private CharacterAnimationConfig animationConfig;
        [SerializeField] private Mesh firstPersonMesh;
        [SerializeField] private Material firstPersonMaterial;
        [NonSerialized] private AbilitySet[] runtimeBaseAbilitySets;

        public IReadOnlyList<AbilitySet> ResolveBaseAbilitySets()
        {
            if (runtimeBaseAbilitySets != null)
            {
                return runtimeBaseAbilitySets;
            }

            return new[] { new AbilitySet(baseAbilities) };
        }

        public GameObject PawnPrefab => pawnPrefab;

        public CharacterAnimationConfig AnimationConfig => animationConfig;

        public Mesh FirstPersonMesh => firstPersonMesh;

        public Material FirstPersonMaterial => firstPersonMaterial;

        public static PawnData CreateRuntime(params AbilitySet[] baseAbilitySets)
        {
            return CreateRuntime(null, baseAbilitySets);
        }

        public static PawnData CreateRuntime(GameObject pawnPrefab, params AbilitySet[] baseAbilitySets)
        {
            PawnData data = CreateInstance<PawnData>();
            data.pawnPrefab = pawnPrefab;
            data.runtimeBaseAbilitySets = baseAbilitySets == null
                ? Array.Empty<AbilitySet>()
                : (AbilitySet[])baseAbilitySets.Clone();
            if (Array.Exists(data.runtimeBaseAbilitySets, abilitySet => abilitySet == null))
            {
                DestroyImmediate(data);
                throw new ArgumentException("PawnData cannot contain a null AbilitySet.", nameof(baseAbilitySets));
            }

            return data;
        }
    }
}
