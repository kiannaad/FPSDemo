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

    }
}
