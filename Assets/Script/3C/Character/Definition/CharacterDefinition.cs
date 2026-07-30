using CGame.Animation;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(menuName = "CGame/Character/Character Definition", fileName = "CharacterDefinition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string definitionId;
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private CharacterAnimationConfig animationConfig;
        [SerializeField] private CharacterControlKind[] supportedControlKinds;
        [SerializeField] private string initialWeaponId;

        public CharacterDefinitionId DefinitionId => new CharacterDefinitionId(definitionId);
        public GameObject VisualPrefab => visualPrefab;
        public CharacterAnimationConfig AnimationConfig => animationConfig;
        public CharacterControlKind[] SupportedControlKinds => supportedControlKinds;
        public WeaponId InitialWeaponId => new WeaponId(initialWeaponId);
        public bool IsValid => Validate() == CharacterDefinitionResolveError.None;

        public bool Supports(CharacterControlKind controlKind)
        {
            if (supportedControlKinds == null)
            {
                return false;
            }

            foreach (CharacterControlKind supportedControlKind in supportedControlKinds)
            {
                if (supportedControlKind == controlKind)
                {
                    return true;
                }
            }

            return false;
        }

        public CharacterDefinitionResolveError Validate(CharacterDefinitionId expectedId = default)
        {
            if (!DefinitionId.IsValid)
            {
                return CharacterDefinitionResolveError.InvalidDefinitionId;
            }

            if (expectedId.IsValid && DefinitionId != expectedId)
            {
                return CharacterDefinitionResolveError.DefinitionIdMismatch;
            }

            if (visualPrefab == null)
            {
                return CharacterDefinitionResolveError.MissingVisualPrefab;
            }

            if (animationConfig == null || !animationConfig.IsValid)
            {
                return CharacterDefinitionResolveError.InvalidAnimationConfig;
            }

            if (supportedControlKinds == null || supportedControlKinds.Length == 0)
            {
                return CharacterDefinitionResolveError.MissingSupportedControlKind;
            }

            if (!InitialWeaponId.IsValid)
            {
                return CharacterDefinitionResolveError.InvalidInitialWeaponId;
            }

            return CharacterDefinitionResolveError.None;
        }
    }
}
