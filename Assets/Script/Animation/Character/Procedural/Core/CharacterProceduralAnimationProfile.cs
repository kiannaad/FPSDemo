using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural Profile", fileName = "CharacterProceduralAnimationProfile")]
    public sealed class CharacterProceduralAnimationProfile : ScriptableObject
    {
        [SerializeField] private KRig rig;
        [SerializeField] private float blendInTime;
        [SerializeField] private float blendOutTime;

        public KRig Rig => rig;
        public float BlendInTime => blendInTime;
        public float BlendOutTime => blendOutTime;
    }
}
