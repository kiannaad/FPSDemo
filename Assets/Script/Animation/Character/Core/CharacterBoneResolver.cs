using UnityEngine;

namespace CGame.Animation
{
    internal static class CharacterBoneResolver
    {
        public static Transform Resolve(
            Animator animator,
            HumanBodyBones humanBone,
            string genericBoneName)
        {
            if (animator == null)
            {
                return null;
            }

            if (animator.isHuman)
            {
                Transform humanTransform = animator.GetBoneTransform(humanBone);
                if (humanTransform != null)
                {
                    return humanTransform;
                }
            }

            Transform[] transforms = animator.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == genericBoneName)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
