using System;
using CGame.Animation;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "PawnDefinition", menuName = "CGame/Gameplay/Pawn Definition")]
    public class PawnDefinition : ScriptableObject
    {
        [SerializeField] private ControllerDefinition controllerDefinition;
        [SerializeField] private PawnFactoryDefinition pawnFactoryDefinition;
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private CharacterAnimationConfig animationConfig;
        [SerializeField] private KRig rig;
        [SerializeField] private Mesh firstPersonMesh;
        [SerializeField] private Material firstPersonMaterial;
        [SerializeField] private bool requireCamera;
        public GameObject PawnPrefab => pawnPrefab;

        public ControllerDefinition ControllerDefinition => controllerDefinition;

        public PawnFactoryDefinition PawnFactoryDefinition => pawnFactoryDefinition;

        public CharacterAnimationConfig AnimationConfig => animationConfig;

        public KRig Rig => rig;

        public Mesh FirstPersonMesh => firstPersonMesh;

        public Material FirstPersonMaterial => firstPersonMaterial;

        public bool RequireCamera => requireCamera;


        public static PawnDefinition CreateRuntime(
            GameObject pawnPrefab,
            params CGame.Ability.AbilitySet[] baseAbilitySets)
        {
            return CreateRuntime(pawnPrefab, false, baseAbilitySets);
        }

        public static PawnDefinition CreateRuntime(
            GameObject pawnPrefab,
            bool requireCamera,
            params CGame.Ability.AbilitySet[] baseAbilitySets)
        {
            PawnDefinition definition = CreateInstance<PawnDefinition>();
            definition.ConfigureRuntime(pawnPrefab);
            definition.requireCamera = requireCamera;
            return definition;
        }

        protected void ConfigureRuntime(GameObject prefab)
        {
            pawnPrefab = prefab;
        }

        public void ConfigureAssembly(
            ControllerDefinition controller,
            PawnFactoryDefinition pawnFactory)
        {
            controllerDefinition = controller ?? throw new ArgumentNullException(nameof(controller));
            pawnFactoryDefinition = pawnFactory ?? throw new ArgumentNullException(nameof(pawnFactory));
        }
    }
}
