using System;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Animation;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "PawnDefinition", menuName = "CGame/Gameplay/Pawn Definition")]
    public class PawnDefinition : ScriptableObject
    {
        [SerializeField] private AbilityDefinition[] baseAbilities = Array.Empty<AbilityDefinition>();
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private CharacterAnimationConfig animationConfig;
        [SerializeField] private KRig rig;
        [SerializeField] private Mesh firstPersonMesh;
        [SerializeField] private Material firstPersonMaterial;
        [SerializeField] private bool requireCamera;
        [SerializeField] private InputProfile inputProfile;
        [NonSerialized] private AbilitySet[] runtimeBaseAbilitySets;

        public IReadOnlyList<AbilitySet> ResolveBaseAbilitySets()
        {
            return runtimeBaseAbilitySets ?? new[] { new AbilitySet(baseAbilities) };
        }

        public GameObject PawnPrefab => pawnPrefab;

        public CharacterAnimationConfig AnimationConfig => animationConfig;

        public KRig Rig => rig;

        public Mesh FirstPersonMesh => firstPersonMesh;

        public Material FirstPersonMaterial => firstPersonMaterial;

        public bool RequireCamera => requireCamera;
        public InputProfile InputProfile => inputProfile;

        public void SetInputProfile(InputProfile inputProfile)
        {
            this.inputProfile = inputProfile;
        }

        public static PawnDefinition CreateRuntime(
            GameObject pawnPrefab,
            params AbilitySet[] baseAbilitySets)
        {
            return CreateRuntime(pawnPrefab, false, baseAbilitySets);
        }

        public static PawnDefinition CreateRuntime(
            GameObject pawnPrefab,
            bool requireCamera,
            params AbilitySet[] baseAbilitySets)
        {
            PawnDefinition definition = CreateInstance<PawnDefinition>();
            definition.ConfigureRuntime(pawnPrefab, baseAbilitySets);
            definition.requireCamera = requireCamera;
            return definition;
        }

        protected void ConfigureRuntime(GameObject prefab, AbilitySet[] abilitySets)
        {
            pawnPrefab = prefab;
            runtimeBaseAbilitySets = abilitySets == null
                ? Array.Empty<AbilitySet>()
                : (AbilitySet[])abilitySets.Clone();
            if (Array.Exists(runtimeBaseAbilitySets, abilitySet => abilitySet == null))
            {
                DestroyImmediate(this);
                throw new ArgumentException(
                    "PawnDefinition cannot contain a null AbilitySet.",
                    nameof(abilitySets));
            }
        }
    }
}
