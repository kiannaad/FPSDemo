using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(
        menuName = "CGame/Animation/Character Animation Config",
        fileName = "CharacterAnimationConfig")]
    public sealed class CharacterAnimationConfig : ScriptableObject
    {
        [SerializeField] private AvatarMask upperBodyMask;

        public AvatarMask UpperBodyMask => upperBodyMask;
        public bool IsValid => upperBodyMask != null;

        [System.Obsolete(
            "Legacy CharacterAnimationGraph no longer owns weapon definitions.")]
        public WeaponAnimationDefinition[] WeaponDefinitions =>
            System.Array.Empty<WeaponAnimationDefinition>();
    }
}
