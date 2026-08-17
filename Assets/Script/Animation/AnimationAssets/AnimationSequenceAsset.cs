using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(fileName = "AnimationSequenceAsset", menuName = "CGame/Animation/Animation Sequence Asset")]
    public class AnimationSequenceAsset : AnimationAssetBase
    {
        [System.Serializable]
        public class SequenceEntry
        {
            [SerializeField] private AnimationClipAsset clipAsset;
            [SerializeField] private float speed = 1f;

            public AnimationClipAsset ClipAsset
            {
                get => clipAsset;
                set => clipAsset = value;
            }

            public float Speed
            {
                get => speed;
                set => speed = value;
            }
        }

        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
        [SerializeField] private SequenceEntry[] clips = System.Array.Empty<SequenceEntry>();

        public float FadeDuration
        {
            get => fadeDuration;
            set => fadeDuration = Mathf.Max(0f, value);
        }

        public SequenceEntry[] Clips
        {
            get => clips;
            set => clips = value ?? System.Array.Empty<SequenceEntry>();
        }

        public override AnimationClip MainClip
            => clips != null && clips.Length > 0 ? clips[0]?.ClipAsset?.MainClip : null;

        public override bool CanEditNotifies => false;

        public override bool IsValid
        {
            get
            {
                if (clips == null || clips.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i]?.ClipAsset == null || !clips[i].ClipAsset.IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

    }
}
